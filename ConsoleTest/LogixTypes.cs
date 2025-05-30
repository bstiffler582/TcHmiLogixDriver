using libplctag.DataTypes;
using System;

namespace ConsoleTest
{
    public record TagDef(string Name, TypeDef Type);
    public record TypeDef(string Name, ushort Code, List<TagDef> Members, uint Dims = 0);

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

        public static bool IsUdt(ushort typeCode)
        {
            const ushort TYPE_IS_STRUCT = 0x8000;
            const ushort TYPE_IS_SYSTEM = 0x1000;

            return ((typeCode & TYPE_IS_STRUCT) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);
        }

        public static bool IsArray(ushort typeCode)
        {
            const ushort TYPE_IS_ARRAY = 0x2000;
            const ushort TYPE_IS_SYSTEM = 0x1000;

            return ((typeCode & TYPE_IS_ARRAY) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);
        }

        public static string ResolveTypeName(ushort typeCode)
        {
            if (Enum.IsDefined(typeof(Code), typeCode))
            {
                return ((Code)typeCode).ToString();
            }

            if (IsArray(typeCode))
            {
                return $"ARRAY OF (0x{(typeCode & 0x0FFF):X4})";
            }
            else if (IsUdt(typeCode))
            {
                if ((typeCode & 0x00FF) == 0xCE)
                    return "STRING";
                else
                    return $"UDT (ID:{typeCode & 0x0FFF})";
            }
            else
            {
                return $"UnknownType(0x{typeCode:X4})";
            }
        }

        public static ushort GetUdtId(ushort typeCode)
        {
            const ushort TYPE_UDT_ID_MASK = 0x0FFF;
            return (ushort)(typeCode & TYPE_UDT_ID_MASK);
        }
    }
}
