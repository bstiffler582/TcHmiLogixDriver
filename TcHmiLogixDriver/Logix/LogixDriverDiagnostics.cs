using System.Collections.Generic;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    record TargetDiagnostics(string connectionState = "CONNECTING...", string model = "", string firmware = "");
    class LogixDriverDiagnostics
    {
        public Dictionary<string, TargetDiagnostics> Targets { get; set; } = new Dictionary<string, TargetDiagnostics>();

        public Value ToValue()
        {
            var targets = new Value();

            foreach (var target in Targets) 
            {
                var targetValue = new Value();
                targetValue.Add("connectionState", target.Value.connectionState);
                targetValue.Add("model", target.Value.model);
                targetValue.Add("firmware", target.Value.firmware);

                targets.Add(target.Key, targetValue);
            }
            var root = new Value();
            root.Add("Targets", targets);
            
            return root;
        }
    }
}