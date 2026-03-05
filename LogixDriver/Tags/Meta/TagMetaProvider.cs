using static Logix.Tags.TagMetaHelpers;

namespace Logix.Tags
{
    public class TagMetaProvider : ITagMetaProvider
    {
        private readonly ITagMetaDecoder decoder;
        private readonly ITagValueReader reader;
        private readonly ITagDefinitionExpander definitionExpander;
        private readonly ITagDefinitionCache cache;

        public TagMetaProvider(ITagValueReader reader, ITagDefinitionCache tagCache)
        {
            this.reader = reader;
            this.cache = tagCache;
            definitionExpander = new TagDefinitionExpander(reader, tagCache);
            decoder = new TagMetaDecoder();
        }

        public async Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null)
        {
            IEnumerable<TagDefinition>? tagDefinitions = await ReadAndFilterBaseTags();

            // selective expansion
            if (tagNames is not null && tagNames.Count() > 0)
            {
                foreach (var tagName in tagNames)
                    await LoadTagDefinitionAsync(tagName, tagDefinitions);
            }
            else
            {
                foreach (var tag in tagDefinitions!)
                    await definitionExpander!.ExpandTagDefinitionAsync(tag, true);
            }

                return tagDefinitions;
        }

        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null)
        {
            return LoadTagDefinitionsAsync(tagNames).GetAwaiter().GetResult();
        }

        public async Task<TagDefinition> LoadTagDefinitionAsync(string tagName)
        {
            var loadedDefinitions = cache.GetTagDefinitions();
            if (loadedDefinitions.Count() < 1)
            {
                var baseTags = await ReadAndFilterBaseTags();
                foreach (var tag in baseTags)
                    cache.AddTagDefinition(tag);
            }

            return await LoadTagDefinitionAsync(tagName, loadedDefinitions);
        }

        public TagDefinition LoadTagDefinition(string tagName)
        {
            return LoadTagDefinitionAsync(tagName).GetAwaiter().GetResult();
        }

        private async Task<TagDefinition> LoadTagDefinitionAsync(string tagName, IEnumerable<TagDefinition> loadedDefinitions)
        {
            var pathParts = tagName
                .Replace('[', '.')
                .Replace(']', '.')
                .Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 1)
                throw new ArgumentException("Invalid tag name");

            var root = loadedDefinitions.FirstOrDefault(d => d.Name == pathParts[0]);

            if (root is null)
                throw new Exception($"Root tag {pathParts[0]} not found.");

            var pathQueue = new Queue<string>(pathParts.Skip(1));

            TagDefinition tag = root;
            while (pathQueue.Count > 0)
            {
                var memberName = pathQueue.Dequeue();
                if (tag?.ExpansionLevel < ExpansionLevel.Shallow)
                    await definitionExpander.ExpandTagDefinitionAsync(tag, false);
                var member = tag?.Children!.FirstOrDefault(c => c.Name == memberName);
                tag = member!;
            }

            if (!tag.IsPrimitive && tag.ExpansionLevel < ExpansionLevel.Deep)
                await definitionExpander.ExpandTagDefinitionAsync(tag, true);

            // re-add root to flatten expanded children
            cache.AddTagDefinition(root);

            return new TagDefinition(tag) { Name = tagName };
        }

        private async Task<IEnumerable<TagDefinition>> ReadAndFilterBaseTags()
        {
            var tag = await reader.ReadTagAsync("@tags");
            return decoder.DecodeControllerTags(tag!)
                .Where(tag => tag.Name.StartsWith("Program:") || !IsSystem(tag.TypeCode));
        }
    }
}
