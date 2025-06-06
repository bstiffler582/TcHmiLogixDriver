using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix
{
    public record TagDef(string Name, TypeDef Type);
    public record TypeDef(string Name, ushort Code, uint Dims = 0, List<TagDef>? Members = null);

    public class LogixSymbol : Symbol
    {
        public LogixSymbol(JsonSchemaValue readValue) :
            base(readValue)
        {

        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            return 0;
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            return 0;
        }
    }

    public class LogixSymbolAdapter
    {
        private void TestData()
        {
            var tags = File.ReadAllText("tags.json");
            var tagList = JsonSerializer.Deserialize<Dictionary<string, TagDef>>(tags);

            var udts = File.ReadAllText("udts.json");
            var udtList = JsonSerializer.Deserialize<Dictionary<string , TypeDef>>(udts);
        }
    }
}
