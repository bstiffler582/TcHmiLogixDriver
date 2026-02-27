using libplctag;

namespace Logix.Proto
{
    public interface ITagValueReaderWriter
    {
        public object ReadTagValue(string tagName);
        public Task<object> ReadTagValueAsync(string tagName);
        public bool WriteTagValue(string tagName, object value);
        public Task WriteTagValueAsync(string tagName, object value);
        public Task<Tag?> ReadTagAsync(string tagName);
        public Tag? ReadTag(string tagName);
        public Task<Tag?> WriteTagAsync(string tagName, object value);
        public Tag? WriteTag(string tagName, object value);
    }
}
