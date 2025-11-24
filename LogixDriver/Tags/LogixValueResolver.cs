using libplctag;
using static Logix.LogixTypes;

namespace Logix.Tags
{
    public interface ILogixValueResolver 
    {
        Type ValueType { get; }
        object ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
    }
    public interface ILogixValueResolver<T> : ILogixValueResolver
    {
        new T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
    }

    public abstract class LogixValueResolverBase<T> : ILogixValueResolver<T>
    {
        public Type ValueType => typeof(T);
        public abstract T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        object ILogixValueResolver.ResolveValue(Tag tag, TagDefinition definition, int offset)
            => ResolveValue(tag, definition, offset) ?? default!;

        protected object PrimitiveValueResolver(Tag tag, ushort typeCode, int offset = 0)
        {
            return (Code)(typeCode) switch
            {
                Code.BOOL => tag.GetBit(offset),
                Code.SINT or Code.USINT => tag.GetInt8(offset),
                Code.INT or Code.UINT => tag.GetInt16(offset),
                Code.DINT or Code.UDINT => tag.GetInt32(offset),
                Code.LINT or Code.ULINT => tag.GetInt64(offset),
                Code.REAL => tag.GetFloat32(offset),
                Code.LREAL => tag.GetFloat64(offset),
                Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                    => tag.GetString(offset),
                _ => throw new Exception($"Primitive type code:{typeCode:X} not handled")
            };
        }
    }

    public class LogixDefaultValueResolver : LogixValueResolverBase<object>
    {
        public override object ResolveValue(Tag tag, TagDefinition definition, int offset = 0)
        {
            if (LogixTypes.IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new List<object>();
                foreach (var m in definition.Type.Members)
                    ret.Add(ResolveValue(tag, m, offset + (int)m.Offset));

                return ret;
            }
            else if (LogixTypes.IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new Dictionary<string, object>();
                foreach (var m in definition.Type.Members)
                    ret[m.Name] = ResolveValue(tag, m, offset + (int)m.Offset);

                return ret;
            }
            else
            {
                return PrimitiveValueResolver(tag, definition.Type.Code, offset);
            }
        }
    }
}