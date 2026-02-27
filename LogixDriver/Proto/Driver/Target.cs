using libplctag;

namespace Logix.Proto
{
    public class Target
    {
        public string Name { get; }
        public string Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public int TimeoutMs { get; set; } = 5000;

        public Target(
            string name,
            string gateway,
            string path = "1,0",
            PlcType plcType = PlcType.ControlLogix,
            int timeoutMs = 5000)
        {
            Name = name;
            Gateway = gateway;
            Path = path;
            PlcType = plcType;
            TimeoutMs = timeoutMs;
        }
    }
}
