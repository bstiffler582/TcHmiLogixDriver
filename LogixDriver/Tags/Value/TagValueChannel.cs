using Logix.Tags;

namespace Logix.Tags
{
    public interface ITagValueChannel : IDisposable
    {
        ITagValueReader Reader { get; }
        ITagValueWriter Writer { get; }
    }

    internal sealed class TagValueChannel : ITagValueChannel
    {
        public ITagValueReader Reader { get; }
        public ITagValueWriter Writer { get; }
        private readonly ITagReadWriteQueue queue;

        public TagValueChannel(ITagValueReader reader, ITagValueWriter writer, ITagReadWriteQueue queue)
        {
            Reader = reader;
            Writer = writer;
            this.queue = queue;
        }

        public void Dispose() => queue.Dispose();
    }
}
