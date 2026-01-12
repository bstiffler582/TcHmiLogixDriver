using libplctag;
using libplctag.DataTypes;

namespace Logix
{
    public record TagDefinition(string Name, TypeDefinition Type, uint Offset = 0, uint BitOffset = 0);
    public record TypeDefinition(ushort Code, uint Length, string Name = "", uint[]? Dimensions = null, List<TagDefinition>? Members = null)
    {
        public int ElementCount()
        {
            if (Dimensions is null || Dimensions.Length == 0) return 1;
            long prod = 1;
            foreach (var d in Dimensions)
                prod = prod * Math.Max(1, (long)d);
            return (int)Math.Max(1, prod);
        }

        public bool IsArray()
        {
            return LogixTypes.IsArray(Code);
        }
    }

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

        const ushort TYPE_IS_ARRAY = 0x6000;
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

        // TODO:
        // create records for TagInfo, UdtInfo
        // create static methods for DecodeTagInfo, DecodeUdtInfo
        // remove dependency on mapper methods, decode from scratch

        private static TagInfo DecodeTagInfo(Tag tag, out int elementSize, int offset = 0)
        {
            /*
            var tagInstanceId = tag.GetUInt32(offset);
            var tagType = tag.GetUInt16(offset + 4);
            var tagLength = tag.GetUInt16(offset + 6);
            var tagArrayDims = new uint[]
            {
                tag.GetUInt32(offset + 8),
                tag.GetUInt32(offset + 12),
                tag.GetUInt32(offset + 16)
            };

            var apparentTagNameLength = (int)tag.GetUInt16(offset + 20);
            var actualTagNameLength = Math.Min(apparentTagNameLength, TAG_STRING_SIZE * 2 - 1);

            var tagNameBytes = Enumerable.Range(offset + 22, actualTagNameLength)
                .Select(o => tag.GetUInt8(o))
                .Select(Convert.ToByte)
                .ToArray();

            var tagName = Encoding.ASCII.GetString(tagNameBytes);

            elementSize = 22 + actualTagNameLength;

            return new TagInfo()
            {
                Id = tagInstanceId,
                Type = tagType,
                Name = tagName,
                Length = tagLength,
                Dimensions = tagArrayDims
            };
            */

            throw new NotImplementedException();
        }

        public static TypeDefinition TypeResolver(TagInfo tagInfo, Dictionary<ushort, TypeDefinition> typeCache, Func<ushort, UdtInfo> readUdtInfo)
        {
            if (IsArray(tagInfo.Type))
            {
                // base element type
                var baseTypeCode = GetArrayBaseType(tagInfo.Type);
                var baseType = TypeResolver(new TagInfo { Type = baseTypeCode }, typeCache, readUdtInfo);

                // build n-dimension array definition
                var dims = tagInfo.Dimensions.Where(n => n > 0).ToArray();
                TypeDefinition BuildArray(int idx = 0)
                {
                    if (idx >= dims.Length || dims[idx] == 0)
                        return baseType;

                    var child = BuildArray(idx + 1);

                    var dim = (int)dims[idx];
                    var members = Enumerable.Range(0, dim)
                        .Select(i => new TagDefinition($"{i}", child, (uint)i * child.Length))
                        .ToList();

                    var arrayName = $"ARRAY[{dim}] OF {child.Name}";
                    var arrayLength = (uint)dim * child.Length;
                    var subDims = dims.Skip(idx).ToArray();

                    return new TypeDefinition(tagInfo.Type, arrayLength, arrayName, subDims, members);
                }

                return BuildArray();
            }
            else if (IsUdt(tagInfo.Type))
            {
                var udtId = GetUdtId(tagInfo.Type);
                if (typeCache.TryGetValue(udtId, out var cached))
                    return cached;

                var udtInfo = readUdtInfo(udtId);
                var members = udtInfo.Fields
                    .Select(m =>
                    {
                        var memberInfo = new TagInfo { Type = m.Type, Dimensions = IsArray(m.Type) ? [m.Metadata] : [0] };
                        var bitOffset = (m.Type == (ushort)Code.BOOL) ? m.Metadata : 0;
                        return new TagDefinition(m.Name, TypeResolver(memberInfo, typeCache, readUdtInfo), m.Offset, (uint)bitOffset);
                    }).ToList();

                var udtDef = new TypeDefinition(tagInfo.Type, udtInfo.Size, udtInfo.Name, Array.Empty<uint>(), members);

                typeCache.Add(udtId, udtDef);

                return udtDef;
            }
            else
            {
                var length = (tagInfo.Length > 0) ? tagInfo.Length : GetTypeLength(tagInfo.Type);
                return new TypeDefinition(tagInfo.Type, length, ResolveTypeName(tagInfo.Type));
            }
        }

        private static ushort GetTypeLength(ushort typeCode)
        {
            return (Code)(typeCode) switch
            {
                Code.BOOL => 1,
                Code.SINT or Code.USINT => 1,
                Code.INT or Code.UINT or Code.WORD => 2,
                Code.DINT or Code.UDINT or Code.DWORD or Code.TIME => 4,
                Code.LINT or Code.ULINT or Code.LWORD or Code.DATE_AND_TIME => 8,
                Code.REAL => 4,
                Code.LREAL => 8,
                Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                    => 88,
                _ => throw new Exception($"Primitive type code:{typeCode:X} not handled")
            };
        }
    }
}
