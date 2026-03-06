using libplctag;

namespace Logix.Tags
{
    public interface ITagCache
    {
        public void AddTag(string tagPath, Tag tag);
        public bool TryGetTag(string tagPath, out Tag? tag);
    }

    internal class TagCache : ITagCache
    {
        private readonly Dictionary<string, Tag> tagCache = new();

        public void AddTag(string tagPath, Tag tag)
        {
            tagCache.TryAdd(tagPath, tag);
        }

        public bool TryGetTag(string tagPath, out Tag? tag)
        {
            return tagCache.TryGetValue(tagPath, out tag);
        }
    }
}
