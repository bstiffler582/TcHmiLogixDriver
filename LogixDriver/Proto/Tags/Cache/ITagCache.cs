using libplctag;

namespace Logix.Proto
{
    public interface ITagCache
    {
        public void AddTag(string tagPath, Tag tag);
        public bool TryGetTag(string tagPath, out Tag? tag);
    }
}
