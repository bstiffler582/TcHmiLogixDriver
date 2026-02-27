using libplctag;

namespace Logix.Proto
{
    public interface ITagCache
    {
        public void AddTagDefinition(TagDefinition tagDefinition);
        public bool TryGetTagDefinition(string tagPath, out TagDefinition? tagDefinition);
        public IEnumerable<TagDefinition> GetTagDefinitions(bool flattened = false);
        public void AddTypeDefinition(ushort code, TypeDefinition typeDefinition);
        public bool TryGetTypeDefinition(ushort code, out TypeDefinition? typeDefinition);
        public void AddTag(string tagPath, Tag tag);
        public bool TryGetTag(string tagPath, out Tag? tag);
    }
}
