using Logix.Driver;
using libplctag;

namespace Logix.Tags
{
    internal class QueuedTagValueReader : ITagValueReader
    {
        private readonly ITagFactory tagFactory;
        private readonly ITagReadWriteQueue queue;

        public QueuedTagValueReader(ITagFactory tagFactory, ITagReadWriteQueue queue)
        {
            this.tagFactory = tagFactory;
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
            var tag = tagFactory.Create(tagName, elementCount);
            return await queue.EnqueueReadAsync(tag);
        }

        public Tag ReadTag(string tagName, int elementCount = 1)
        {
            var tag = tagFactory.Create(tagName, elementCount);
            return queue.EnqueueReadSync(tag);
        }
    }
}
