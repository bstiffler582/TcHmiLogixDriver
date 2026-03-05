using libplctag;

namespace Logix.Tags
{
    public class QueuedTagValueWriter : ITagValueWriter
    {
        private readonly ITagReadWriteQueue queue;

        public QueuedTagValueWriter(ITagReadWriteQueue queue)
        {
            this.queue = queue;
        }

        public Tag WriteTag(Tag tag)
        {
            return queue.EnqueueWriteSync(tag);
        }

        public async Task<Tag> WriteTagAsync(Tag tag)
        {
            return await queue.EnqueueWriteAsync(tag);
        }

        public Tag Initialize(Tag tag)
        {
            return queue.EnqueueInitializeSync(tag);
        }

        public async Task<Tag> InitializeAsync(Tag tag)
        {
            return await queue.EnqueueInitializeAsync(tag);
        }
    }
}
