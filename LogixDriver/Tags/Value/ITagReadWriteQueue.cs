using libplctag;

namespace Logix.Tags
{
    public interface ITagReadWriteQueue : IDisposable
    {
        public Task<Tag> EnqueueReadAsync(Tag tag);
        public Tag EnqueueReadSync(Tag tag);
        public Task<Tag> EnqueueInitializeAsync(Tag tag);
        public Tag EnqueueInitializeSync(Tag tag);
        public Task<Tag> EnqueueWriteAsync(Tag tag);
        public Tag EnqueueWriteSync(Tag tag);
    }
}
