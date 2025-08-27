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
        public int TimeoutMs { get; set; } = 5000;

        // map of tag definitions
        private readonly Dictionary<string, TagDefinition> tagDefinitions = new();
        public IDictionary<string, TagDefinition> TagDefinitions => tagDefinitions;

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

        bool TryGetTagDefinition(string name, out TagDefinition? tag) => tagDefinitions.TryGetValue(name, out tag);

        // flatten so we don't have to do this
        public TagDefinition? GetTagDefinitionByName(string name)
        {
            var pathArray = name.Split('.');
            var definition = TryGetTagDefinition(pathArray[0], out var def) ? def : null;

            if (definition is null) return null;

            // is root tag
            if (pathArray[0] == name && pathArray.Length == 1)
                return definition;

            // resolve path
            foreach (var path in pathArray.Skip(1))
            {
                if (definition?.Type.Members is null) return null;
                definition = definition.Type.Members.FirstOrDefault(t => t.Name == path);
            }

            return definition;
        }

        public void AddTagDefinition(TagDefinition tag)
        {
            //tagDefinitions.TryAdd(tag.Name, tag);
            var flattened = Flatten(tag);
            foreach (var t in flattened)
                tagDefinitions.TryAdd(t.Path, t.TagDef);

                //.ToDictionary(x => x.Path, x => x.Node);
            // Flatten into dictionary directly:
            //var dict = root.Flatten().ToDictionary(x => x.Path, x => x.Node);

            //if (tag.Type.Members?.Count > 0)
            //{
            //    AddChildTags(tag, tag.Name, tags);
            //    //var children = tag.Type.Members?.SelectMany(m =>
            //    //{
            //    //    return m.Type.Members;
            //    //});
            //} 
        }

        public void AddTagDefinition(IEnumerable<TagDefinition> tagList)
        {
            foreach (var tag in tagList)
            {
                var flattened = Flatten(tag).ToList();
                foreach (var t in flattened)
                    tagDefinitions.TryAdd(t.Path, t.TagDef);
            }
        }

        private static IEnumerable<(string Path, TagDefinition TagDef)> Flatten(TagDefinition node, string parentPath = "")
        {
            string fullPath;
            if (int.TryParse(node.Name, out var idx) && !string.IsNullOrEmpty(parentPath))
                fullPath = $"{parentPath}[{idx}]";
            else
                fullPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}.{node.Name}";

            // yield the current node
            yield return (fullPath, node);

            if (node.Type.Name.Contains("STRING"))
                yield break;

            // recurse into children
            if (node.Type.Members is not null)
            {
                foreach (var child in node.Type.Members.SelectMany(c => Flatten(c, fullPath)))
                    yield return child;
            }
            
        }

        //private void AddChildTags(TagDefinition parent, string path, Dictionary<string, TagDefinition> children)
        //{
        //    if (parent.Type.Members is null) return;

        //    foreach (var m in parent.Type.Members)
        //    {
        //        string name;
        //        if (parent.Type.Name.Contains("ARRAY"))
        //            name = $"{path}[{m.Name}]";
        //        else
        //            name = $"{path}.{m.Name}";

        //        children.Add(name, m);
        //        if (m.Type.Members?.Count > 0 && m.Type.Name != "STRING")
        //            AddChildTags(m, name, children);
        //    }
        //}

        //public void Debug()
        //{
        //    var tags = JsonSerializer.Serialize(tagMap);
        //    File.WriteAllText("tags.json", tags);
        //    var udts = JsonSerializer.Serialize(udtIdMap);
        //    File.WriteAllText("udts.json", udts);
        //}
    }
}
