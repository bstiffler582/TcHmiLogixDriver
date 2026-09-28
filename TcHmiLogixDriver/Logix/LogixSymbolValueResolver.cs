using Logix;
using Logix.Tags;
using System;
using TcHmiSrv.Core;
using static Logix.Tags.TagMetaHelpers;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolValueResolver : TagValueResolverBase<Value>
    {
        public override Value ResolveValue(byte[] buffer, TagDefinition definition, int offset = 0)
        {
            if (IsArray(definition.TypeCode))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return new Value();

                var members = new Value();
                foreach (var m in definition.Children)
                    members.Add(ResolveValue(buffer, m, offset + (int)m.Offset));

                return members;
            }
            else if (IsUdt(definition.TypeCode) && !definition.TypeName.Contains("STRING"))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return new Value();

                var members = new Value();
                foreach (var m in definition.Children)
                {
                    if (m.TypeCode == (ushort)Code.BOOL)
                        members.Add(m.Name, ResolveValue(buffer, m, ((offset + (int)m.Offset) * 8) + (int)m.BitOffset));
                    else
                        members.Add(m.Name, ResolveValue(buffer, m, offset + (int)m.Offset));
                }

                return members;
            }
            else
            {
                var ret = PrimitiveValueResolver(buffer, definition.TypeCode, offset);
                return (Code)(definition.TypeCode) switch
                {
                    Code.BOOL => (bool)ret,
                    Code.SINT => (sbyte)ret,
                    Code.USINT or Code.BYTE => (byte)ret,
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

        public override void WriteTagBuffer(byte[] buffer, TagDefinition definition, Value value, int offset = 0)
        {
            if (IsArray(definition.TypeCode))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return;

                foreach (var m in definition.Children)
                {
                    int.TryParse(m.Name, out var i);
                    WriteTagBuffer(buffer, m, value[i], offset + (int)m.Offset);
                }

            }
            else if (IsUdt(definition.TypeCode) && !definition.TypeName.Contains("STRING"))
            {
                if (definition.Children is null || definition.Children.Count < 1)
                    return;

                foreach (var m in definition.Children)
                {
                    // BOOL members are addressed by bit offset, everything else by byte offset
                    if (m.TypeCode == (ushort)Code.BOOL)
                        WriteTagBuffer(buffer, m, value[m.Name], ((offset + (int)m.Offset) * 8) + (int)m.BitOffset);
                    else
                        WriteTagBuffer(buffer, m, value[m.Name], offset + (int)m.Offset);
                }
            }
            else
            {
                object write = (Code)(definition.TypeCode) switch
                {
                    Code.BOOL => value.GetBool(),
                    Code.SINT => value.GetSByte(),
                    Code.USINT or Code.BYTE => value.GetByte(),
                    Code.INT => value.GetInt16(),
                    Code.UINT or Code.WORD => value.GetUInt16(),
                    Code.DINT => value.GetInt32(),
                    Code.UDINT or Code.DWORD => value.GetUInt32(),
                    Code.LINT => value.GetInt64(),
                    Code.ULINT or Code.LWORD => value.GetUInt64(),
                    Code.REAL => value.GetSingle(),
                    Code.LREAL => value.GetDouble(),
                    Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                        => value.GetString(),
                    _ => throw new Exception($"Primitive type code:{definition.TypeCode:X} not handled")
                };

                PrimitiveValueWriter(buffer, definition.TypeCode, write, offset);
            }
        }
    }
}