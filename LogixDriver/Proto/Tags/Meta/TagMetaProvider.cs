namespace Logix.Proto
{
    public class TagMetaProvider : ITagMetaProvider
    {
        private readonly ITagMetaDecoder decoder;
        private readonly ITagValueReader reader;
        private readonly ITagDefinitionExpander definitionExpander;

        public TagMetaProvider(ITagValueReader reader, ITagDefinitionCache tagCache)
        {
            this.reader = reader;
            definitionExpander = new TagDefinitionExpander(reader, tagCache);
            decoder = new TagMetaDecoder();
        }

        public async Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null, bool deep = true)
        {
            IEnumerable<TagDefinition>? tagDefinitions = await ReadAndFilterBaseTags();

            // TODO: selective expansion
            if (tagNames is null)
            {

            }

            if (deep)
            {
                foreach (var tag in tagDefinitions!)
                    await definitionExpander!.ExpandTagDefinitionAsync(tag, true);
            }

            return tagDefinitions;
        }

        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null, bool deep = true)
        {
            return LoadTagDefinitionsAsync(tagNames, deep).GetAwaiter().GetResult();
        }

        public Task<TagDefinition> LoadTagDefinitionAsync(string tagName, bool deep = true)
        {
            throw new NotImplementedException();
        }

        public TagDefinition LoadTagDefinition(string tagName, bool deep = true)
        {
            throw new NotImplementedException();
        }

        private async Task<IEnumerable<TagDefinition>> ReadAndFilterBaseTags()
        {
            var tag = await reader.ReadTagAsync("@tags");
            return decoder.DecodeControllerTags(tag!)
                .Where(tag => tag.Name.StartsWith("Program:") || !LogixTypes.IsSystem(tag.TypeCode));
        }
    }
}
