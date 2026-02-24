using libplctag;

namespace Logix
{
    public class LogixTarget
    {
        public string Name { get; }
        public string Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public Protocol Protocol { get; }
        public int TimeoutMs { get; set; } = 5000;

        // map of tag definitions
        private readonly Dictionary<string, TagDefinition> tagDefinitions = new();
        private readonly Dictionary<string, TagDefinition> tagDefinitionsFlat = new();

        // type definition cache
        private readonly Dictionary<ushort, TypeDefinition> typeDefinitionCache = new();

        public IReadOnlyDictionary<string, TagDefinition> TagDefinitions => tagDefinitions;
        public IReadOnlyDictionary<string, TagDefinition> TagDefinitionsFlat => tagDefinitionsFlat;

        public LogixTarget(
            string Name, 
            string Gateway, 
            string Path = "1,0", 
            PlcType PlcType = PlcType.ControlLogix, 
            Protocol Protocol = Protocol.ab_eip, 
            int TimeoutMs = 5000)
        {
            this.Name = Name;
            this.Gateway = Gateway;
            this.Path = Path;
            this.PlcType = PlcType;
            this.Protocol = Protocol;
            this.TimeoutMs = TimeoutMs;
        }

        public bool TryGetTagDefinition(string name, out TagDefinition? tagDefinition) => 
            tagDefinitionsFlat.TryGetValue(name, out tagDefinition);

        public void AddTagDefinition(TagDefinition tag)
        {
            tagDefinitions.TryAdd(tag.Name, tag);

            var flattened = Flatten(tag);
            foreach (var t in flattened)
                tagDefinitionsFlat.TryAdd(t.Path, t.TagDef);
        }

        public void AddTagDefinition(IEnumerable<TagDefinition> tags)
        {
            foreach (var tag in tags)
                AddTagDefinition(tag);
        }

        public void CacheTypeDefinition(ushort typeId, TypeDefinition type)
        {
            typeDefinitionCache.TryAdd(typeId, type);
        }

        public bool TryGetCachedTypeDefinition(ushort typeId, out TypeDefinition? definition)
        {
            if (typeDefinitionCache.TryGetValue(typeId, out definition))
                return true;
            else return false;
        }

        private static IEnumerable<(string Path, TagDefinition TagDef)> Flatten(TagDefinition node, string parentPath = "")
        {
            string fullPath;
            if (int.TryParse(node.Name, out var idx) && !string.IsNullOrEmpty(parentPath))
                fullPath = $"{parentPath}[{idx}]";
            else
                fullPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}.{node.Name}";

            yield return (fullPath, node);

            if (node.TypeName.Contains("STRING"))
                yield break;

            if (node.Children is not null)
            {
                foreach (var child in node.Children.SelectMany(c => Flatten(c, fullPath)))
                    yield return child;
            }
        }
    }
}
