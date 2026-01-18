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
        private LogixTagSubscription? subscription;
        private volatile bool isConnected = false;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public LogixDriver(LogixTarget target)
        {
            Target = target;
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
            // rather than throw the subscription out, should we just stop/restart the timer?
            if (isConnected)
            {
                this.isConnected = true;
                if (subscription is null)
                    subscription = new LogixTagSubscription(this, 500);
            }
            else
            {
                this.isConnected = false;
                subscription?.Dispose();
                subscription = null;
            }
        }

        /// <summary>
        /// Reads a PLC tag by name
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns>Tag value as object</returns>
        /// <exception cref="Exception"></exception>
        public object ReadTagValue(string tagName)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                // check if subscribed
                if (subscription is not null)
                {
                    var (subscribedTag, isStale) = subscription.GetSubscribedTag(tagName);
                    if (subscribedTag is not null && !isStale)
                        return ValueResolver.ResolveValue(subscribedTag, definition);

                    // wait for subscription reads
                    if (subscription.Busy)
                        subscription.WaitUntilIdle();
                }

                tag.Read();

                return ValueResolver.ResolveValue(tag, definition);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    SetConnectionState(false);
                
                throw new Exception($"Error reading tag {tagName}", ex);
            }
        }

        public void WriteTagValue(string tagName, object value)
        {
            try
            {
                var (definition, tag) = GetTag(tagName);

                if (!tag.IsInitialized)
                    tag.Initialize();

                ValueResolver.WriteTagBuffer(tag, definition, value);

                tag.Write();
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

        public void SubscribeTag(string tagName)
        {
            if (subscription is null) return;

            var (definition, tag) = GetTag(tagName);

            if (!subscription.IsTagSubscribed(tagName))
                subscription.SubscribeTag(tagName, tag);
        }

        public void UnsubscribeTag(string tagName)
        {
            if (subscription is null) return;
            subscription.UnsubscribeTag(tagName);
        }

        public void UnsubscribeTags(IEnumerable<string> tagNames)
        {
            foreach (var tagName in tagNames)
                UnsubscribeTag(tagName);
        }

        public void Dispose()
        {
            foreach (var tag in tagCache.Values)
                tag.Dispose();

            if (subscription is not null)
                subscription.Dispose();
        }
    }
}
