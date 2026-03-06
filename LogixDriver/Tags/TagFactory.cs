using Logix.Driver;
using libplctag;

namespace Logix.Tags
{
    public interface ITagFactory
    {
        Tag Create(string name, int elementCount = 1);
    }

    public class TagFactory : ITagFactory
    {
        private readonly Target target;

        public TagFactory(Target target) => this.target = target;

        public Tag Create(string name, int elementCount = 1) => new Tag
        {
            Gateway = target.Gateway,
            Path = target.Path,
            PlcType = target.PlcType,
            Protocol = Protocol.ab_eip,
            Name = name,
            ElementCount = elementCount,
            Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
        };
    }
}
