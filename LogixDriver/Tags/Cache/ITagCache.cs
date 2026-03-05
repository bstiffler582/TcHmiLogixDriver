using libplctag;

namespace Logix.Tags
{
    public interface ITagCache
    {
        public void AddTag(string tagPath, Tag tag);
        public bool TryGetTag(string tagPath, out Tag? tag);
    }
}
