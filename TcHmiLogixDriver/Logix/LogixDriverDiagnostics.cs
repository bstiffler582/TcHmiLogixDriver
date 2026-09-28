using System.Collections.Concurrent;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    record TargetDiagnostics(bool isConnected = false, string controllerInfo = "");
    class LogixDriverDiagnostics
    {
        // updated from driver connection events (monitor threads) while requests enumerate it
        public ConcurrentDictionary<string, TargetDiagnostics> Targets { get; } = new();

        public Value ToValue()
        {
            var targets = new Value();

            foreach (var target in Targets)
            {
                var targetValue = new Value();
                targetValue.Add("isConnected", target.Value.isConnected);
                targetValue.Add("controllerInfo", target.Value.controllerInfo);

                targets.Add(target.Key, targetValue);
            }
            var root = new Value();
            root.Add("Targets", targets);

            return root;
        }
    }
}
