using libplctag;
using Logix;
using Logix.Tags;
using System;
using TcHmiSrv.Core;
using static Logix.Tags.TagMetaHelpers;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolValueResolver : TagValueResolverBase<Value>
    {
        public override Value ResolveValue(Tag tag, TagDefinition definition, int offset = 0)
        {
            if (IsArray(definition.TypeCode))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return new Value();

                var members = new Value();
                foreach (var m in definition.Children)
                    members.Add(ResolveValue(tag, m, offset + (int)m.Offset));

                return members;
            }
            else if (IsUdt(definition.TypeCode) && !definition.TypeName.Contains("STRING"))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return new Value();

                var ret = new Value();
                var members = new Value();
                foreach (var m in definition.Children)
                {
                    if (m.TypeCode == (ushort)Code.BOOL)
                        members.Add(m.Name, ResolveValue(tag, m, ((offset + (int)m.Offset) * 8) + (int)m.BitOffset));
                    else
                        members.Add(m.Name, ResolveValue(tag, m, offset + (int)m.Offset));
                }

                return members;
            }
            else
            {
                var ret = PrimitiveValueResolver(tag, definition.TypeCode, offset);

                return (Code)(definition.TypeCode) switch
                {
                    Code.BOOL => (bool)ret,
                    Code.SINT => (sbyte)ret,
                    Code.USINT => (byte)ret,
                    Code.INT => (short)ret,
                    Code.UINT or Code.WORD => (ushort)ret,
                    Code.DINT => (int)ret,
                    Code.UDINT or Code.DWORD => (uint)ret,
                    Code.LINT => (long)ret,
                    Code.ULINT or Code.LWORD => (ulong)ret,
                    Code.REAL => (float)ret,
                    Code.LREAL => (double)ret,
                    Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                        => (string)ret,
                    _ => throw new Exception($"Primitive type code:{definition.TypeCode:X} not handled")
                };
            }
        }

        public override void WriteTagBuffer(Tag tag, TagDefinition definition, Value value, int offset = 0)
        {
            if (IsArray(definition.TypeCode))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return;

                foreach (var m in definition.Children)
                {
                    int.TryParse(m.Name, out var i);
                    WriteTagBuffer(tag, m, value[i], offset + (int)m.Offset);
                }

            }
            else if (IsUdt(definition.TypeCode) && !definition.TypeName.Contains("STRING"))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return;

                var ret = new Value();
                var members = new Value();
                foreach (var m in definition.Children)
                {
                    if (m.TypeCode == (ushort)Code.BOOL)
                        WriteTagBuffer(tag, m, value[m.Name], offset + (int)m.Offset);
                    else
                        WriteTagBuffer(tag, m, value[m.Name], ((offset + (int)m.Offset) * 8) + (int)m.BitOffset);
                }
            }
            else
            {
                object write = (Code)(definition.TypeCode) switch
                {
                    Code.BOOL => value.GetBool(),
                    Code.SINT => value.GetSByte(),
                    Code.USINT or Code.BYTE => value.GetByte(),
                    Code.INT or Code.UINT or Code.WORD => value.GetInt16(),
                    Code.DINT or Code.UDINT or Code.DWORD => value.GetInt32(),
                    Code.LINT or Code.ULINT or Code.LWORD => value.GetInt64(),
                    Code.REAL => value.GetSingle(),
                    Code.LREAL => value.GetDouble(),
                    Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                        => value.GetString(),
                    _ => throw new Exception($"Primitive type code:{definition.TypeCode:X} not handled")
                };

                PrimitiveValueWriter(tag, definition.TypeCode, write, offset);
            }
        }

        public static Value SetBit(bool bitValue, int bitOffset, Value currentValue, Code typeCode)
        {
            long converted = (bitValue) ? 
                currentValue |= (1 << bitOffset) :
                currentValue &= ~(1 << bitOffset);

            return typeCode switch
            {
                Code.SINT => (sbyte)converted,
                Code.BYTE or Code.USINT => (byte)converted,
                Code.INT => (short)converted,
                Code.UINT or Code.WORD => (ushort)converted,
                Code.DINT => (int)converted,
                Code.UDINT or Code.DWORD => (uint)converted,
                Code.LINT => (long)converted,
                Code.ULINT or Code.LWORD => (ulong)converted,
                _ => throw new NotSupportedException()
            };
        }
    }
}