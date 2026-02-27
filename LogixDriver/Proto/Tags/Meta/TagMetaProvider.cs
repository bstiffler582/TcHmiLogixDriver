namespace Logix.Proto
{
    public class TagMetaProvider : ITagMetaProvider
    {
        private ITagMetaDecoder? decoder;
        private ITagValueReaderWriter? reader;
        private ITagDefinitionExpander? definitionExpander;

        public TagMetaProvider(ITagValueReaderWriter readerWriter)
        {
            this.reader = readerWriter;
            decoder = new TagMetaDecoder();
        }

        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null, bool deep = true)
        {
            IEnumerable<TagDefinition>? tagDefinitions = ReadAndFilterBaseTags();

            // selective expansion
            if (tagNames is null)
            {

            }

            if (deep)
            {
                foreach (var tag in tagDefinitions!)
                    definitionExpander!.ExpandTagDefinition(tag, true);
            }

            return tagDefinitions ?? Enumerable.Empty<TagDefinition>();
        }

        public void ExpandTagDefinition(TagDefinition tagDefinition, ITagValueReaderWriter reader)
        {
            throw new NotImplementedException();
        }

        public TagDefinition LoadTagDefinition(string tagName, bool deep = true)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<TagDefinition>? ReadAndFilterBaseTags()
        {
            var tag = reader?.ReadTag("@tags");
            return decoder?.DecodeControllerTags(tag!)
                .Where(tag => tag.Name.StartsWith("Program:") || !LogixTypes.IsSystem(tag.TypeCode));
        }
    }
}
