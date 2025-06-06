using libplctag;
using System.Text.Json;
using System.IO;

namespace ConsoleTest
{
    public class LogixTarget
    {
        public string Name { get; }
        public string Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public Protocol Protocol { get; }

        private Dictionary<ushort, TypeDefinition> udtIdMap = new Dictionary<ushort, TypeDefinition>();
        private Dictionary<string, TagDefinition> tagMap = new Dictionary<string, TagDefinition>();

        public LogixTarget(string Name, string Gateway, string Path, PlcType PlcType, Protocol Protocol)
        {
            this.Name = Name;
            this.Gateway = Gateway;
            this.Path = Path;
            this.PlcType = PlcType;
            this.Protocol = Protocol;
        }

        public void AddTag(TagDefinition tag)
        {
            tagMap.Add(tag.Name, tag);

            if (tag.Type.Members?.Count > 0)
                GetChildTags(tag, tag.Name, tagMap);
        }

        private void GetChildTags(TagDefinition parent, string path, Dictionary<string, TagDefinition> children)
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
                    GetChildTags(m, name, children);
            }
        }

        public void Debug()
        {
            var tags = JsonSerializer.Serialize(tagMap);
            File.WriteAllText("tags.json", tags);
            var udts = JsonSerializer.Serialize(udtIdMap);
            File.WriteAllText("udts.json", udts);
        }

        public void AddUdtDef(ushort Id, TypeDefinition udtTag)
        {
            udtIdMap.TryAdd(Id, udtTag);
        }

        public bool TryGetUdtDef(ushort Id, out TypeDefinition? udtDef)
        {
            if (udtIdMap.TryGetValue(Id, out TypeDefinition? def))
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
