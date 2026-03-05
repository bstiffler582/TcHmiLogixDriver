using libplctag;
using Logix.Tags;

namespace Logix.Driver
{
    public class Driver : IDriver, IConnectionState
    {
        public Target Target { get; }
        public bool IsConnected => isConnected;
        public string ControllerInfo => controllerInfo;

        private readonly ITagMetaProvider metaProvider;
        private readonly ITagValueResolver valueResolver;
        private readonly ITagDefinitionCache tagDefinitionCache = new TagDefinitionCache();
        private readonly ITagCache tagCache = new TagCache();
        private readonly ITagMetaDecoder metaDecoder = new TagMetaDecoder();

        private ITagReadWriteQueue? readWriteQueue;
        private ITagValueReader? valueReader;
        private ITagValueWriter? valueWriter;

        private volatile bool isConnected = false;
        private string controllerInfo = string.Empty;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";
        private const int QUEUE_INTERVAL_MS = 50;

        public Driver(string name, string gateway, string path, PlcType plcType = PlcType.ControlLogix, ITagValueResolver? valueResolver = null)
        {
            Target = new Target(name, gateway, path, plcType);
            this.valueResolver = valueResolver ?? new DefaultTagValueResolver();

            // bypass queue for reading tag metadata
            metaProvider = new TagMetaProvider(new TagValueReader(Target), tagDefinitionCache);
        }

        public bool TryConnect()
        {
            if (isConnected)
                return true;

            try
            {
                controllerInfo = ReadControllerInfo();
                SetConnectionState(true);
                return true;
            }
            catch (Exception) 
            { 
                return false;
            }
        }

        private void SetConnectionState(bool connected)
        {
            if (connected)
            {
                readWriteQueue?.Dispose();
                readWriteQueue = new TagReadWriteQueue(QUEUE_INTERVAL_MS);

                valueReader = new QueuedTagValueReader(Target, readWriteQueue!);
                valueWriter = new QueuedTagValueWriter(readWriteQueue!);
            }
            
            isConnected = connected;
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
            var (definition, tag) = GetTag(tagName);
            tag = valueReader!.ReadTag(tag);
            return valueResolver.ResolveValue(tag, definition);
        }

        public async Task<object?> ReadTagValueAsync(string tagName)
        {
            try
            {
                if (!isConnected)
                    return null;
                
                var (definition, tag) = GetTag(tagName);
                tag = await valueReader!.ReadTagAsync(tag);
                return valueResolver.ResolveValue(tag, definition);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                {
                    SetConnectionState(false); 
                    return null;
                }
                else throw new Exception(ex.Message);
            }
        }

        public void WriteTagValue(string tagName, object value)
        {
            var (definition, tag) = GetTag(tagName);

            if (!tag.IsInitialized)
                tag = valueWriter!.Initialize(tag);

            valueResolver.WriteTagBuffer(tag, definition, value);
            tag = valueWriter!.WriteTag(tag);
        }

        public async Task WriteTagValueAsync(string tagName, object value)
        {
            var (definition, tag) = GetTag(tagName);

            if (!tag.IsInitialized)
                tag = await valueWriter!.InitializeAsync(tag);

            valueResolver.WriteTagBuffer(tag, definition, value);
            tag = await valueWriter!.WriteTagAsync(tag);
        }

        /// <summary>
        /// Direct read of controller info
        /// </summary>
        /// <returns>String with controller model and version</returns>
        private string ReadControllerInfo()
        {
            var rawPayload = new byte[] {
                0x01, 0x02, 0x20, 0x01, 0x24, 0x01 };

            var tag = CreateTag("@raw");

            tag.Initialize();
            tag.SetSize(rawPayload.Length);
            tag.SetBuffer(rawPayload);
            tag.Write();

            return metaDecoder!.DecodeControllerInfo(tag) ?? string.Empty;
        }

        private (TagDefinition, Tag) GetTag(string tagPath)
        {
            if (!tagDefinitionCache.TryGetTagDefinition(tagPath, out var definition) || definition!.ExpansionLevel != ExpansionLevel.Deep)
                definition = metaProvider.LoadTagDefinition(tagPath);

            if (definition is null)
                throw new Exception($"Unable to load definition for tag {tagPath}.");

            if (!tagCache.TryGetTag(tagPath, out var tag))
            {
                if (definition.IsArray)
                {
                    var readPath = ResolveArrayPath(tagPath, definition);
                    tag = CreateTag(readPath, definition.ElementCount());
                }
                else
                {
                    tag = CreateTag(tagPath, 1);
                }
                tagCache.AddTag(tagPath, tag);
            }

            if (tag is null)
                throw new Exception($"Unable to create libplctag {tagPath}.");

            return (definition, tag);
        }

        private Tag CreateTag(string tagPath, int elementCount = 1)
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

            return tag;
        }

        private string ResolveArrayPath(string tagName, TagDefinition definition)
        {
            var member = definition;
            var path = tagName;
            while (member is not null && member.IsArray)
            {
                path += "[0]";
                member = member.Children?.First();
            }

            return path;
        }

        public void Dispose()
        {
            readWriteQueue?.Dispose();
        }
    }
}
