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
                var ret = LogixTypes.PrimitiveValueResolver(tag, definition.Type.Code, offset);
                return (Code)(definition.Type.Code) switch
                {
                    Code.BOOL => (bool)ret,
                    Code.SINT or Code.USINT => (sbyte)ret,
                    Code.INT or Code.UINT => (short)ret,
                    Code.DINT or Code.UDINT => (int)ret,
                    Code.LINT or Code.ULINT => (long)ret,
                    Code.REAL => (float)ret,
                    Code.LREAL => (double)ret,
                    Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                        => (string)ret,
                    _ => throw new Exception($"Primitive type code:{definition.Type.Code:X} not handled")
                };
            }
        }
    }
}