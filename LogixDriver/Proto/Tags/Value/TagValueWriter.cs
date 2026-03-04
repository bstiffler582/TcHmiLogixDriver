using libplctag;

namespace Logix.Proto
{
    public class TagValueWriter : ITagValueWriter
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
