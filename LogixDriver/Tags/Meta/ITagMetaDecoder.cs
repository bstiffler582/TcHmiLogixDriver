using libplctag;

namespace Logix.Tags
{
    public interface ITagMetaDecoder
    {
        public TagDefinition DecodeTagMeta(Tag tag, int offset, out int elementSize);
        public TypeDefinition DecodeUdtMeta(Tag tag);
        public IEnumerable<TagDefinition> DecodeControllerTags(Tag tag);
        public IEnumerable<TagDefinition> DecodeProgramTags(Tag tag);
        public string DecodeControllerInfo(Tag tag);
    }
}
