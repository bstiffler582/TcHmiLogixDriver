using System.Collections.Generic;

namespace TcHmiLogixDriver.Logix
{
    record TargetConfig(string targetAddress, string targetSlot, int timeout = 1000);
    class LogixDriverConfig
    {
        public Dictionary<string, TargetConfig> Targets { get; set; }
    }
}
