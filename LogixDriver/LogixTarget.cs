using libplctag;

namespace LogixDriver
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
        public IDictionary<string, TagDefinition> TagDefinitions => tagDefinitions;
        public IDictionary<string, TagDefinition> TagDefinitionsFlat => tagDefinitionsFlat;

        // map of tags
        private readonly Dictionary<string, Tag> tags = new();

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

        public TagDefinition? TryGetTagDefinition(string name) => 
            tagDefinitionsFlat.TryGetValue(name, out var tag) ? tag : null;

        public void AddTagDefinition(TagDefinition tag)
        {
            tagDefinitions.Add(tag.Name, tag);
            var flattened = Flatten(tag);
            foreach (var t in flattened)
                tagDefinitionsFlat.TryAdd(t.Path, t.TagDef);
        }

        public void AddTagDefinition(IEnumerable<TagDefinition> tagList)
        {
            foreach (var tag in tagList)
            {
                tagDefinitions.Add(tag.Name, tag);
                var flattened = Flatten(tag).ToList();
                foreach (var t in flattened)
                    tagDefinitionsFlat.TryAdd(t.Path, t.TagDef);
            }
        }

        private static IEnumerable<(string Path, TagDefinition TagDef)> Flatten(TagDefinition node, string parentPath = "")
        {
            string fullPath;
            if (int.TryParse(node.Name, out var idx) && !string.IsNullOrEmpty(parentPath))
                fullPath = $"{parentPath}[{idx}]";
            else
                fullPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}.{node.Name}";

            yield return (fullPath, node);

            if (node.Type.Name.Contains("STRING"))
                yield break;

            if (node.Type.Members is not null)
            {
                foreach (var child in node.Type.Members.SelectMany(c => Flatten(c, fullPath)))
                    yield return child;
            }
            
        }

        //public void Debug()
        //{
        //    var tags = JsonSerializer.Serialize(tagMap);
        //    File.WriteAllText("tags.json", tags);
        //    var udts = JsonSerializer.Serialize(udtIdMap);
        //    File.WriteAllText("udts.json", udts);
        //}
    }
}
