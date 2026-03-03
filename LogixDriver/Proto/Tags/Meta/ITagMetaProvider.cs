namespace Logix.Proto
{
    public interface ITagMetaProvider
    {
        public Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null, bool deep = true);
        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null, bool deep = true);
        public Task<TagDefinition> LoadTagDefinitionAsync(string tagName, bool deep = true);
        public TagDefinition LoadTagDefinition(string tagName, bool deep = true);
    }
}
