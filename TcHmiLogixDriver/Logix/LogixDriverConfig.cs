using System.Collections.Generic;

namespace TcHmiLogixDriver.Logix
{
    record TargetConfig(string targetAddress, string targetSlot, int timeout = 1000, string[]? tagSelector = null);
    class LogixDriverConfig
    {
        public LogixDriverConfig(Dictionary<string, TargetConfig>? targets = null)
        {
            Targets = targets ?? new Dictionary<string, TargetConfig>();
        }

        public Dictionary<string, TargetConfig> Targets { get; }
    }
}
