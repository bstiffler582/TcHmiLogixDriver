using libplctag;

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
        private Dictionary<string, TagDef> tagMap = new Dictionary<string, TagDef>();

        public LogixTarget(string Name, string Gateway, string Path, PlcType PlcType, Protocol Protocol)
        {
            this.Name = Name;
            this.Gateway = Gateway;
            this.Path = Path;
            this.PlcType = PlcType;
            this.Protocol = Protocol;
        }

        public void AddTag(TagDef tag, string programName = "")
        {
            string path = (string.IsNullOrEmpty(programName)) ? 
                tag.Name : $"{programName}::{tag.Name}";

            tagMap.Add(path, tag);

            if (tag.Type.Members?.Count > 0)
                getChildTags(tag, path, tagMap);
        }

        private void getChildTags(TagDef parent, string path, Dictionary<string, TagDef> children)
        {
            if (parent.Type.Members is null) return;

            foreach (var m in parent.Type.Members)
            {
                string name;
                if (parent.Type.Name.Contains("ARRAY"))
                    name = $"{path}[{m.Name}]";
                else
                    name = $"{path}.{m.Name}";

                children.Add(name, m);
                if (m.Type.Members?.Count > 0 && m.Type.Name != "STRING")
                    getChildTags(m, name, children);
            }
        }

        public void Debug()
        {
            Console.WriteLine("Debug...");
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
