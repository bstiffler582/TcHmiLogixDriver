using libplctag;
using Logix;
using Logix.Tags;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolValueResolver : LogixValueResolverBase<Value>
    {
        public override Value ResolveValue(Tag tag, TagDefinition definition, int offset = 0)
        {
            throw new System.NotImplementedException();
        }
    }
}