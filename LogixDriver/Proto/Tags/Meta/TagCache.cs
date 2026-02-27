using libplctag;

namespace Logix.Proto
{
    public class TagCache : ITagCache
    {
        private readonly Dictionary<string, TagDefinition> tagDefinitions = new();
        private readonly Dictionary<string, TagDefinition> tagDefinitionsFlat = new();
        private readonly Dictionary<ushort, TypeDefinition> typeDefinitionCache = new();
        private readonly Dictionary<string, Tag> tagCache = new();

        public void AddTagDefinition(TagDefinition tagDefinition)
        {
            tagDefinitions.TryAdd(tagDefinition.Name, tagDefinition);

            var flattened = Flatten(tagDefinition);
            foreach (var t in flattened)
                tagDefinitionsFlat.TryAdd(t.Path, t.TagDef);
        }

        public IEnumerable<TagDefinition> GetTagDefinitions(bool flattened = false)
        {
            if (flattened)
                return tagDefinitionsFlat.Values;
            else
                return tagDefinitions.Values;
        }

        public bool TryGetTagDefinition(string tagName, out TagDefinition? tagDefinition)
        {
            return tagDefinitionsFlat.TryGetValue(tagName, out tagDefinition);
        }

        public void AddTypeDefinition(ushort code, TypeDefinition typeDefinition)
        {
            typeDefinitionCache.TryAdd(code, typeDefinition);
        }

        public bool TryGetTypeDefinition(ushort code, out TypeDefinition? typeDefinition)
        {
            return typeDefinitionCache.TryGetValue(code, out typeDefinition);
        }

        private IEnumerable<(string Path, TagDefinition TagDef)> Flatten(TagDefinition node, string parentPath = "")
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

        public void AddTag(string tagPath, Tag tag)
        {
            tagCache.TryAdd(tagPath, tag);
        }

        public bool TryGetTag(string tagPath, out Tag? tag)
        {
            return tagCache.TryGetValue(tagPath, out tag);
        }
    }
}
