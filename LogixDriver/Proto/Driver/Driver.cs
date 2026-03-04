using libplctag;

namespace Logix.Proto
{
    public class Driver : IDriver, IConnectionState, IDisposable
    {
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; }
        public bool IsConnected => isConnected;

        private readonly ITagMetaProvider metaProvider;
        private readonly ITagDefinitionCache tagDefinitionCache = new TagDefinitionCache();
        private readonly ITagCache tagCache = new TagCache();
        private readonly ITagMetaDecoder metaDecoder = new TagMetaDecoder();
        private readonly ITagReadWriteQueue? readWriteQueue;
        private readonly ITagValueReader valueReader;
        private readonly ITagValueWriter valueWriter;

        private volatile bool isConnected = true;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public Driver(string name, string gateway, string path, PlcType plcType = PlcType.ControlLogix, ITagValueResolver? valueResolver = null, bool useReadWriteQueue = true)
        {
            Target = new Target(name, gateway, path, plcType);
            ValueResolver = valueResolver ?? new DefaultTagValueResolver();

            // bypass queue for reading tag metadata
            metaProvider = new TagMetaProvider(new TagValueReader(Target), tagDefinitionCache);

            if (useReadWriteQueue)
            {
                readWriteQueue = new TagReadWriteQueue(this, 50);
                valueReader = new QueuedTagValueReader(Target, readWriteQueue);
                valueWriter = new QueuedTagValueWriter(readWriteQueue);
            }
            else
            {
                valueReader = new TagValueReader(Target);
                valueWriter = new TagValueWriter();
            }
        }

        public async Task LoadTagsAsync(IEnumerable<string>? tagFilter = null)
        {
            var tags = await metaProvider.LoadTagDefinitionsAsync(tagFilter);
            foreach (var tag in tags)
                tagDefinitionCache.AddTagDefinition(tag);
        }

        public void LoadTags(IEnumerable<string>? tagFilter = null)
        {
            LoadTagsAsync(tagFilter).GetAwaiter().GetResult();
        }

        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat()
        {
            return tagDefinitionCache.GetTagDefinitionsFlat();
        }

        public IEnumerable<TagDefinition> GetTagDefinitions()
        {
            return tagDefinitionCache.GetTagDefinitions();
        }

        public object? ReadTagValue(string tagName)
        {
            var tag = GetTag(tagName);
            var definition = GetDefinition(tagName);

            tag = valueReader.ReadTag(tag);
            return ValueResolver.ResolveValue(tag, definition);
        }

        public async Task<object?> ReadTagValueAsync(string tagName)
        {
            var tag = GetTag(tagName);
            var definition = GetDefinition(tagName);

            tag = await valueReader.ReadTagAsync(tag);
            return ValueResolver.ResolveValue(tag, definition);
        }

        public void WriteTagValue(string tagName, object value)
        {
            var tag = GetTag(tagName);
            var definition = GetDefinition(tagName);

            if (!tag.IsInitialized)
                tag = valueWriter.Initialize(tag);

            ValueResolver.WriteTagBuffer(tag, definition, value);
            tag = valueWriter.WriteTag(tag);
        }

        public async Task WriteTagValueAsync(string tagName, object value)
        {
            var tag = GetTag(tagName);
            var definition = GetDefinition(tagName);

            if (!tag.IsInitialized)
                tag = await valueWriter.InitializeAsync(tag);

            ValueResolver.WriteTagBuffer(tag, definition, value);
            tag = await valueWriter.WriteTagAsync(tag);
        }

        /// <summary>
        /// Direct read of controller info
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
            if (tagDefinitionCache.TryGetTagDefinition(tagPath, out var cached) && cached is not null)
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
            readWriteQueue?.Dispose();
        }
    }
}
