using Logix.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TcHmiLogixDriver.Logix;
using TcHmiLogixDriver.Logix.Symbols;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using TcHmiSrv.Core.Tools.Json.Extensions;
using TcHmiSrv.Core.Tools.Json.Newtonsoft;
using TcHmiSrv.Core.Tools.Management;

namespace TcHmiLogixDriver
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmiLogixDriver : IServerExtension
    {
        private readonly RequestListener requestListener = new();
        private readonly ConfigListener configListener = new();
        private readonly ShutdownListener shutdownListener = new();

        private LogixDriverConfig configuration = new();

        // Shared between request handling and driver connection events (raised on driver monitor
        // threads). Published by reference swap; drivers and symbolProvider are never mutated after
        // publishing (symbolProvider is copy-on-write), diagnostics entries are concurrent.
        private volatile IReadOnlyDictionary<string, IDriver> drivers = new Dictionary<string, IDriver>();
        private volatile DynamicSymbolsProvider symbolProvider = new();
        private volatile LogixDriverDiagnostics diagnostics = new();

        private readonly SemaphoreSlim initialize = new(1, 1);
        // Serializes symbol loads and every publish of symbolProvider/drivers.
        private readonly SemaphoreSlim symbolsGate = new(1, 1);

        private static bool IsEngineering => TcHmiApplication.Path.Contains(".engineering_servers");

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);

            // server event handling
            requestListener.OnRequestAsync += OnRequestAsync;
            configListener.OnChangeAsync += OnConfigChangeAsync;
            shutdownListener.OnShutdownAsync += OnShutdownAsync;

            return ErrorValue.HMI_SUCCESS;
        }

        // Raised on the driver's connection monitor thread: keep it quick and non-blocking.
        // Reconnection is handled by the driver itself; only diagnostics and the tag tree need updating.
        private void DriverConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            // ignore events from drivers being torn down by a config change
            if (sender is not IDriver driver || !IsCurrent(driver))
                return;

            diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(e.IsConnected, driver.ControllerInfo);

            if (e.IsConnected)
                _ = LoadDriverSymbolsAsync(driver);
        }

        private bool IsCurrent(IDriver driver) =>
            drivers.TryGetValue(driver.Target.Name, out var current) && ReferenceEquals(current, driver);

        // configuration updated
        private async Task OnConfigChangeAsync(object sender, TcHmiSrv.Core.Listeners.ConfigListenerEventArgs.OnChangeEventArgs e)
        {
            if (e.Path != "Targets")
                return;

            var config = await TcHmiApplication.AsyncHost.GetConfigValueAsync(TcHmiApplication.Context, "Targets");

            var targets = new Dictionary<string, TargetConfig>();
            foreach (var target in config.Keys)
            {
                var targetConfig = TcHmiJsonSerializer.Deserialize<TargetConfig>(config[target].ToJson(), false);
                targets.Add(target, targetConfig);
            }

            configuration = new LogixDriverConfig(targets);
            await CreateDriversAsync();
        }

        private async Task CreateDriversAsync()
        {
            // Wait rather than skip: a config change arriving mid-create must still be applied.
            // Each pass reads the latest configuration.
            await initialize.WaitAsync();

            try
            {
                var newDrivers = new Dictionary<string, IDriver>();
                var newDiagnostics = new LogixDriverDiagnostics();

                foreach (var (targetName, config) in configuration.Targets)
                {
                    var target = new Target(
                            name: targetName,
                            gateway: config.targetAddress,
                            path: config.targetSlot,
                            timeoutMs: config.timeout,
                            heartbeatInterval: TimeSpan.FromSeconds(5));

                    target.MaxConcurrentOperations = config.maxConcurrentOperations;

                    var driver = Driver.Create(
                        target,
                        new LogixSymbolValueResolver());

                    newDrivers.Add(targetName, driver);
                    newDiagnostics.Targets[targetName] = new TargetDiagnostics();
                }

                IEnumerable<IDriver> oldDrivers;
                await symbolsGate.WaitAsync();
                try
                {
                    // Publish before connecting so the connection events below are recognised as current.
                    // Under symbolsGate so an in-progress symbol load for an old driver can't publish afterwards.
                    oldDrivers = drivers.Values;
                    diagnostics = newDiagnostics;
                    symbolProvider = new DynamicSymbolsProvider();
                    drivers = newDrivers;
                }
                finally
                {
                    symbolsGate.Release();
                }

                DriverCleanup(oldDrivers);

                foreach (var driver in newDrivers.Values)
                    driver.ConnectionStateChanged += DriverConnectionStateChanged;

                // Symbols load from the Connected event (subscribed above, before any transition can happen),
                // and a failed first attempt is retried by the driver's reconnect loop.
                await Task.WhenAll(newDrivers.Values.Select(d => d.TryConnectAsync()));
            }
            catch (Exception ex)
            {
                await TcHmiAsyncLogger.SendAsync(Severity.Error, $"{ex.Message}\n{ex.StackTrace}", []);
            }
            finally
            {
                initialize.Release();
            }
        }

        // Loads the driver's tag tree and (re)creates its symbol. Fire-and-forget from connection
        // events, so it logs rather than throws.
        private async Task LoadDriverSymbolsAsync(IDriver driver)
        {
            var targetName = driver.Target.Name;

            await symbolsGate.WaitAsync();
            try
            {
                // Outside engineering the tag tree is loaded once; in engineering it's reloaded on every
                // connect, since the PLC program may have been changed.
                if (!IsCurrent(driver) || !driver.IsConnected)
                    return;
                if (symbolProvider.ContainsKey(targetName) && !IsEngineering)
                    return;
                if (!configuration.Targets.TryGetValue(targetName, out var config))
                    return;

                // reloading (engineering only): forget what was read before, the program may have changed
                if (symbolProvider.ContainsKey(targetName))
                    driver.Tags.Refresh();

                await driver.LoadTagsAsync(config.tagSelector);

                // copy-on-write: requests may be enumerating the current provider
                var current = symbolProvider;
                var next = new DynamicSymbolsProvider();
                foreach (var entry in current)
                {
                    if (entry.Key != targetName)
                        next.Add(entry.Key, entry.Value);
                }
                next.Add(targetName, new LogixSymbol(driver, config.tagSelector));

                symbolProvider = next;

                if (current.TryGetValue(targetName, out var oldSymbol))
                    (oldSymbol as LogixSymbol)?.Dispose();
            }
            catch (Exception ex)
            {
                await TcHmiAsyncLogger.SendAsync(Severity.Error, $"Loading symbols for target {targetName} failed: {ex.Message}", []);
            }
            finally
            {
                symbolsGate.Release();
            }
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private async Task OnRequestAsync(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var ret = ErrorValue.HMI_SUCCESS;
            var context = e.Context;
            var commands = e.Commands;

            // one snapshot for the whole request; a concurrent symbol load publishes a new provider
            var provider = symbolProvider;

            try
            {
                if (commands.Count == 1 && commands.First().Mapping == "ListSymbols")
                {
                    foreach (var symbol in provider.Values)
                        await (symbol as LogixSymbol)!.UpdateMappedSymbolsAsync();
                }

                foreach (var command in await provider.HandleCommandsAsync(commands, context))
                {
                    var mapping = command.Mapping;

                    try
                    {
                        switch (command.Mapping)
                        {
                            case "Diagnostics":
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;
                                command.ReadValue = diagnostics.ToValue();
                                break;

                            default:
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        command.ExtensionResult = Convert.ToUInt32(TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail);
                        command.ResultString =
                            await TcHmiAsyncLogger.LocalizeAsync(context, "ERROR_CALL_COMMAND", mapping, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new TcHmiException(ex.Message, ret == ErrorValue.HMI_SUCCESS ? ErrorValue.HMI_E_EXTENSION : ret);
            }
        }

        private void DriverCleanup(IEnumerable<IDriver> toClean)
        {
            foreach (var driver in toClean)
            {
                driver.ConnectionStateChanged -= DriverConnectionStateChanged;
                driver.Dispose();
            }
        }

        // cleanup
        private async Task OnShutdownAsync(object? sender, TcHmiSrv.Core.Listeners.ShutdownListenerEventArgs.OnShutdownEventArgs e)
        {
            requestListener.OnRequestAsync -= OnRequestAsync;
            configListener.OnChangeAsync -= OnConfigChangeAsync;
            shutdownListener.OnShutdownAsync -= OnShutdownAsync;

            // don't tear drivers down underneath an in-progress create
            await initialize.WaitAsync();

            var toClean = drivers.Values;
            drivers = new Dictionary<string, IDriver>();
            DriverCleanup(toClean);

            initialize.Dispose();
        }
    }
}
