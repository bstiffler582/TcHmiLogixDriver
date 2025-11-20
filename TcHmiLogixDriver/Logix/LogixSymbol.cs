using Logix;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

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
            driver.ValueResolver = new LogixSymbolValueResolver();
        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            TagDefinition def;
            StringBuilder path = new StringBuilder();

            if (elements.Count > 1)
            {
                target.TagDefinitions.TryGetValue(elements.Dequeue(), out def);
                path.Append(def.Name);
                while (elements.Count > 0)
                {
                    def = def.Type.Members.Find(m => m.Name == elements.Peek());

                    if (int.TryParse(def.Name, out var idx))
                        path.Append("[").Append(def.Name).Append("]");
                    else
                        path.Append(".").Append(def.Name);

                    elements.Dequeue();
                }
            }
            else
            {
                target.TagDefinitions.TryGetValue(elements.Dequeue(), out def);
                path.Append(def.Name);
            }

            if (def == null)
                throw new Exception("Tag not found");
            else
            {
                var val = driver.ReadTagValue(target, path.ToString()) as Value;
                return val;
            }
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }
}