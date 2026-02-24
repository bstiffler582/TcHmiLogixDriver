namespace Logix
{
    public enum ExpansionLevel
    {
        None = 0,
        Shallow = 1,
        Deep = 2
    }

    public class TagDefinition
    {
        public string Name { get; set; }
        public ushort TypeCode { get; set; }
        public uint Length { get; set; }
        public uint Offset { get; set; }
        public uint BitOffset { get; set; }
        public string TypeName { get; set; }
        public uint[]? Dimensions { get; set; }
        public List<TagDefinition>? Children { get; set; }
        public ExpansionLevel ExpansionLevel { get; set; } = ExpansionLevel.None;

        public TagDefinition(
            string name,
            ushort typeCode,
            uint length,
            uint offset = 0,
            uint bitOffset = 0,
            string typeName = "",
            uint[]? dimensions = null,
            List<TagDefinition>? children = null)
        {
            Name = name;
            TypeCode = typeCode;
            Length = length;
            Offset = offset;
            BitOffset = bitOffset;
            TypeName = typeName;
            Dimensions = dimensions;
            Children = children;
        }
        public TagDefinition(TagDefinition tagDefinition)
        {
            Name = tagDefinition.Name;
            TypeCode = tagDefinition.TypeCode;
            Length = tagDefinition.Length;
            Offset = tagDefinition.Offset;
            BitOffset = tagDefinition.BitOffset;
            TypeName = tagDefinition.TypeName;
            Dimensions = tagDefinition.Dimensions;
            Children = tagDefinition.Children;
        }

        public static TagDefinition FromTypeMemberDefinition(TypeMemberDefinition typeMember)
        {
            return new TagDefinition(
                typeMember.Name, 
                typeMember.Code, 
                LogixTypes.GetTypeLength(typeMember.Code), 
                typeMember.Offset, 
                typeMember.BitOffset, 
                LogixTypes.ResolveTypeName(typeMember.Code),
                [typeMember.Dimension]
            );
        }

        public bool IsArray => LogixTypes.IsArray(TypeCode);
        public bool IsPrimitive => LogixTypes.IsPrimitive(TypeCode);
        public int ElementCount()
        {
            if (Dimensions is null || Dimensions.Length == 0) return 1;
            long prod = 1;
            foreach (var d in Dimensions)
                prod = prod * Math.Max(1, (long)d);
            return (int)Math.Max(1, prod);
        }
    }

    public record TypeDefinition(ushort Id, uint Length, string Name = "", List<TypeMemberDefinition>? Members = null);

    public record TypeMemberDefinition(ushort Code, string Name, uint Offset, ushort Dimension, ushort BitOffset);

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

        public static bool IsPrimitive(ushort typeCode) =>
            (typeCode >= 0xC1 && typeCode <= 0xD4);

        public static bool IsUdt(ushort typeCode) =>
            ((typeCode & TYPE_IS_STRUCT) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);

        public static bool IsArray(ushort typeCode) =>
            ((typeCode & TYPE_IS_ARRAY) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);

        public static bool IsSystem(ushort typeCode) =>
            ((typeCode & TYPE_IS_SYSTEM) != 0);

        public static ushort GetUdtId(ushort typeCode) =>
            (ushort)(typeCode & TYPE_UDT_ID_MASK);

        public static ushort GetArrayBaseType(ushort typeCode) =>
            (ushort)(typeCode & ~TYPE_IS_ARRAY);

        public static string ResolveTypeName(ushort code, string name = "")
        {
            if (Enum.IsDefined(typeof(Code), code))
                return ((Code)code).ToString();
            else if (IsArray(code))
                return "array";
            else if (IsUdt(code))
                return "object";
            else if (name.StartsWith("Program:"))
                return "program";
            else if (IsSystem(code))
                return $"SystemType(0x{code:X4})";
            else
                return $"UnknownType(0x{code:X4})";
        }

        public static ushort GetTypeLength(ushort typeCode)
        {
            return (Code)(typeCode) switch
            {
                Code.BOOL => 1,
                Code.SINT or Code.USINT or Code.BYTE => 1,
                Code.INT or Code.UINT or Code.WORD => 2,
                Code.DINT or Code.UDINT or Code.DWORD or Code.TIME or Code.REAL => 4,
                Code.LINT or Code.ULINT or Code.LWORD or Code.DATE_AND_TIME or Code.LREAL => 8,
                Code.STRING or Code.STRING2 or Code.STRINGI or Code.STRINGN or Code.STRING_STRUCT
                    => 88,
                _ => 0
            };
        }
    }
}
