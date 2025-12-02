using libplctag;
using Logix;
using Logix.Tags;
using System;
using TcHmiSrv.Core;
using static Logix.LogixTypes;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolValueResolver : LogixValueResolverBase<Value>
    {
        public override Value ResolveValue(Tag tag, TagDefinition definition, int offset = 0)
        {
            if (LogixTypes.IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return new Value();

                var members = new Value();
                foreach (var m in definition.Type.Members)
                    members.Add(ResolveValue(tag, m, offset + (int)m.Offset));

                return members;
            }
            else if (LogixTypes.IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return new Value();

                var ret = new Value();
                var members = new Value();
                foreach (var m in definition.Type.Members)
                    members.Add(m.Name, ResolveValue(tag, m, offset + (int)m.Offset));

                return members;
            }
            else
            {
                var ret = PrimitiveValueResolver(tag, definition.Type.Code, offset);
                return (Code)(definition.Type.Code) switch
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
                    _ => throw new Exception($"Primitive type code:{definition.Type.Code:X} not handled")
                };
            }
        }

        public override void WriteTagBuffer(Tag tag, TagDefinition definition, Value value, int offset = 0)
        {
            if (LogixTypes.IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return;

                foreach (var m in definition.Type.Members)
                {
                    int.TryParse(m.Name, out var i);
                    WriteTagBuffer(tag, m, value[i], offset + (int)m.Offset);
                }

            }
            else if (LogixTypes.IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return;

                var ret = new Value();
                var members = new Value();
                foreach (var m in definition.Type.Members)
                {
                    WriteTagBuffer(tag, m, value[m.Name], offset + (int)m.Offset);
                }
            }
            else
            {
                object write = (Code)(definition.Type.Code) switch
                {
                    Code.BOOL => value.GetBool(),
                    Code.SINT or Code.USINT or Code.BYTE => value.GetSByte(),
                    Code.INT or Code.UINT or Code.WORD => value.GetInt16(),
                    Code.DINT or Code.UDINT or Code.DWORD => value.GetInt32(),
                    Code.LINT or Code.ULINT or Code.LWORD => value.GetInt64(),
                    Code.REAL => value.GetSingle(),
                    Code.LREAL => value.GetDouble(),
                    Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                        => value.GetString(),
                    _ => throw new Exception($"Primitive type code:{definition.Type.Code:X} not handled")
                };

                PrimitiveValueWriter(tag, definition.Type.Code, write, offset);
            }
        }
    }
}