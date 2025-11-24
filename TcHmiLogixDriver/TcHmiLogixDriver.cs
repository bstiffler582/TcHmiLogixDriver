//-----------------------------------------------------------------------
// <copyright file="TcHmiLogixDriver.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using Logix;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using TcHmiLogixDriver.Logix;
using TcHmiLogixDriver.Logix.Symbols;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Listeners.ConfigListenerEventArgs;
using TcHmiSrv.Core.Listeners.ShutdownListenerEventArgs;
using TcHmiSrv.Core.Listeners.SubscriptionListenerEventArgs;
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
        private SubscriptionListener subscriptionListener = new();

        private LogixDriverConfig configuration;
        private LogixDriverDiagnostics diagnostics;

        private DynamicSymbolsProvider symbolProvider;
        private HashSet<string> requestedSchemas = new();
        private Dictionary<string, LogixDriver> drivers;

        private Timer connectionStateTimer;

        // debug
        private Queue<string> requestExceptionLog = new();

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);

            // server event handling
            requestListener.OnRequest += onRequest;
            configListener.OnChange += onConfigChange;
            shutdownListener.OnShutdown += onShutDown;
            subscriptionListener.OnUnsubscribe += onUnsubscribe;

            // target connection state management
            connectionStateTimer = new Timer(5000);
            connectionStateTimer.AutoReset = false;
            connectionStateTimer.Elapsed += onConnectionStateTimerElapsed;
            connectionStateTimer.Start();

            symbolProvider = new DynamicSymbolsProvider();

            return ErrorValue.HMI_SUCCESS;
        }

        // target connection state management
        private void onConnectionStateTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var disconnectedDrivers = drivers.Values.Where(d => !d.IsConnected);
            foreach (var driver in disconnectedDrivers) 
            {
                try
                {
                    TryConnectDriver(driver);
                }
                catch (Exception ex) 
                {
                    System.IO.File.AppendAllText("configLoad.log", $"{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}");
                }
            }
            connectionStateTimer.Start();
        }

        private void onShutDown(object sender, OnShutdownEventArgs e)
        {
            requestListener.OnRequest -= onRequest;
            configListener.OnChange -= onConfigChange;
            shutdownListener.OnShutdown -= onShutDown;
            subscriptionListener.OnUnsubscribe -= onUnsubscribe;

            connectionStateTimer.Stop();
            connectionStateTimer.Elapsed -= onConnectionStateTimerElapsed;
            connectionStateTimer.Dispose();

            foreach (var driver in drivers.Values)
                driver.Dispose();

            System.IO.File.AppendAllLines("requestLog.log", requestExceptionLog);
        }

        // update configuration
        private void onConfigChange(object sender, OnChangeEventArgs e)
        {
            if (e.Path != "Targets")
                return;

            drivers = new Dictionary<string, LogixDriver>();
            symbolProvider = new DynamicSymbolsProvider();
            diagnostics = new LogixDriverDiagnostics();

            try
            {
                configuration = GetConfiguration();

                foreach (var targetConfig in configuration.Targets)
                {
                    var targetName = targetConfig.Key;
                    var config = targetConfig.Value;

                    var target = new LogixTarget(targetName, config.targetAddress, config.targetSlot);
                    var driver = new LogixDriver(target);
                    var diag = new TargetDiagnostics();

                    diagnostics.Targets.Add(targetName, diag);
                    drivers.Add(targetName, driver);

                    TryConnectDriver(driver);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("configLoad.log", $"{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}");
                Console.WriteLine("Error loading configuration: " + ex.ToString());
            }
        }

        private void TryConnectDriver(LogixDriver driver)
        {
            var info = driver.ReadControllerInfo();
            var config = configuration.Targets[driver.Target.Name];

            if (!string.IsNullOrEmpty(info))
            {
                // update diagnostics
                diagnostics.Targets[driver.Target.Name] =
                    new TargetDiagnostics(connectionState: "CONNECTED", model: info.Split(' ')[0], info.Split(' ')[2]);

                // browse tags
                if (config.tagBrowser)
                {
                    var tags = driver.LoadTags();

                    // cache tags
                    var cacheConfigPath = $"Targets::{driver.Target.Name}::tagDefinitionCache";
                    var json = JsonConvert.SerializeObject(tags);
                    TcHmiApplication.AsyncHost.ReplaceConfigValue(TcHmiApplication.Context, cacheConfigPath, json);
                }

                // re / create symbol
                if (symbolProvider.TryGetValue(driver.Target.Name, out var oldSymbol))
                {
                    (oldSymbol as LogixSymbol).Dispose();
                    symbolProvider.Remove(driver.Target.Name);
                }

                symbolProvider.Add(driver.Target.Name, new LogixSymbol(driver, requestedSchemas));
            }
            else
            {
                // update diagnostics
                diagnostics.Targets[driver.Target.Name] =
                    new TargetDiagnostics(connectionState: "DISCONNECTED", "", "");

                // read tag defintion cache
                if (!config.tagBrowser && !string.IsNullOrEmpty(config.tagDefinitionCache))
                {
                    var tags = JsonConvert.DeserializeObject<IEnumerable<TagDefinition>>(config.tagDefinitionCache);
                    driver.Target.AddTagDefinition(tags);
                }
            }

            // update symbol provider
            if (symbolProvider.TryGetValue(driver.Target.Name, out var symbol))
                symbol = new LogixSymbol(driver, requestedSchemas);
            else
                symbolProvider.Add(driver.Target.Name, new LogixSymbol(driver, requestedSchemas));
        }

        private LogixDriverConfig GetConfiguration()
        {
            var config = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "Targets");
            
            var targets = new Dictionary<string, TargetConfig>();
            foreach (var target in config.Keys)
            {
                var targetConfig = TcHmiJsonSerializer.Deserialize<TargetConfig>(config[target].ToJson(), false);
                targets.Add(target, targetConfig);
            }

            return new LogixDriverConfig
            {
                Targets = targets
            };
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void onRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var commands = e.Commands;

            try
            {
                e.Commands.Result = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;

                // store requested schema paths - maybe there is a better way to retrieved mapped symbols?
                if (commands.First().Name == "TcHmiLogixDriver.GetSchema")
                    requestedSchemas.Add(commands.First().WriteValue);

                foreach (var command in symbolProvider.HandleCommands(e.Commands, e.Context))
                {
                    // Use the mapping to check which command is requested
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
            }
            catch (Exception ex)
            {
                logRequestException(ex);

                var command = e.Commands.FirstOrDefault();
                if (command != null)
                {
                    command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                    command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                }
                else
                {
                    Console.WriteLine("u r cooked: " + ex.ToString(), ErrorValue.HMI_E_EXTENSION);
                }
                
            }
        }

        private void onUnsubscribe(object sender, OnUnsubscribeEventArgs e)
        {
            foreach (var symbol in symbolProvider.Values)
            {
                (symbol as LogixSymbol).UnsubscribeById(e.Context.SubscriptionId);
            }
        }

        private void logRequestException(Exception ex)
        {
            requestExceptionLog.Enqueue($"{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}");
            if (requestExceptionLog.Count > 250)
                requestExceptionLog.Dequeue();
        }
    }
}
