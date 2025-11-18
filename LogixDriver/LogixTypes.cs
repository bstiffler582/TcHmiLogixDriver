using libplctag;
using libplctag.DataTypes;

namespace Logix
{
    public record TagDefinition(string Name, TypeDefinition Type, uint Offset = 0);
    public record TypeDefinition(ushort Code, uint Length, string Name = "", uint Dims = 0, List<TagDefinition>? Members = null);

    public static class LogixTypes
    {
        public enum Code : ushort
        {
            BOOL = 0xC1, SINT = 0xC2, INT = 0xC3, DINT = 0xC4, 
            LINT = 0xC5, USINT = 0xC6, UINT = 0xC7, UDINT = 0xC8, ULINT = 0xC9, 
            REAL = 0xCA, LREAL = 0xCB,
            STIME = 0xCC, DATE = 0xCD, TIME_OF_DAY = 0xCE, DATE_AND_TIME = 0xCF,
            STRING = 0xD0, BYTE = 0xD1, WORD = 0xD2, DWORD = 0xD3, LWORD = 0xD4,
            STRING2 = 0xD5, FTIME = 0xD6, LTIME = 0xD7, ITIME = 0xD8,
            STRINGN = 0xD9, SHORT_STRING = 0xDA, TIME = 0xDB, EPATH = 0xDC,
            ENGUNIT = 0xDD, STRINGI = 0xDE,
            ABBREV_STRUCT = 0xA0, ABBREV_ARRAY = 0xA1,
            FULL_STRUCT = 0xA2, FULL_ARRAY = 0xA3,
            STRING_STRUCT = 0x8FCE
        }

        const ushort TYPE_IS_ARRAY = 0x2000;
        const ushort TYPE_IS_STRUCT = 0x8000;
        const ushort TYPE_IS_SYSTEM = 0x1000;
        const ushort TYPE_UDT_ID_MASK = 0x0FFF;

        public static bool IsUdt(ushort typeCode) =>
            ((typeCode & TYPE_IS_STRUCT) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);

        public static bool IsArray(ushort typeCode) =>
            ((typeCode & TYPE_IS_ARRAY) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);

        public static ushort GetUdtId(ushort typeCode) =>
            (ushort)(typeCode & TYPE_UDT_ID_MASK);

        public static ushort GetArrayBaseType(ushort typeCode) =>
            (ushort)(typeCode & ~TYPE_IS_ARRAY);

        public static string ResolveTypeName(ushort typeCode)
        {
            if (Enum.IsDefined(typeof(Code), typeCode))
                return ((Code)typeCode).ToString();
            else if ((typeCode & 0x1000) != 0)
                return $"SystemType(0x{typeCode:X4})";
            else 
                return $"UnknownType(0x{typeCode:X4})";
        }

        public static TypeDefinition TypeResolver(TagInfo tagInfo, Dictionary<ushort, TypeDefinition> typeCache, Func<ushort, UdtInfo> readUdtInfo)
        {
            Func<TagInfo, TypeDefinition> callback = (TagInfo tagInfo) => TypeResolver(tagInfo, typeCache, readUdtInfo);

            if (IsArray(tagInfo.Type))
            {
                return ArrayTypeResolver(tagInfo, callback);
            }
            else if (IsUdt(tagInfo.Type))
            {
                return UdtTypeResolver(tagInfo, typeCache, readUdtInfo, callback);
            }
            else
            {
                var length = (tagInfo.Length > 0) ? tagInfo.Length : GetTypeLength(tagInfo.Type);
                return new TypeDefinition(tagInfo.Type, length, ResolveTypeName(tagInfo.Type));
            }
        }

        private static TypeDefinition ArrayTypeResolver(TagInfo tagInfo, Func<TagInfo, TypeDefinition> resolveType)
        {
            var baseTypeCode = GetArrayBaseType(tagInfo.Type);
            var baseType = resolveType(new TagInfo { Type = baseTypeCode });
            var members = Enumerable.Range(0, (int)tagInfo.Dimensions[0])
                .Select(index => new TagDefinition($"{index}", baseType, (uint)index * baseType.Length))
                .ToList();
            return new TypeDefinition(tagInfo.Type, tagInfo.Dimensions[0] * baseType.Length, $"ARRAY OF {baseType.Name}", tagInfo.Dimensions[0], members);
        }

        private static TypeDefinition UdtTypeResolver(TagInfo tagInfo, Dictionary<ushort, TypeDefinition> typeCache, Func<ushort, UdtInfo> readUdtInfo, Func<TagInfo, TypeDefinition> resolveType)
        {
            var udtId = GetUdtId(tagInfo.Type);
            if (typeCache.TryGetValue(udtId, out var cached))
                return cached;

            var udtInfo = readUdtInfo(udtId);
            var members = udtInfo.Fields
                .Select(m => new TagDefinition(m.Name, resolveType(new TagInfo { Type = m.Type, Dimensions = [m.Metadata] }), m.Offset))
                .ToList();

            var udtDef = new TypeDefinition(tagInfo.Type, udtInfo.Size, udtInfo.Name, tagInfo.Dimensions[0], members);

            typeCache.Add(udtId, udtDef);

            return udtDef;
        }

        public static object ValueResolver(Tag tag, TagDefinition definition, int offset = 0)
        {
            if (IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new List<object>();
                foreach (var m in definition.Type.Members)
                    ret.Add(ValueResolver(tag, m, offset + (int)m.Offset));

                return ret;
            }
            else if (IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new Dictionary<string, object>();
                foreach (var m in definition.Type.Members)
                    ret[m.Name] = ValueResolver(tag, m, offset + (int)m.Offset);

                return ret;
            }
            else
            {
                return PrimitiveValueResolver(tag, definition.Type.Code, offset);
            }
        }

        private static object PrimitiveValueResolver(Tag tag, ushort typeCode, int offset = 0)
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

        private static ushort GetTypeLength(ushort typeCode)
        {
            return (Code)(typeCode) switch
            {
                Code.BOOL => 1,
                Code.SINT or Code.USINT => 1,
                Code.INT or Code.UINT => 2,
                Code.DINT or Code.UDINT => 4,
                Code.LINT or Code.ULINT => 8,
                Code.REAL => 4,
                Code.LREAL => 8,
                Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                    => 88,
                _ => throw new Exception($"Primitive type code:{typeCode:X} not handled")
            };
        }
    }
}
