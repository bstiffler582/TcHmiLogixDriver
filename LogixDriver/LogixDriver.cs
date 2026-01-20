using Logix.Tags;
using libplctag;

namespace Logix
{
    public class LogixDriver : IDisposable
    {
        public ILogixTagReader TagReader { get; set; } = new LogixTagReader();
        public ILogixTagLoader TagLoader { get; set; } = new LogixTagLoader();
        public ILogixValueResolver ValueResolver { get; set; } = new LogixDefaultValueResolver();
        public bool IsConnected => isConnected;

        public readonly LogixTarget Target;
        private Dictionary<string, Tag> tagCache = new();
        private LogixTagReadWriteQueue? readWriteQueue;
        private volatile bool isConnected = false;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public LogixDriver(LogixTarget target)
        {
            Target = target;
            SetConnectionState(true);
        }

        /// <summary>
        /// Read tag info and build tag definitions
        /// </summary>
        /// <returns>Mutates Target</returns>
        public IEnumerable<TagDefinition> LoadTags(string selector = "*")
        {
            var tags = TagLoader.LoadTags(Target, TagReader, selector);
            Target.AddTagDefinition(tags);
            return tags;
        }

        private (TagDefinition, Tag) GetTag(string tagName)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            if (!tagCache.TryGetValue(tagName, out var tag))
            {
                if (definition.Type.IsArray())
                {
                    var readPath = ResolveArrayPath(tagName, definition);
                    tag = TagReader.CreateTag(Target, readPath, definition.Type.ElementCount());
                }
                else
                {
                    tag = TagReader.CreateTag(Target, tagName, 1);
                }
                tagCache.Add(tagName, tag);
            }

            return (definition, tag);
        }

        private string ResolveArrayPath(string tagName, TagDefinition definition)
        {
            var member = definition;
            var path = tagName;
            while (member is not null && member.Type.IsArray())
            {
                path += "[0]";
                member = member.Type.Members?.First();
            }

            return path;
        }

        protected void SetConnectionState(bool isConnected)
        {
            if (isConnected)
            {
                this.isConnected = true;
                if (readWriteQueue is null)
                    readWriteQueue = new LogixTagReadWriteQueue(this, 50);
            }
            else
            {
                this.isConnected = false;
                readWriteQueue?.Dispose();
                readWriteQueue = null;
            }
        }

        /// <summary>
        /// Reads a PLC tag by name synchronously
        /// </summary>
        public object ReadTagValue(string tagName)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                if (readWriteQueue is null)
                    throw new InvalidOperationException("Queue not initialized. Ensure connection is established via ReadControllerInfo().");

                return readWriteQueue.EnqueueReadSync(tagName, definition, tag)
                    ?? throw new InvalidOperationException($"Read operation returned null for tag {tagName}");
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);

                throw new Exception($"Error reading tag {tagName}", ex);
            }
        }

        /// <summary>
        /// Reads a PLC tag by name asynchronously
        /// </summary>
        public async Task<object?> ReadTagValueAsync(string tagName)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                if (readWriteQueue is null)
                    throw new InvalidOperationException("Queue not initialized. Ensure connection is established via ReadControllerInfo().");

                return await readWriteQueue.EnqueueReadAsync(tagName, definition, tag);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);

                throw new Exception($"Error reading tag {tagName}", ex);
            }
        }

        /// <summary>
        /// Writes a PLC tag by name synchronously
        /// </summary>
        public void WriteTagValue(string tagName, object value)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                if (readWriteQueue is null)
                    throw new InvalidOperationException("Queue not initialized. Ensure connection is established via ReadControllerInfo().");

                readWriteQueue.EnqueueWriteSync(tagName, definition, tag, value);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);

                throw new Exception($"Error writing tag {tagName}", ex);
            }
        }

        /// <summary>
        /// Writes a PLC tag by name asynchronously
        /// </summary>
        public async Task WriteTagValueAsync(string tagName, object value)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                if (readWriteQueue is null)
                    throw new InvalidOperationException("Queue not initialized. Ensure connection is established via ReadControllerInfo().");

                await readWriteQueue.EnqueueWriteAsync(tagName, definition, tag, value);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);

                throw new Exception($"Error writing tag {tagName}", ex);
            }
        }

        public string ReadControllerInfo()
        {
            string info = string.Empty;
            try
            {
                info = TagReader.ReadControllerInfo(Target);
                SetConnectionState(true);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);
                else
                    throw new Exception("Tag read exception.", ex);
            }
            return info;
        }

        public void Dispose()
        {
            foreach (var tag in tagCache.Values)
                tag.Dispose();

            readWriteQueue?.Dispose();
        }
    }
}
