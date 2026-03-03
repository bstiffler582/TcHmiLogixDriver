using libplctag;

namespace Logix.Proto
{
    public interface ITagValueWriter
    {
        public Task<Tag> WriteTagAsync(string tagName, object value);
        public Tag WriteTag(string tagName, object value);
    }
}
