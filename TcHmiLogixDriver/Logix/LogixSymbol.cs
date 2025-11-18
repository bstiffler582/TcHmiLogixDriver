using System;
using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbol : Symbol
    {
        public LogixSymbol(LogixSymbolAdapter adapter) : base(LogixSymbolAdapter.BuildSymbolSchema(adapter))
        {
        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            throw new NotImplementedException();
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }
}