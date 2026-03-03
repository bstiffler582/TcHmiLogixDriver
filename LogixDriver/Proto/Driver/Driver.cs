using libplctag;
using System.Text;

namespace Logix.Proto
{
    public class Driver : IDriver, IDisposable
    {
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; } = new DefaultTagValueResolver();
        public bool IsConnected => isConnected;
        private readonly ITagMetaProvider metaProvider;
        private readonly ITagReadWriteQueue readWriteQueue;
        private readonly ITagDefinitionCache tagCache = new TagDefinitionCache();
        private readonly ITagMetaDecoder metaDecoder = new TagMetaDecoder();

        private volatile bool isConnected = true;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public Driver(Target target)
        {
            Target = target;
            metaProvider = new TagMetaProvider(this, tagCache);
            readWriteQueue = new TagReadWriteQueue(this, 50);
        }

        public Driver(string name, string gateway, string path, PlcType plcType = PlcType.ControlLogix)
        {
            Target = new Target(name, gateway, path, plcType);
            metaProvider = new TagMetaProvider(this, tagCache);
            readWriteQueue = new TagReadWriteQueue(this, 50);
        }

        public async Task LoadTagsAsync(IEnumerable<string>? tagFilter = null)
        {
            var tags = await metaProvider.LoadTagDefinitionsAsync(tagFilter);
            foreach (var tag in tags)
                tagCache.AddTagDefinition(tag);
        }

        public void LoadTags(IEnumerable<string>? tagFilter = null)
        {
            LoadTagsAsync(tagFilter).GetAwaiter().GetResult();
        }

        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitions()
        {
            return tagCache.GetTagDefinitionsFlat();
        }

        public async Task<Tag> ReadTagAsync(string tagName)
        {
            var tag = GetTag(tagName);
            await tag.ReadAsync();
            return tag;
        }

        public Tag ReadTag(string tagName) 
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

        public object? ReadTagValue(string tagName)
        {
            var tag = GetTag(tagName);
            var definition = GetDefinition(tagName);

            tag = readWriteQueue.EnqueueReadSync(tag);
            return ValueResolver.ResolveValue(tag, definition);
        }

        public Task<object?> ReadTagValueAsync(string tagName)
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

                var tag = GetTag("@raw", cache: false);

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

        private TagDefinition GetDefinition(string tagPath)
        {
            if (tagCache.TryGetTagDefinition(tagPath, out var cached) && cached is not null)
            {
                return cached;
            }
            else
            {
                var definition = metaProvider.LoadTagDefinition(tagPath);
                return definition;
            }
        }

        private Tag GetTag(string tagPath, int elementCount = 1, bool cache = true)
        {
            Tag tag;

            if (!tagCache.TryGetTag(tagPath, out tag!))
            {
                tag = new Tag
                {
                    Gateway = Target.Gateway,
                    Path = Target.Path,
                    PlcType = Target.PlcType,
                    Protocol = Protocol.ab_eip,
                    Name = tagPath,
                    ElementCount = elementCount,
                    Timeout = TimeSpan.FromMilliseconds(Target.TimeoutMs)
                };
            }

            if (cache)
                tagCache.AddTag(tagPath, tag);

            return tag;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
