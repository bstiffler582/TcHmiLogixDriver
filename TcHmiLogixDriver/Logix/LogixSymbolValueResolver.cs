using Logix.Tags;
using System;
using TcHmiSrv.Core;
using static Logix.Tags.TagMetaHelpers;

namespace TcHmiLogixDriver.Logix
{
    /// <summary>
    /// Resolves tag data to TcHmi values: primitives as their .NET types, strings as string,
    /// arrays as Value lists (nested per dimension), structures as Value maps.
    /// </summary>
    public class LogixSymbolValueResolver : TagValueResolverBase<Value>
    {
        public override Value ResolveValue(byte[] buffer, TypeRef type, int offset = 0, int bitOffset = 0)
        {
            if (type.IsArray)
            {
                var elements = new Value();
                foreach (var element in Elements(type, offset, bitOffset))
                    elements.Add(ResolveValue(buffer, element.Type, element.Offset, element.BitOffset));
                return elements;
            }

            switch (Resolved(type))
            {
                case PrimitiveType primitive:
                    return ToValue(primitive, ReadPrimitive(buffer, primitive, offset, bitOffset));

                case StringType stringType:
                    return ReadString(buffer, stringType, offset);

                case StructType structType:
                    var members = new Value();
                    foreach (var m in structType.Members)
                        members.Add(m.Name, ResolveValue(buffer, m.Type, offset + m.Offset, m.BitOffset));
                    return members;

                case var other:
                    throw new NotSupportedException($"Type {other.Name} can't be read.");
            }
        }

        public override void WriteTagBuffer(byte[] buffer, TypeRef type, Value value, int offset = 0, int bitOffset = 0)
        {
            if (type.IsArray)
            {
                if (value.Count != type.Dims[0])
                    throw new ArgumentException($"Write value for {type} has {value.Count} elements, expected {type.Dims[0]}.");

                var i = 0;
                foreach (var element in Elements(type, offset, bitOffset))
                    WriteTagBuffer(buffer, element.Type, value[i++], element.Offset, element.BitOffset);
                return;
            }

            switch (Resolved(type))
            {
                case PrimitiveType primitive:
                    WritePrimitive(buffer, primitive, FromValue(primitive, value), offset, bitOffset);
                    break;

                case StringType stringType:
                    WriteString(buffer, stringType, value.GetString(), offset);
                    break;

                case StructType structType:
                    foreach (var m in structType.Members)
                    {
                        // every member is required; a skipped member would write back whatever was last read
                        if (!value.ContainsKey(m.Name))
                            throw new ArgumentException($"Write value for {structType.Name} is missing member '{m.Name}'.");
                        WriteTagBuffer(buffer, m.Type, value[m.Name], offset + m.Offset, m.BitOffset);
                    }
                    break;

                case var other:
                    throw new NotSupportedException($"Type {other.Name} can't be written.");
            }
        }

        private static Value ToValue(PrimitiveType type, object value) => type.Code switch
        {
            Code.BOOL => (bool)value,
            Code.SINT => (sbyte)value,
            Code.USINT => (byte)value,
            Code.INT => (short)value,
            Code.UINT => (ushort)value,
            Code.DINT or Code.TIME => (int)value,
            Code.UDINT => (uint)value,
            Code.LINT or Code.DATE_AND_TIME => (long)value,
            Code.ULINT => (ulong)value,
            Code.REAL => (float)value,
            Code.LREAL => (double)value,
            _ => throw new NotSupportedException($"Type {type.Name} can't be read.")
        };

        private static object FromValue(PrimitiveType type, Value value) => type.Code switch
        {
            Code.BOOL => value.GetBool(),
            Code.SINT => value.GetSByte(),
            Code.USINT => value.GetByte(),
            Code.INT => value.GetInt16(),
            Code.UINT => value.GetUInt16(),
            Code.DINT or Code.TIME => value.GetInt32(),
            Code.UDINT => value.GetUInt32(),
            Code.LINT or Code.DATE_AND_TIME => value.GetInt64(),
            Code.ULINT => value.GetUInt64(),
            Code.REAL => value.GetSingle(),
            Code.LREAL => value.GetDouble(),
            _ => throw new NotSupportedException($"Type {type.Name} can't be written.")
        };
    }
}
