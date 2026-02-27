using libplctag;

namespace Logix.Proto
{
    public interface ITagMetaDecoder
    {
        public TagDefinition DecodeTag(Tag tag, int offset, out int elementSize);
        public TypeDefinition DecodeUdt(Tag tag);
        public IEnumerable<TagDefinition> DecodeControllerTags(Tag tag);
        public IEnumerable<TagDefinition> DecodeProgramTags(Tag tag);
        public string DecodeControllerInfo(Tag tag);
    }
}
