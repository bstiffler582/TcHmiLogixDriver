namespace Logix.Tags
{
    public interface ITagValueChannelFactory
    {
        ITagValueChannel Open(ITagFactory tagFactory, uint intervalMs);
    }

    public class TagValueChannelFactory : ITagValueChannelFactory
    {
        public ITagValueChannel Open(ITagFactory tagFactory, uint intervalMs)
        {
            var queue = new TagReadWriteQueue(intervalMs);
            return new TagValueChannel(
                new QueuedTagValueReader(tagFactory, queue),
                new QueuedTagValueWriter(queue),
                queue);
        }
    }
}
