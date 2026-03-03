using libplctag;

namespace Logix.Proto
{
    public class TagValueReader : TagReaderWriterBase, ITagValueReader
    {
        private readonly ITagReadWriteQueue? readQueue;

        public TagValueReader(Target target, ITagCache tagCache, ITagReadWriteQueue? queue = null) : base(target, tagCache)
        {
            readQueue = queue;
        }

        public async Task<Tag> ReadTagAsync(string tagName)
        {
            var tag = GetTag(tagName);

            if (readQueue is null)
                await tag.ReadAsync();
            else
                await readQueue.EnqueueReadAsync(tag);

            return tag;
        }

        public Tag ReadTag(string tagName)
        {
            return ReadTagAsync(tagName).GetAwaiter().GetResult();
        }
    }
}
