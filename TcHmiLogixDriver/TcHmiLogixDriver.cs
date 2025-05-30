//-----------------------------------------------------------------------
// <copyright file="TcHmiLogixDriver.cs" company="Beckhoff Automation GmbH & Co. KG">
//     Copyright (c) Beckhoff Automation GmbH & Co. KG. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
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
    public class LogixSymbol : Symbol
    {
        public LogixSymbol(JsonSchemaValue readValue) : 
            base(readValue)
        {

        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            throw new NotImplementedException();
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }

    // Represents the default type of the TwinCAT HMI server extension.
    public class TcHmiLogixDriver : IServerExtension
    {
        private readonly RequestListener requestListener = new RequestListener();
        private readonly ConfigListener configListener = new ConfigListener();
        private readonly ShutdownListener shutdownListener = new ShutdownListener();
        
        private LogixDriverConfig configuration = new LogixDriverConfig();
        private LogixDriver driver = LogixDriver.Instance;
        private DynamicSymbolsProvider symbolProvider;

        // Called after the TwinCAT HMI server loaded the server extension.
        public ErrorValue Init()
        {
            requestListener.OnRequest += OnRequest;
            configListener.OnChange += OnConfigChange;

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
                var dict = new Dictionary<string, Symbol>();
                JSchema schema = JSchema.Parse(@"{
                  'type': 'object',
                  'properties': {
                    'name': {'type':'string'},
                    'roles': {'type': 'array'}
                  }
                }");
                schema.Items.Add(new JSchema());
                
                dict.Add("SomeTest", new LogixSymbol(new JsonSchemaValue(schema)));

                symbolProvider = new DynamicSymbolsProvider(dict);
            }
        }

        // Called when a client requests a symbol from the domain of the TwinCAT HMI server extension.
        private void OnRequest(object sender, TcHmiSrv.Core.Listeners.RequestListenerEventArgs.OnRequestEventArgs e)
        {
            var commands = e.Commands;

            try
            {
                e.Commands.Result = TcHmiLogixDriverErrorValue.TcHmiLogixDriverSuccess;

                foreach (var command in symbolProvider.HandleCommands(commands))
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
