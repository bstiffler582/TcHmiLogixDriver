using Logix.Driver;
using libplctag;

namespace Logix.Tags
{
    public class TagValueReader : ITagValueReader
    {
        private readonly Target? target;

        public TagValueReader(Target? target) 
        { 
            this.target = target;
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
            var tag = CreateTag(tagName, elementCount);
            await tag.ReadAsync();
            return tag;
        }

        public Tag ReadTag(string tagName, int elementCount = 1)
        {
            return ReadTagAsync(tagName, elementCount).GetAwaiter().GetResult();
        }

        private Tag CreateTag(string tagName, int elementCount = 1)
        {
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            return new Tag
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = Protocol.ab_eip,
                Name = tagName,
                ElementCount = elementCount,
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
            };
        }
    }
}
