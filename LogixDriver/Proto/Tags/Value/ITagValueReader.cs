using libplctag;

namespace Logix.Proto
{
    public interface ITagValueReader
    {
        public Task<Tag> ReadTagAsync(Tag tag);
        public Tag ReadTag(Tag tag);
        public Task<Tag> ReadTagAsync(string tagName, int elementCount = 1);
        public Tag ReadTag(string tagName, int elementCount = 1);
    }
}
