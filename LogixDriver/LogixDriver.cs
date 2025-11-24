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
        private LogixTagSubscription subscription;
        private volatile bool isConnected = false;

        private const string EX_ERR_TIMEOUT = "ErrorTimeout";

        public LogixDriver(LogixTarget target)
        {
            Target = target;
            subscription = new LogixTagSubscription(this, 500);
        }

        /// <summary>
        /// Read tag info and build tag definitions
        /// </summary>
        /// <returns>Mutates Target</returns>
        public IEnumerable<TagDefinition> LoadTags()
        {
            var tags = TagLoader.LoadTags(Target, TagReader);
            Target.AddTagDefinition(tags);
            return tags;
        }

        /// <summary>
        /// Reads a PLC tag by name
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns>Type dictated by tag definition and ValueResolver</returns>
        /// <exception cref="Exception"></exception>
        public object ReadTagValue(string tagName)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            // check if subscribed
            var (subscribedTag, isStale) = subscription.GetSubscribedTag(tagName);
            if (subscribedTag is not null && !isStale)
                return ValueResolver.ResolveValue(subscribedTag, definition);

            try
            {
                if (!tagCache.TryGetValue(tagName, out var tag))
                {
                    tag = TagReader.GetTag(Target, tagName, (int)definition.Type.Dims);
                    tagCache.Add(tagName, tag);
                }

                // wait for subscription reads
                if (subscription.Busy)
                    subscription.WaitUntilIdle();

                tag.Read();

                return ValueResolver.ResolveValue(tag, definition);
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    isConnected = false;
                
                throw new Exception("Tag read exception", ex);
            }
        }

        public void WriteTagValue(string tagName, object value)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            try
            {
                if (!tagCache.TryGetValue(tagName, out var tag))
                {
                    tag = TagReader.GetTag(Target, tagName, (int)definition.Type.Dims);
                    tagCache.Add(tagName, tag);
                }

                if (!tag.IsInitialized)
                    tag.Initialize();

                ValueResolver.WriteTagBuffer(tag, definition, value);

                tag.Write();
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    isConnected = false;

                throw new Exception("Tag read exception", ex);
            }
        }

        public string ReadControllerInfo()
        {
            string info = string.Empty;
            try
            {
                info = TagReader.ReadControllerInfo(Target);
                isConnected = true;
            }
            catch (Exception ex)
            {
                if (ex.Message == EX_ERR_TIMEOUT)
                    isConnected = false;
                else
                    throw new Exception("Tag read exception.", ex);
            }
            return info;
        }

        public void SubscribeTag(string tagName)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            if (!subscription.IsTagSubscribed(tagName))
            {
                var tag = TagReader.GetTag(Target, tagName, (int)definition.Type.Dims);
                subscription.SubscribeTag(tag);
            }
        }

        public void UnsubscribeTag(string tagName)
        {
            subscription.UnsubscribeTag(tagName);
        }

        public void UnsubscribeTags(IEnumerable<string> tagNames)
        {
            foreach (var tagName in tagNames)
                subscription.UnsubscribeTag(tagName);
        }

        public void Dispose()
        {
            foreach (var tag in tagCache.Values)
                tag.Dispose();

            subscription.Dispose();
        }
    }
}
