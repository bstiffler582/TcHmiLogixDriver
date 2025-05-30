using libplctag;
using static ConsoleTest.LogixDriver;

namespace ConsoleTest
{
    public class LogixTarget
    {
        public string Name { get; }
        public string Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public Protocol Protocol { get; }

        private Dictionary<ushort, TypeDef> udtIdMap = new Dictionary<ushort, TypeDef>();

        public LogixTarget(string Name, string Gateway, string Path, PlcType PlcType, Protocol Protocol)
        {
            this.Name = Name;
            this.Gateway = Gateway;
            this.Path = Path;
            this.PlcType = PlcType;
            this.Protocol = Protocol;
        }

        public void AddUdtDef(ushort Id, TypeDef udtTag)
        {
            udtIdMap.TryAdd(Id, udtTag);
        }

        public bool TryGetUdtDef(ushort Id, out TypeDef? udtDef)
        {
            if (udtIdMap.TryGetValue(Id, out TypeDef? def))
            {
                udtDef = def;
                return true;
            }
            else
            {
                udtDef = null;
                return false;
            }
        }
    }
}
