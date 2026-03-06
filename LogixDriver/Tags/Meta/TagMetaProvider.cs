using Logix.Driver;
using static Logix.Tags.TagMetaHelpers;

namespace Logix.Tags
{
    public interface ITagMetaProvider
    {
        public Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null);
        public IEnumerable<TagDefinition> LoadTagDefinitions(IEnumerable<string>? tagNames = null);
        public Task<TagDefinition> LoadTagDefinitionAsync(string tagName);
        public TagDefinition LoadTagDefinition(string tagName);

        bool TryGetTagDefinition(string tagPath, out TagDefinition? definition);
        IEnumerable<TagDefinition> GetTagDefinitions();
        IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat();
    }

    internal class TagMetaProvider : ITagMetaProvider
    {
        private readonly ITagMetaDecoder metaDecoder;
        private readonly ITagValueReader reader;
        private readonly ITagDefinitionExpander definitionExpander;
        private readonly ITagDefinitionCache cache;

        public TagMetaProvider(ITagFactory tagFactory)
          : this(new TagValueReader(tagFactory), new TagDefinitionCache())
        { }

        public TagMetaProvider(
            ITagValueReader reader, 
            ITagDefinitionCache tagCache,
            ITagMetaDecoder? metaDecoder = null,
            ITagDefinitionExpander? definitionExpander = null)
        {
            this.reader = reader;
            this.cache = tagCache;
            this.metaDecoder = metaDecoder ?? new TagMetaDecoder();
            this.definitionExpander = definitionExpander ?? new TagDefinitionExpander(reader, tagCache, metaDecoder);
        }

        public async Task<IEnumerable<TagDefinition>> LoadTagDefinitionsAsync(IEnumerable<string>? tagNames = null)
        {
            IEnumerable<TagDefinition>? tagDefinitions = await ReadAndFilterBaseTags();

            // selective expansion
            if (tagNames is not null && tagNames.Any())
            {
                foreach (var tagName in tagNames)
                    await LoadTagDefinitionAsync(tagName, tagDefinitions);
            }
            else
            {
                foreach (var tag in tagDefinitions!)
                {
                    await definitionExpander!.ExpandTagDefinitionAsync(tag, true);
                    cache.AddTagDefinition(tag);
                }
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
            return metaDecoder.DecodeTagList(tag!)
                .Where(tag => tag.Name.StartsWith("Program:") || !IsSystem(tag.TypeCode));
        }

        public bool TryGetTagDefinition(string tagPath, out TagDefinition? definition)
        {
            return cache.TryGetTagDefinition(tagPath, out definition);
        }

        public IEnumerable<TagDefinition> GetTagDefinitions()
        {
            return cache.GetTagDefinitions();
        }

        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat()
        {
            return cache.GetTagDefinitionsFlat();
        }
    }
}
