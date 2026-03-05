namespace Logix.Tags
{
    public interface ITagMetaProvider
    {
        public Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null);
        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null);
        public Task<TagDefinition> LoadTagDefinitionAsync(string tagName);
        public TagDefinition LoadTagDefinition(string tagName);
    }
}
