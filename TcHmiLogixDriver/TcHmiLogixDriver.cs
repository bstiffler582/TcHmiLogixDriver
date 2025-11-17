//-----------------------------------------------------------------------
// <copyright file="TcHmiLogixDriver.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace TcHmiLogixDriver
{
    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmiLogixDriver : IServerExtension
    {
        private readonly RequestListener requestListener = new RequestListener();
        private readonly ConfigListener configListener = new ConfigListener();
        private readonly ShutdownListener shutdownListener = new ShutdownListener();
        
        private LogixDriverConfig configuration = new LogixDriverConfig();

        //private LogixDriver driver = LogixDriver.Instance;
        private DynamicSymbolsProvider symbolProvider;
        private Value listSymbols = new Value();

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            requestListener.OnRequest += OnRequest;
            configListener.OnChange += OnConfigChange;

            //TcHmiApplication.AsyncDebugHost.WaitForDebugger(true);

            return ErrorValue.HMI_SUCCESS;
        }

        private void OnConfigChange(object sender, OnChangeEventArgs e)
        {
            if (e.Path == "targets")
            {
                LoadConfiguration();
            }
        }

        private void LoadConfiguration()
        {
            var config = TcHmiApplication.AsyncHost.GetConfigValue(TcHmiApplication.Context, "targets");
            var targets = new Dictionary<string, TargetConfig>();
            foreach (var target in config.Keys)
            {
                var targetConfig = TcHmiJsonSerializer.Deserialize<TargetConfig>(config[target].ToJson(), false);
                targets.Add(target, targetConfig);
            }
            configuration.Targets = targets;

            if (configuration.Targets.Count > 0)
            {
                try
                {
                    var defs = System.IO.File.ReadAllText("tags.json") is string json
                        ? JsonConvert.DeserializeObject<Dictionary<string, TagDefinition>>(json)
                        : new Dictionary<string, TagDefinition>();

                    var symbolAdapter = new LogixSymbolAdapter("MyTarget", defs.Values);
                    
                    symbolProvider = new DynamicSymbolsProvider();
                    symbolProvider.Add("MyTarget", symbolAdapter.Symbol);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading tags: " + ex.ToString());
                }
            }
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void OnRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var commands = e.Commands;

            try
            {
                e.Commands.Result = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;

                foreach (var command in symbolProvider.HandleCommands(e.Commands))
                {
                    try
                    {
                        // Use the mapping to check which command is requested
                        switch (command.Mapping)
                        {
                            //case "ListSymbols":
                            //    command.ExtensionResult = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;
                            //    command.ReadValue = listSymbols;
                            //    break;
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
                throw new TcHmiException(ex.ToString(), ErrorValue.HMI_E_EXTENSION);
            }
        }

        private Value GetDiagnostics()
        {
            var diag = new LogixDriverDiagnostics();
            diag.Targets.Add("Test", new TargetDiagnostics(connectionState: "CONNECTED"));
            return TcHmiJsonSerializer.Deserialize(ValueJsonConverter.DefaultConverter, JsonConvert.SerializeObject(diag));
        }
    }
}
