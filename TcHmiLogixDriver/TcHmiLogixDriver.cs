//-----------------------------------------------------------------------
// <copyright file="TcHmiLogixDriver.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using Logix;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TcHmiLogixDriver.Logix;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Listeners;
using TcHmiSrv.Core.Listeners.ConfigListenerEventArgs;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using TcHmiSrv.Core.Tools.Json.Extensions;
using TcHmiSrv.Core.Tools.Json.Newtonsoft;
using TcHmiSrv.Core.Tools.Json.Newtonsoft.Converters;
using TcHmiSrv.Core.Tools.Management;
using TcHmiSrv.Core.Tools.Resolving.Handlers;

namespace TcHmiLogixDriver
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmiLogixDriver : IServerExtension
    {
        private readonly RequestListener requestListener = new RequestListener();
        private readonly ConfigListener configListener = new ConfigListener();
        private readonly ShutdownListener shutdownListener = new ShutdownListener();
        
        private LogixDriverConfig configuration;
        private LogixDriverDiagnostics diagnostics;
        private DynamicSymbolsProvider symbolProvider;

        private HashSet<string> requestedSchemas = new HashSet<string>();
        private MappingTree mappingTree = new MappingTree();

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            requestListener.OnRequest += OnRequest;
            configListener.OnChange += OnConfigChange;

            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);
            symbolProvider = new DynamicSymbolsProvider();

            return ErrorValue.HMI_SUCCESS;
        }

        private void OnConfigChange(object sender, OnChangeEventArgs e)
        {
            if (e.Path != "Targets")
                return;

            try
            {
                configuration = GetConfiguration();
                symbolProvider = new DynamicSymbolsProvider();

                foreach (var t in configuration.Targets)
                {
                    LoadTarget(t.Key, t.Value);
                }
            }
            catch (Exception ex)
            {
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

        private void LoadTarget(string targetName, TargetConfig config)
        {
            //var defs = System.IO.File.ReadAllText("tags.json") is string json
            //    ? JsonConvert.DeserializeObject<Dictionary<string, TagDefinition>>(json)
            //    : new Dictionary<string, TagDefinition>();

            var target = new LogixTarget(targetName, config.targetAddress, config.targetSlot);
            var driver = new LogixDriver();

            if (config.tagBrowser)
            {
                // TODO: cache tags/defs in config so it works without browsing?
                driver.LoadTags(target);
            }

            symbolProvider.Add(target.Name, new LogixSymbol(target, driver, mappingTree));
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void OnRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var commands = e.Commands;

            try
            {
                e.Commands.Result = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;

                // store requested schema paths
                if (commands.First().Name == "TcHmiLogixDriver.GetSchema")
                {
                    var mappingCmd = commands.First();
                    if (!requestedSchemas.Contains(mappingCmd.WriteValue))
                    {
                        requestedSchemas.Add(mappingCmd.WriteValue);
                        var path = TcHmiApplication.SplitSymbolPath(mappingCmd.WriteValue, StringSplitOptions.RemoveEmptyEntries);
                        mappingTree.AddPath(path);
                    }
                }

                foreach (var command in symbolProvider.HandleCommands(e.Commands))
                {
                    try
                    {
                        // Use the mapping to check which command is requested
                        switch (command.Mapping)
                        {
                            case "Diagnostics":
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;
                                command.ReadValue = GetDiagnostics();
                                break;

                            default:
                                command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                                command.ResultString = "Unknown command '" + command.Mapping + "' not handled.";
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                        command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                var command = e.Commands.FirstOrDefault();
                if (command != null)
                {
                    command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverFail;
                    command.ResultString = "Calling command '" + command.Mapping + "' failed! Additional information: " + ex.ToString();
                }
                else
                {
                    throw new TcHmiException("u r cooked: " + ex.ToString(), ErrorValue.HMI_E_EXTENSION);
                }
                
            }
        }

        private Value GetDiagnostics()
        {
            var diag = new LogixDriverDiagnostics();
            diag.Targets.Add("Test", new TargetDiagnostics(connectionState: "CONNECTED", model: "", firmware: ""));
            return TcHmiJsonSerializer.Deserialize(ValueJsonConverter.DefaultConverter, JsonConvert.SerializeObject(diag));
        }
    }
}
