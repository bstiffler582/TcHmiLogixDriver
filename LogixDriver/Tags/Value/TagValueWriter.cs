using libplctag;

namespace Logix.Tags
{
    public interface ITagValueWriter
    {
        public Task<Tag> WriteTagAsync(Tag tag);
        public Tag WriteTag(Tag tag);
        public Task<Tag> InitializeAsync(Tag tag);
        public Tag Initialize(Tag tag);
    }

    internal class TagValueWriter : ITagValueWriter
    {
        public Tag Initialize(Tag tag)
        {
            if (!tag.IsInitialized)
                tag.Initialize();
            return tag;
        }

        public async Task<Tag> InitializeAsync(Tag tag)
        {
            if (!tag.IsInitialized)
                await tag.InitializeAsync();

            return tag;
        }

        public Tag WriteTag(Tag tag)
        {
            tag.Write();
            return tag;
        }

        public async Task<Tag> WriteTagAsync(Tag tag)
        {
            await tag.WriteAsync();
            return tag;
        }
    }
}
