using Logix.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private LogixDriverDiagnostics diagnostics = new();
        private Dictionary<string, IDriver> drivers = new();
        private DynamicSymbolsProvider symbolProvider = new();

        private volatile bool initializing = false;

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);

            // server event handling
            requestListener.OnRequestAsync += OnRequestAsync;
            configListener.OnChangeAsync += OnConfigChangeAsync;
            shutdownListener.OnShutdown += OnShutDown;

            return ErrorValue.HMI_SUCCESS;
        }

        private void DriverConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            if (initializing)
                return;

            var driver = sender as IDriver;

            if (!e.IsConnected)
            {
                diagnostics.Targets[driver!.Target.Name] = new TargetDiagnostics(false, driver.ControllerInfo);
                LogixDriverDiagnostics.TryConnectDriver(driver);
            }
            else
            {
                diagnostics.Targets[driver!.Target.Name] = new TargetDiagnostics(true, driver.ControllerInfo);
                if (!symbolProvider.ContainsKey(driver.Target.Name))
                    LoadDriverSymbolsAsync(driver).GetAwaiter();
            }
        }

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
            // re-initialize
            initializing = true;

            // clean up existing drivers
            foreach (var driver in drivers.Values)
            {
                driver.ConnectionStateChanged -= DriverConnectionStateChanged; 
                driver.Dispose();
            }

            drivers = new Dictionary<string, IDriver>();
            symbolProvider = new DynamicSymbolsProvider();
            diagnostics = new LogixDriverDiagnostics();

            try
            {
                foreach (var targetConfig in configuration.Targets)
                {
                    var targetName = targetConfig.Key;
                    var config = targetConfig.Value;

                    // create / initialize EIP driver
                    var driver = Driver.Create(
                        new Target(
                            name: targetName, 
                            gateway: config.targetAddress, 
                            path: config.targetSlot,
                            timeoutMs: config.timeout,
                            heartbeatInterval: TimeSpan.FromSeconds(5)),
                        new LogixSymbolValueResolver());

                    driver.ConnectionStateChanged += DriverConnectionStateChanged;
                    drivers.Add(targetName, driver);

                    var diag = new TargetDiagnostics();
                    diagnostics.Targets.Add(targetName, diag);

                    if (driver.TryConnect())
                    {
                        diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(true, driver.ControllerInfo);
                        await LoadDriverSymbolsAsync(driver);
                    }
                    else
                    {
                        LogixDriverDiagnostics.TryConnectDriver(driver);
                    }
                }
            }
            catch (Exception ex)
            {
                await TcHmiAsyncLogger.SendAsync(Severity.Error, $"{ex.Message}\n{ex.StackTrace}", []);
            }
            finally
            {
                initializing = false;
            }
        }

        // connect driver, load tags and create symbol provider
        private async Task LoadDriverSymbolsAsync(IDriver driver)
        {
            if (!configuration.Targets.TryGetValue(driver.Target.Name, out var config))
                return;

            if (driver.IsConnected)
            {
                await driver.LoadTagsAsync(config.tagSelector);

                // re / create symbol
                if (symbolProvider.TryGetValue(driver.Target.Name, out var oldSymbol))
                {
                    (oldSymbol as LogixSymbol)!.Dispose();
                    symbolProvider.Remove(driver.Target.Name);
                }
                symbolProvider.Add(driver.Target.Name, new LogixSymbol(driver));
            }
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private async Task OnRequestAsync(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var ret = ErrorValue.HMI_SUCCESS;
            var context = e.Context;
            var commands = e.Commands;

            try
            {
                if (commands.Count == 1 && commands.First().Mapping == "ListSymbols")
                {
                    foreach (var symbol in symbolProvider.Values)
                        await (symbol as LogixSymbol)!.UpdateMappedSymbolsAsync();
                }

                foreach (var command in await symbolProvider!.HandleCommandsAsync(commands, context))
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

        // cleanup
        private void OnShutDown(object? sender, TcHmiSrv.Core.Listeners.ShutdownListenerEventArgs.OnShutdownEventArgs e)
        {
            requestListener.OnRequestAsync -= OnRequestAsync;
            configListener.OnChangeAsync -= OnConfigChangeAsync;
            shutdownListener.OnShutdown -= OnShutDown;

            foreach (var driver in drivers.Values)
            {
                driver.ConnectionStateChanged -= DriverConnectionStateChanged;
                driver.Dispose();
            }
        }
    }
}
