using libplctag;
using Logix.Tags;

namespace Logix.Driver
{
    public class Driver : IDriver
    {
        public Target Target { get; }
        public bool IsConnected => isConnected;
        public string ControllerInfo => controllerInfo;

        private ITagValueChannel? channel;

        private readonly ITagCache tagCache;
        private readonly ITagMetaProvider metaProvider;
        private readonly ITagValueResolver valueResolver;
        private readonly ITagValueChannelFactory channelFactory;
        private readonly ITagFactory tagFactory;

        private volatile bool isConnected = false;
        private string controllerInfo = string.Empty;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";
        private const uint QUEUE_INTERVAL_MS = 50;

        public Driver(
            Target target,
            ITagValueResolver valueResolver,
            ITagCache tagCache,
            ITagMetaProvider metaProvider,
            ITagValueChannelFactory channelFactory,
            ITagFactory tagFactory)
        {
            Target = target;
            this.valueResolver = valueResolver;
            this.tagCache = tagCache;
            this.metaProvider = metaProvider;
            this.channelFactory = channelFactory;
            this.tagFactory = tagFactory;
        }

        public static Driver Create(Target target, ITagValueResolver? valueResolver = null)
        {
            var tagFactory = new TagFactory(target);
            return new Driver(
                target,
                valueResolver ?? new DefaultTagValueResolver(),
                new TagCache(),
                new TagMetaProvider(tagFactory),
                new TagValueChannelFactory(),
                tagFactory
            );
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
            channel?.Dispose();
            channel = connected ? channelFactory?.Open(tagFactory, QUEUE_INTERVAL_MS) : null;
            isConnected = connected;
        }

        public async Task LoadTagsAsync(IEnumerable<string>? tagFilter = null)
        {
            await metaProvider.LoadTagDefinitionsAsync(tagFilter);
        }

        public void LoadTags(IEnumerable<string>? tagFilter = null)
        {
            LoadTagsAsync(tagFilter).GetAwaiter().GetResult();
        }

        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat()
        {
            return metaProvider.GetTagDefinitionsFlat();
        }

        public IEnumerable<TagDefinition> GetTagDefinitions()
        {
            return metaProvider.GetTagDefinitions();
        }

        public object? ReadTagValue(string tagName)
        {
            if (!isConnected || channel is null)
                return null;

            var (definition, tag) = GetTag(tagName);
            tag = channel.Reader.ReadTag(tag);
            return valueResolver.ResolveValue(tag, definition);
        }

        public async Task<object?> ReadTagValueAsync(string tagName)
        {
            if (!isConnected || channel is null)
                return null;

            try
            {
                var (definition, tag) = GetTag(tagName);
                tag = await channel.Reader.ReadTagAsync(tag);
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
            if (!isConnected || channel is null)
                return;

            var (definition, tag) = GetTag(tagName);

            if (!tag.IsInitialized)
                tag = channel.Writer.Initialize(tag);

            valueResolver.WriteTagBuffer(tag, definition, value);
            tag = channel.Writer.WriteTag(tag);
        }

        public async Task WriteTagValueAsync(string tagName, object value)
        {
            if (!isConnected || channel is null)
                return;

            var (definition, tag) = GetTag(tagName);

            if (!tag.IsInitialized)
                tag = await channel.Writer.InitializeAsync(tag);

            valueResolver.WriteTagBuffer(tag, definition, value);
            tag = await channel.Writer.WriteTagAsync(tag);
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

            return new TagMetaDecoder().DecodeControllerInfo(tag) ?? string.Empty;
        }

        private (TagDefinition, Tag) GetTag(string tagPath)
        {
            if (!metaProvider.TryGetTagDefinition(tagPath, out var definition) || definition!.ExpansionLevel != ExpansionLevel.Deep)
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
            channel?.Dispose();
        }
    }
}
