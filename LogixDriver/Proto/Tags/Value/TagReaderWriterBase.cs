using libplctag;

namespace Logix.Proto
{
    public class TagReaderWriterBase
    {
        public Target Target { get; }
        private readonly ITagCache tagCache;

        public TagReaderWriterBase(Target target, ITagCache cache)
        { 
            this.Target = target;
            this.tagCache = cache;
        }

        protected Tag GetTag(string tagPath, int elementCount = 1, bool cache = true)
        {
            Tag tag;

            if (!tagCache.TryGetTag(tagPath, out tag!))
            {
                tag = new Tag
                {
                    Gateway = Target.Gateway,
                    Path = Target.Path,
                    PlcType = Target.PlcType,
                    Protocol = Protocol.ab_eip,
                    Name = tagPath,
                    ElementCount = elementCount,
                    Timeout = TimeSpan.FromMilliseconds(Target.TimeoutMs)
                };
            }

            if (cache)
                tagCache.AddTag(tagPath, tag);

            return tag;
        }
    }
}
