namespace Logix.Proto
{
    public interface ITagMetaProvider
    {
        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null, bool deep = true);
        public TagDefinition LoadTagDefinition(string tagName, bool deep = true);
        public void ExpandTagDefinition(TagDefinition tagDefinition, ITagValueReaderWriter reader);
    }
}
