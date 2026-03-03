using libplctag;

namespace Logix.Proto
{
    public interface ITagValueReader
    {
        public Task<Tag> ReadTagAsync(string tagName);
        public Tag ReadTag(string tagName);
    }
}
