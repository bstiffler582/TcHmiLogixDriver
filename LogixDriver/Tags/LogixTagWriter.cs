using libplctag;
using libplctag.DataTypes;

namespace Logix.Tags
{
    public interface ILogixTagWriter
    {
        void WriteTagValue(LogixTarget target, string path);
    }

    public class LogixTagWriter : ILogixTagWriter
    {
        public void WriteTagValue(LogixTarget target, string path)
        {
            throw new NotImplementedException();
        }
    }
}
