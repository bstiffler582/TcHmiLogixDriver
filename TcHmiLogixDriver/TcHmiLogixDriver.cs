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
        private LogixDriverDiagnostics diagnostics = new();
        private Dictionary<string, IDriver> drivers = new();
        private DynamicSymbolsProvider symbolProvider = new();

        private CancellationTokenSource? connectionStateCts;
        private volatile bool initializing = false;

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);

            // server event handling
            requestListener.OnRequestAsync += onRequestAsync;
            configListener.OnChangeAsync += onConfigChangeAsync;
            shutdownListener.OnShutdown += onShutDown;

            connectionStateCts = new CancellationTokenSource();
            var monitorConnectionState = Task.Run(() =>
                ConnectionStateAsync(connectionStateCts.Token), connectionStateCts.Token);

            return ErrorValue.HMI_SUCCESS;
        }

        // Polling for connection state, update symbol mappings
        private async Task ConnectionStateAsync(CancellationToken cancel, uint interval = 5000)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
            while (await timer.WaitForNextTickAsync(cancel))
            {
                if (initializing)
                    return;

                try
                {
                    foreach (var driver in drivers.Values.Where(d => !d.IsConnected))
                    {
                        // try reconnect
                        if (driver.TryConnect())
                            diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(true, driver.ControllerInfo);
                        else
                            diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(false, "");
                    }

                    // update symbol mappings
                    await UpdateMappedSymbolListAsync();
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText("configLoad.log", $"\n{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}\n");
                    Console.WriteLine("Error on reconnect: " + ex.ToString());
                }
            }
        }

        // request mapped symbol list from TcHmiSrv
        private async Task UpdateMappedSymbolListAsync()
        {
            if (symbolProvider is null || symbolProvider.Values.Count < 1)
                return;

            // read all mapped symbols
            var (result, ctx, cmd) = await TcHmiApplication.AsyncHost.ExecuteAsync(
                TcHmiApplication.Context, new Command("ListSymbols"));

            if (result != ErrorValue.HMI_SUCCESS)
                return;

            // filter for TcHmiLogixDriver symbols
            var domainSymbolNames = cmd.ReadValue.Keys
                .Where(s => s.StartsWith(ctx.Domain));

            // update symbol providers
            foreach (var symbol in symbolProvider.Where(s => s.Value is LogixSymbol))
            {
                var targetSymbolNames = domainSymbolNames.Where(s => s.Contains(symbol.Key));
                (symbol.Value as LogixSymbol)!.UpdateMappedSymbols(targetSymbolNames);
            }
        }

        // configuration updated
        private async Task onConfigChangeAsync(object sender, TcHmiSrv.Core.Listeners.ConfigListenerEventArgs.OnChangeEventArgs e)
        {
            if (e.Path != "Targets")
                return;

            // clean up drivers
            foreach (var driver in drivers.Values)
                driver.Dispose();

            // re-initialize
            initializing = true;
            drivers = new Dictionary<string, IDriver>();
            symbolProvider = new DynamicSymbolsProvider();
            diagnostics = new LogixDriverDiagnostics();

            try
            {
                configuration = await GetConfigurationAsync();

                foreach (var targetConfig in configuration.Targets)
                {
                    var targetName = targetConfig.Key;
                    var config = targetConfig.Value;

                    // create / initialize EIP driver
                    var driver = Driver.Create(
                        new Target(targetName, config.targetAddress, config.targetSlot), 
                        new LogixSymbolValueResolver());

                    drivers.Add(targetName, driver);

                    var diag = new TargetDiagnostics();
                    diagnostics.Targets.Add(targetName, diag);

                    await InitializeDriverAsync(driver);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("configLoad.log", $"\n{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}\n");
                Console.WriteLine("Error loading configuration: " + ex.ToString());
            }
            finally
            {
                initializing = false;
            }
        }

        // retrieve extension config data
        private async Task<LogixDriverConfig> GetConfigurationAsync()
        {
            var config = await TcHmiApplication.AsyncHost.GetConfigValueAsync(TcHmiApplication.Context, "Targets");

            var targets = new Dictionary<string, TargetConfig>();
            foreach (var target in config.Keys)
            {
                var targetConfig = TcHmiJsonSerializer.Deserialize<TargetConfig>(config[target].ToJson(), false);
                targets.Add(target, targetConfig);
            }

            return new LogixDriverConfig(targets);
        }

        // initialize driver, load tags, create symbol(s)
        private async Task InitializeDriverAsync(IDriver driver)
        {
            if (!configuration.Targets.TryGetValue(driver.Target.Name, out var config))
                return;

            if (driver.TryConnect())
            {
                var info = driver.ControllerInfo;
                diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(true, info);

                await driver.LoadTagsAsync(config.tagSelector);

                // re / create symbol
                if (symbolProvider.TryGetValue(driver.Target.Name, out var oldSymbol))
                {
                    (oldSymbol as LogixSymbol)!.Dispose();
                    symbolProvider.Remove(driver.Target.Name);
                }
                symbolProvider.Add(driver.Target.Name, new LogixSymbol(driver));
            }
            else
            {
                // update diagnostics
                diagnostics.Targets[driver.Target.Name] = new TargetDiagnostics(isConnected: false, "");
            }
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private async Task onRequestAsync(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var ret = ErrorValue.HMI_SUCCESS;
            var context = e.Context;
            var commands = e.Commands;

            try
            {
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
                            await TcHmiAsyncLogger.LocalizeAsync(context, "ERROR_CALL_COMMAND", mapping, ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                throw new TcHmiException(ex.ToString(), ret == ErrorValue.HMI_SUCCESS ? ErrorValue.HMI_E_EXTENSION : ret);
            }
        }

        // cleanup
        private void onShutDown(object? sender, TcHmiSrv.Core.Listeners.ShutdownListenerEventArgs.OnShutdownEventArgs e)
        {
            connectionStateCts!.Cancel();

            requestListener.OnRequestAsync -= onRequestAsync;
            configListener.OnChangeAsync -= onConfigChangeAsync;
            shutdownListener.OnShutdown -= onShutDown;

            foreach (var driver in drivers.Values)
                driver.Dispose();
        }
    }
}
