using libplctag;
using System.Net.WebSockets;
using static Logix.LogixTypes;

namespace Logix.Tags
{
    public interface ILogixValueResolver 
    {
        Type ValueType { get; }
        object ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        void WriteTagBuffer(Tag tag, TagDefinition definition, object value, int offset = 0);
    }
    public interface ILogixValueResolver<T> : ILogixValueResolver
    {
        new T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        void WriteTagBuffer(Tag tag, TagDefinition definition, T value, int offset = 0);
    }

    public abstract class LogixValueResolverBase<T> : ILogixValueResolver<T>
    {
        public Type ValueType => typeof(T);
        public abstract T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        public abstract void WriteTagBuffer(Tag tag, TagDefinition definition, T value, int offset = 0);
        object ILogixValueResolver.ResolveValue(Tag tag, TagDefinition definition, int offset)
            => ResolveValue(tag, definition, offset) ?? default!;
        void ILogixValueResolver.WriteTagBuffer(Tag tag, TagDefinition definition, object value, int offset)
            => WriteTagBuffer(tag, definition, (T)value);


        protected object PrimitiveValueResolver(Tag tag, ushort typeCode, int offset = 0)
        {
            return (Code)(typeCode) switch
            {
                Code.BOOL => tag.GetBit(offset),
                Code.SINT or Code.USINT => tag.GetInt8(offset),
                Code.INT or Code.UINT or Code.WORD => tag.GetInt16(offset),
                Code.DINT or Code.UDINT or Code.DWORD => tag.GetInt32(offset),
                Code.LINT or Code.ULINT or Code.LWORD => tag.GetInt64(offset),
                Code.REAL => tag.GetFloat32(offset),
                Code.LREAL => tag.GetFloat64(offset),
                Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                    => tag.GetString(offset),
                _ => throw new Exception($"Primitive type code:{typeCode:X} not handled")
            };
        }

        protected void PrimitiveValueWriter(Tag tag, ushort typeCode, object value, int offset = 0)
        {
            switch ((Code)typeCode)
            {
                case Code.BOOL: 
                    tag.SetBit(offset, (bool)value);
                    break;
                case Code.SINT or Code.USINT:
                    tag.SetInt8(offset, (sbyte)value);
                    break;
                case Code.INT or Code.UINT:
                    tag.SetInt16(offset, (short)value);
                    break;
                case Code.DINT or Code.UDINT:
                    tag.SetInt32(offset, (int)value);
                    break;
                case Code.LINT or Code.ULINT:
                    tag.SetInt64(offset, (long)value);
                    break;
                case Code.REAL:
                    tag.SetFloat32(offset, (float)value);
                    break;
                case Code.LREAL:
                    tag.SetFloat64(offset, (double)value);
                    break;
                case Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT:
                    tag.SetString(offset, (string)value);
                    break;
                default:
                    break;
            }
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

        public override void WriteTagBuffer(Tag tag, TagDefinition definition, object value, int offset = 0)
        {
            if (LogixTypes.IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return;

                if (value.GetType() == typeof(IEnumerable<>))
                {
                    var arr = (value as IEnumerable<object>).ToArray();
                    foreach (var m in definition.Type.Members)
                    {
                        {
                            int.TryParse(m.Name, out var i);
                            WriteTagBuffer(tag, m, arr[i], offset + (int)m.Offset);
                        }
                    }
                }
            }
            else if (LogixTypes.IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return;

                if (value.GetType() == typeof(IDictionary<string, object>))
                {
                    var dict = value as IDictionary<string, object>;
                    foreach (var m in definition.Type.Members)
                    {
                        WriteTagBuffer(tag, m, dict[m.Name], offset + (int)m.Offset);
                    }
                }
            }
            else
            {
                PrimitiveValueWriter(tag, definition.Type.Code, value, offset);
            }
        }
    }
}