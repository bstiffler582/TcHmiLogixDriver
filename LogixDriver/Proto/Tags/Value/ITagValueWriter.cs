using libplctag;

namespace Logix.Proto
{
    public interface ITagValueWriter
    {
        public Task<Tag> WriteTagAsync(Tag tag);
        public Tag WriteTag(Tag tag);
        public Task<Tag> InitializeAsync(Tag tag);
        public Tag Initialize(Tag tag);
    }
}
