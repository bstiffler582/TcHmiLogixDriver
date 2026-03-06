using libplctag;

namespace Logix.Tags
{
    public interface ITagValueReader
    {
        public Task<Tag> ReadTagAsync(Tag tag);
        public Tag ReadTag(Tag tag);
        public Task<Tag> ReadTagAsync(string tagName, int elementCount = 1);
        public Tag ReadTag(string tagName, int elementCount = 1);
    }

    internal class TagValueReader : ITagValueReader
    {
        private readonly ITagFactory tagFactory;

        public TagValueReader(ITagFactory tagFactory) 
        { 
            this.tagFactory = tagFactory;
        }

        public async Task<Tag> ReadTagAsync(Tag tag)
        {
            await tag.ReadAsync();
            return tag;
        }

        public Tag ReadTag(Tag tag)
        {
            return ReadTagAsync(tag).GetAwaiter().GetResult();
        }

        public async Task<Tag> ReadTagAsync(string tagName, int elementCount = 1)
        {
            var tag = tagFactory.Create(tagName, elementCount);
            await tag.ReadAsync();
            return tag;
        }

        public Tag ReadTag(string tagName, int elementCount = 1)
        {
            return ReadTagAsync(tagName, elementCount).GetAwaiter().GetResult();
        }
    }
}
