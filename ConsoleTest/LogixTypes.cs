using libplctag.DataTypes;
using System;

namespace ConsoleTest
{
    static class LogixTypes
    {
        private static readonly Dictionary<ushort, string> TypeCodeMap = new()
        {
            { 0xC1, "BOOL" }, { 0xC2, "SINT" }, { 0xC3, "INT" }, { 0xC4, "DINT" },
            { 0xC5, "LINT" }, { 0xC6, "USINT" }, { 0xC7, "UINT" }, { 0xC8, "UDINT" },
            { 0xC9, "ULINT" }, { 0xCA, "REAL" }, { 0xCB, "LREAL" },
            { 0xCC, "STIME" }, { 0xCD, "DATE" }, { 0xCE, "TIME_OF_DAY" }, { 0xCF, "DATE_AND_TIME" },
            { 0xD0, "STRING" }, { 0xD1, "BYTE" }, { 0xD2, "WORD" }, { 0xD3, "DWORD" }, { 0xD4, "LWORD" },
            { 0xD5, "STRING2" }, { 0xD6, "FTIME" }, { 0xD7, "LTIME" }, { 0xD8, "ITIME" },
            { 0xD9, "STRINGN" }, { 0xDA, "SHORT_STRING" }, { 0xDB, "TIME" }, { 0xDC, "EPATH" },
            { 0xDD, "ENGUNIT" }, { 0xDE, "STRINGI" },
            { 0xA0, "ABBREV_STRUCT" }, { 0xA1, "ABBREV_ARRAY" },
            { 0xA2, "FULL_STRUCT" }, { 0xA3, "FULL_ARRAY" }
        };

        public static bool IsUdt(ushort typeCode)
        {
            const ushort TYPE_IS_STRUCT = 0x8000;
            const ushort TYPE_IS_SYSTEM = 0x1000;

            return ((typeCode & TYPE_IS_STRUCT) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);
        }

        private static bool IsArray(ushort typeCode)
        {
            const ushort TYPE_IS_ARR = 0x2000;
            const ushort TYPE_IS_SYSTEM = 0x1000;

            return ((typeCode & TYPE_IS_ARR) != 0) && !((typeCode & TYPE_IS_SYSTEM) != 0);
        }

        public static string ResolveTypeName(ushort typeCode)
        {
            if (TypeCodeMap.TryGetValue(typeCode, out var baseName))
            {
                return baseName;
            }

            bool udt = IsUdt(typeCode);
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
    }
}
