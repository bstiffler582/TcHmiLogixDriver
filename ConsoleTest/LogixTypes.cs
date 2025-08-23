using libplctag.DataTypes;
using System;

namespace ConsoleTest
{
    public record TagDefinition(string Name, TypeDefinition Type);
    public record TypeDefinition(ushort Code, string Name = "", uint Dims = 0, List<TagDefinition>? Members = null);

    static class LogixTypes
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
            FULL_STRUCT = 0xA2, FULL_ARRAY = 0xA3
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
            else if (IsUdt(typeCode) && (typeCode & 0x00FF) == 0xCE)
                return "STRING";
            else if ((typeCode & 0x1000) != 0)
                return $"SystemType(0x{typeCode:X4})";
            else 
                return $"UnknownType(0x{typeCode:X4})";
        }

        // resolve types
        public static TypeDefinition ResolveType(TypeDefinition type, 
            Func<TypeDefinition, TypeDefinition> arrayResolver,
            Func<TypeDefinition, TypeDefinition> udtResolver)
        {
            if (IsArray(type.Code))
            {
                return arrayResolver(type);
            }
            else if (IsUdt(type.Code))
            {
                return udtResolver(type);
            }
            else
            {
                return new TypeDefinition(type.Code, ResolveTypeName(type.Code));
            }
        }

        public static TypeDefinition ArrayResolver(TypeDefinition type, Func<TypeDefinition, TypeDefinition> typeResolver)
        {
            var baseTypeCode = GetArrayBaseType(type.Code);
            var baseType = typeResolver(new TypeDefinition(baseTypeCode));
            var members = Enumerable.Range(0, (int)type.Dims)
                .Select(m => new TagDefinition($"{m}", baseType))
                .ToList();

            return new TypeDefinition(type.Code, $"ARRAY OF {baseType.Name}", type.Dims, members);
        }

        public static TypeDefinition UdtResolver(TypeDefinition type, Dictionary<ushort, TypeDefinition> typeCache, Func<TypeDefinition, TypeDefinition> typeResolver, Func<ushort, UdtInfo> readUdtInfo)
        {
            var udtId = GetUdtId(type.Code);
            if (typeCache.TryGetValue(udtId, out var cached))
                return cached;

            var udtInfo = readUdtInfo(udtId);
            var members = udtInfo.Fields
                .Select(m => new TagDefinition(m.Name, typeResolver(new TypeDefinition(m.Type, Dims: m.Metadata))))
                .ToList();

            var udtDef = new TypeDefinition(type.Code, udtInfo.Name, type.Dims, members);
            typeCache.Add(udtId, udtDef);
            return udtDef;
        }
    }
}
