namespace Logix.Proto
{
    public interface ITagDefinitionCache
    {
        public void AddTagDefinition(TagDefinition tagDefinition);
        public bool TryGetTagDefinition(string tagPath, out TagDefinition? tagDefinition);
        public IEnumerable<TagDefinition> GetTagDefinitions();
        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat();
        public void AddTypeDefinition(ushort code, TypeDefinition typeDefinition);
        public bool TryGetTypeDefinition(ushort code, out TypeDefinition? typeDefinition);
    }
}
