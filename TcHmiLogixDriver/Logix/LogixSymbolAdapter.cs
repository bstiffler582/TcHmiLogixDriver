using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using System.Runtime.CompilerServices;

namespace TcHmiLogixDriver.Logix
{
    public record TagDef(string Name, TypeDef Type);
    public record TypeDef(string Name, ushort Code, uint Dims = 0, List<TagDef>? Members = null, string BaseType = "");

    public static class LogixExtensions
    {
        
    }

    public static class LogixSymbolAdapter
    {

        public static Value ToValue(this TypeDef typeDef)
        {
            var ret = new Value();
            if (IsPrimitve(typeDef.Name))
            {
                ret.Add("$ref", ToTcHmiTypeName(typeDef.Name));
            }
            else if (typeDef.Name.StartsWith("ARRAY"))
            {
                ret.Add("type", "array");
                var items = new Value();
                items.Add("$ref", ToTcHmiTypeName(typeDef.BaseType));
                ret.Add("maxItems", typeDef.Dims);
                ret.Add("minItems", typeDef.Dims);
                ret.Add("items", items);
            }
            else if (typeDef.Members != null)
            {

            }

            return ret;
        }

        private static void TestData()
        {
            var tags = File.ReadAllText("tags.json");
            var tagList = JsonSerializer.Deserialize<Dictionary<string, TagDef>>(tags);

            var udts = File.ReadAllText("udts.json");
            var udtList = JsonSerializer.Deserialize<Dictionary<string , TypeDef>>(udts);
        }

        // TODO: use enum from console app
        private static bool IsPrimitve(string TypeName)
        {
            return new List<string>()
            {
                "BOOL",
                "INT",
                "DINT",
                "REAL",
                "STRING",
                "SINT"
            }.Contains(TypeName);
        }

        private static string ToTcHmiTypeName(string TypeName)
        {
            return $"tchmi:general#/definitions/{TypeName}";
        }

        // convert TypeDefs to Value to return to ListSymbols
        public static Value GetDefinitions()
        {
            var udts = new List<TypeDef>();
            // mock data
            var members = new List<TagDef>()
            {
                new TagDef("bTest", new TypeDef("BOOL", 0)),
                new TagDef("nTest", new TypeDef("DINT", 0)),
                new TagDef("fTest", new TypeDef("REAL", 0)),
                new TagDef("sTest", new TypeDef("STRING", 0)),
                new TagDef("arrTest", new TypeDef("ARRAY OF DINT", 0, 10, null, "DINT"))
            };
            var myUdt = new TypeDef("UDT_Test", 0, 0, members);
            udts.Add(myUdt);
            
            var definitions = new Value();
            definitions.Add("type", "object");

            foreach (var udt in udts) {
                definitions.Add(udt.Name, udt.ToValue());
            }

            return definitions;
        }
    }
}
