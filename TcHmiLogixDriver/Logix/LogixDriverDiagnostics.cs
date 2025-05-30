using System.Collections.Generic;

namespace TcHmiLogixDriver.Logix
{
    class LogixDriverDiagnostics
    {
        public Dictionary<string, TargetDiagnostics> Targets { get; set; } = new Dictionary<string, TargetDiagnostics>();
    }
    record TargetDiagnostics(string connectionState);
}