using System.Collections.Generic;

namespace TcHmiLogixDriver.Logix
{
    record TargetDiagnostics(string connectionState, string model, string firmware);
    class LogixDriverDiagnostics
    {
        public Dictionary<string, TargetDiagnostics> Targets { get; set; } = new Dictionary<string, TargetDiagnostics>();
    }
}