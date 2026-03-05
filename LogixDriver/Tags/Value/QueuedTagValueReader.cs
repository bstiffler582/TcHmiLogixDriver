using Logix.Driver;
using libplctag;

namespace Logix.Tags
{
    internal class QueuedTagValueReader : ITagValueReader
    {
        private readonly Target? target;
        private readonly ITagReadWriteQueue queue;

        public QueuedTagValueReader(Target? target, ITagReadWriteQueue queue)
        {
            this.target = target;
            this.queue = queue;
        }

        public async Task<Tag> ReadTagAsync(Tag tag)
        {
            return await queue.EnqueueReadAsync(tag);
        }

        public Tag ReadTag(Tag tag)
        {
            return ReadTagAsync(tag).GetAwaiter().GetResult();
        }

        public async Task<Tag> ReadTagAsync(string tagName, int elementCount = 1)
        {
            var tag = CreateTag(tagName, elementCount);
            return await queue.EnqueueReadAsync(tag);
        }

        public Tag ReadTag(string tagName, int elementCount = 1)
        {
            var tag = CreateTag(tagName, elementCount);
            return queue.EnqueueReadSync(tag);
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
