using Logix;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using TcHmiSrv.Core.Tools.Json.Newtonsoft;
using TcHmiSrv.Core.Tools.Json.Newtonsoft.Converters;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbol : Symbol
    {
        private LogixTarget target;
        private LogixDriver driver;

        public LogixSymbol(LogixTarget target, LogixDriver driver) : base(LogixSchemaAdapter.BuildSymbolSchema(target))
        {
            this.target = target;
            this.driver = driver;
        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            var val = driver.ReadTagValue(target, string.Join(".", elements));

            Type type = val.GetType();
            if (type.IsPrimitive)
            {
                while (elements.Count > 0)
                    elements.Dequeue();

                return (int)val;
            }

            var v = TcHmiJsonSerializer.Deserialize(ValueJsonConverter.DefaultConverter,
                JsonConvert.SerializeObject(val));

            var b = new Value();
            b.Add("ctrlrReadTest", v);
            
            return b;
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }
}