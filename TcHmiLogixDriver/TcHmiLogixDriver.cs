//-----------------------------------------------------------------------
// <copyright file="TcHmiLogixDriver.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using Logix;
using TcHmiLogixDriver.Logix;
using TcHmiLogixDriver.Logix.Symbols;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Listeners.ConfigListenerEventArgs;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using TcHmiSrv.Core.Tools.Json.Extensions;
using TcHmiSrv.Core.Tools.Json.Newtonsoft;
using TcHmiSrv.Core.Tools.Json.Newtonsoft.Converters;
using TcHmiSrv.Core.Tools.Management;
using TcHmiSrv.Core.Listeners.ShutdownListenerEventArgs;

namespace TcHmiLogixDriver
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmiLogixDriver : IServerExtension
    {
        private readonly RequestListener requestListener = new();
        private readonly ConfigListener configListener = new();
        private readonly ShutdownListener shutdownListener = new();
        
        
        private LogixDriverConfig configuration;
        private DynamicSymbolsProvider symbolProvider;

        private HashSet<string> requestedSchemas = new();
        private Dictionary<string, LogixDriver> drivers;
        private Value diagnosticsValue;

        private readonly SubscriptionManager subscriptionManager = new();

        // debug
        private Queue<string> requestExceptionLog = new();

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            requestListener.OnRequest += onRequest;
            configListener.OnChange += onConfigChange;
            shutdownListener.OnShutdown += onShutDown;

            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);
            symbolProvider = new DynamicSymbolsProvider();

            return ErrorValue.HMI_SUCCESS;
        }

        private void onShutDown(object sender, OnShutdownEventArgs e)
        {
            requestListener.OnRequest -= onRequest;
            configListener.OnChange -= onConfigChange;
            shutdownListener.OnShutdown -= onShutDown;

            System.IO.File.AppendAllLines("requestLog.log", requestExceptionLog);
        }

        private void onConfigChange(object sender, OnChangeEventArgs e)
        {
            if (e.Path != "Targets")
                return;

            drivers = new Dictionary<string, LogixDriver>();

            try
            {
                configuration = GetConfiguration();
                symbolProvider = new DynamicSymbolsProvider();
                drivers = new Dictionary<string, LogixDriver>();

                foreach (var t in configuration.Targets)
                {
                    var driver = LoadTarget(t.Key, t.Value);
                    drivers.Add(t.Key, driver);
                }

                diagnosticsValue = GetDiagnostics();
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("configLoad.log", $"{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}");
                Console.WriteLine("Error loading configuration: " + ex.ToString());
            }
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

        private LogixDriver LoadTarget(string targetName, TargetConfig config)
        {
            var target = new LogixTarget(targetName, config.targetAddress, config.targetSlot);
            var driver = new LogixDriver(target);

            var cacheConfigPath = $"Targets::{target.Name}::tagDefinitionCache";
            if (config.tagBrowser)
            {
                var tags = driver.LoadTags();

                // cache tag defintions in config for offline use
                var json = JsonConvert.SerializeObject(tags);
                TcHmiApplication.AsyncHost.ReplaceConfigValue(TcHmiApplication.Context, $"Targets::{target.Name}::tagDefinitionCache", json);
            }
            else
            {
                // load tag definitions from cache
                if (!string.IsNullOrEmpty(config.tagDefinitionCache)) 
                {
                    var tags = JsonConvert.DeserializeObject<IEnumerable<TagDefinition>>(config.tagDefinitionCache);
                    target.AddTagDefinition(tags);
                }
            }

            // maybe store requested schemas in subscription manager as well?
            // then pass that into Symbol
            symbolProvider.Add(target.Name, new LogixSymbol(driver, requestedSchemas));
            return driver;
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
                    try
                    {
                        // Use the mapping to check which command is requested
                        switch (command.Mapping)
                        {
                            case "Diagnostics":
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;
                                command.ReadValue = diagnosticsValue ?? GetDiagnostics();
                                break;

                            default:
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        logRequestException(ex);

                        command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                        command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
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

        private void logRequestException(Exception ex)
        {
            requestExceptionLog.Enqueue($"{DateTime.Now.ToString()}\n{ex.Message}\n{ex.StackTrace}");
            if (requestExceptionLog.Count > 250)
                requestExceptionLog.Dequeue();
        }

        private Value GetDiagnostics()
        {
            var diagnostics = new LogixDriverDiagnostics();

            foreach (var driver in drivers.Values)
            {
                TargetDiagnostics diag;
                var diagString = driver.ReadControllerInfo();

                if (diagString != string.Empty)
                    diag = new TargetDiagnostics(connectionState: "CONNECTED", model: diagString.Split(' ')[0], diagString.Split(' ')[2]);
                else
                    diag = new TargetDiagnostics(connectionState: "DISCONNECTED", model: "", firmware: "");

                diagnostics.Targets.Add(driver.Target.Name, diag);
            }

            diagnosticsValue = TcHmiJsonSerializer.Deserialize(ValueJsonConverter.DefaultConverter, JsonConvert.SerializeObject(diagnostics));
            return diagnosticsValue;
        }
    }
}
