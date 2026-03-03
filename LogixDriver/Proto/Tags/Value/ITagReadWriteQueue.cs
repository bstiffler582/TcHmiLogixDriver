using libplctag;

namespace Logix.Proto
{
    public interface ITagReadWriteQueue
    {
        public Task<Tag> EnqueueReadAsync(Tag tag);
        public Tag EnqueueReadSync(Tag tag);
        public Task<Tag> EnqueueWriteAsync(Tag tag);
        Tag EnqueueWriteSync(Tag tag);
    }
}
