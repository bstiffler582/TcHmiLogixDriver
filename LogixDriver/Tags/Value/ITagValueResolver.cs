using libplctag;

namespace Logix.Tags
{
    public interface ITagValueResolver
    {
        Type ValueType { get; }
        object ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        void WriteTagBuffer(Tag tag, TagDefinition definition, object value, int offset = 0);
    }
    public interface ITagValueResolver<T> : ITagValueResolver
    {
        new T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        void WriteTagBuffer(Tag tag, TagDefinition definition, T value, int offset = 0);
    }
}
