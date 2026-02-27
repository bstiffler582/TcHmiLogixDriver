using libplctag;
using System.Text;

namespace Logix.Proto
{
    public class Driver : IDriver, IDisposable
    {
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; } = new DefaultTagValueResolver();
        public bool IsConnected => isConnected;
        public ITagMetaProvider MetaProvider { get; set; }

        private ITagCache? tagCache;
        private ITagReadWriteQueue? readWriteQueue;
        private ITagMetaDecoder? metaDecoder;
        private bool isConnected = true;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public Driver(Target target)
        {
            Target = target;
            MetaProvider = new TagMetaProvider(this);
            Initialize();
        }

        public Driver(string name, string gateway, string path, PlcType plcType = PlcType.ControlLogix)
        {
            Target = new Target(name, gateway, path, plcType);
            MetaProvider = new TagMetaProvider(this);
            Initialize();
        }

        private void Initialize()
        {
            tagCache = new TagCache();
            readWriteQueue = new TagReadWriteQueue(this);
            metaDecoder = new TagMetaDecoder();
        }

        public void LoadTags(IEnumerable<string>? tagNames = null)
        {
            
        }

        public IEnumerable<TagDefinition> GetTagDefinitions()
        {
            return tagCache!.GetTagDefinitions() ?? Enumerable.Empty<TagDefinition>();
        }

        public Task<Tag?> ReadTagAsync(string tagName)
        {
            if (!tagCache!.TryGetTag(tagName, out var tag))
            {
                tag = CreateTag(tagName);
            }

            return readWriteQueue!.EnqueueReadAsync(tag!);
        }

        public Tag? ReadTag(string tagName) 
        {
            return ReadTagAsync(tagName).GetAwaiter().GetResult();
        }

        public Task<Tag> WriteTagAsync(string tagName, object value)
        {
            throw new NotImplementedException();
        }

        public Tag WriteTag(string tagName, object value)
        {
            throw new NotImplementedException();
        }

        public object ReadTagValue(string tagName)
        {
            throw new NotImplementedException();
        }

        public Task<object> ReadTagValueAsync(string tagName)
        {
            throw new NotImplementedException();
        }

        public bool WriteTagValue(string tagName, object value)
        {
            throw new NotImplementedException();
        }

        public Task WriteTagValueAsync(string tagName, object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reads controller info
        /// Bypasses read/write queue to control connection state
        /// </summary>
        /// <returns>String with controller model and version</returns>
        public string ReadControllerInfo()
        {
            try
            {
                var rawPayload = new byte[] {
                0x01, 0x02,
                0x20, 0x01,
                0x24, 0x01
            };

                var tag = CreateTag("@raw");

                tag.Initialize();
                tag.SetSize(rawPayload.Length);
                tag.SetBuffer(rawPayload);
                tag.Write();

                return metaDecoder!.DecodeControllerInfo(tag) ?? string.Empty;
            }
            catch (Exception)
            {
                isConnected = false;
                return string.Empty;
            }
        }

        private Tag CreateTag(string tagPath, int elementCount = 1, bool cache = true)
        {
            var tag = new Tag
            {
                Gateway = Target.Gateway,
                Path = Target.Path,
                PlcType = Target.PlcType,
                Protocol = Protocol.ab_eip,
                Name = tagPath,
                ElementCount = elementCount,
                Timeout = TimeSpan.FromMilliseconds(Target.TimeoutMs)
            };

            if (cache)
                tagCache?.AddTag(tagPath, tag);

            return tag;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
