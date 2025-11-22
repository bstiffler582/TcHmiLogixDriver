using Logix.Tags;
using libplctag;
using System.Collections.Concurrent;

namespace Logix
{
    public class LogixDriver : IDisposable
    {
        class SubscribedTag
        {
            public readonly Tag Tag;
            public bool IsStale { get; set; }
            public SubscribedTag(Tag tag)
            {
                Tag = tag;
                IsStale = true;
            }
        }

        public ILogixTagReader TagReader { get; set; } = new LogixTagReader();
        public ILogixTagWriter TagWriter { get; set; } = new LogixTagWriter();
        public ILogixTagLoader TagLoader { get; set; } = new LogixTagLoader();
        public ILogixValueResolver ValueResolver { get; set; } = new LogixDefaultValueResolver();

        public readonly LogixTarget Target;

        private Dictionary<string, Tag> tagCache = new();

        private bool isSubscriptionProcessing = false;
        private ConcurrentDictionary<string, SubscribedTag> subscribedTags = new();
        private PeriodicTimer subscriptionTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        private Task? subscriptionProcess;
        private CancellationTokenSource? subscriptionCts;

        public LogixDriver(LogixTarget target)
        {
            Target = target;
        }

        public IEnumerable<TagDefinition> LoadTags()
        {
            var tags = TagLoader.LoadTags(Target, TagReader);
            Target.AddTagDefinition(tags);
            return tags;
        }

        public object ReadTagValue(string tagName)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            // if subscribed
            if (subscribedTags.TryGetValue(tagName, out var subscribedTag))
            {
                if (!subscribedTag.IsStale)
                    return ValueResolver.ResolveValue(subscribedTag.Tag, definition);
                else
                {
                    // going to do an explicit read, mark not stale
                    subscribedTag.IsStale = false;
                }
            }

            if (!tagCache.TryGetValue(tagName, out var tag))
            {
                tag = TagReader.ReadTagValue(Target, tagName, (int)definition.Type.Dims);
                tagCache.Add(tagName, tag);
            }
            else
            {
                // don't try to read at the same time as subscription
                if (subscribedTag is null || !isSubscriptionProcessing)
                    tag.Read();
            }

            return ValueResolver.ResolveValue(tag, definition);
        }

        public string ReadControllerInfo()
        {
            string info = string.Empty;
            try
            {
                info = TagReader.ReadControllerInfo(Target);
            }
            catch { }
            return info;
        }

        public void SubscribeTag(string tagName)
        {
            var definition = Target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag definition not found.");

            // if not started, start subscription process
            if (subscriptionProcess is null)
            {
                subscriptionCts = new CancellationTokenSource();
                subscriptionProcess = Task.Run(() => ProcessSubscriptionReads(subscriptionCts.Token));
            }

            if (!subscribedTags.ContainsKey(tagName))
            {
                var tag = GetTag(Target, tagName, (int)definition.Type.Dims);
                subscribedTags.TryAdd(tagName, new SubscribedTag(tag));
            }
        }

        public void SubscribeTags(IEnumerable<string> tagNames)
        {
            foreach (var tag in tagNames)
                SubscribeTag(tag);
        }

        public void UnsubscribeTag(string tagName)
        {
            subscribedTags.TryRemove(tagName, out _);
        }

        public void UnsubscribeTags(IEnumerable<string> tagNames)
        {
            foreach (var tag in tagNames)
                UnsubscribeTag(tag);
        }

        private async void ProcessSubscriptionReads(CancellationToken cancel)
        {
            while (await subscriptionTimer.WaitForNextTickAsync(cancel))
            {
                // Prevent re-entry
                if (isSubscriptionProcessing)
                    continue;

                isSubscriptionProcessing = true;

                // set all stale
                foreach (var stale in subscribedTags.Values)
                    stale.IsStale = true;

                // read tags
                foreach (var subscribed in subscribedTags.Values)
                {
                    await subscribed.Tag.ReadAsync();
                    subscribed.IsStale = false;
                }

                isSubscriptionProcessing = false;
            }
        }

        private Tag GetTag(LogixTarget target, string path, int elements = 1)
        {
            if (!tagCache.TryGetValue(path, out var tag))
            {
                tag = new Tag
                {
                    Gateway = target.Gateway,
                    Path = target.Path,
                    PlcType = target.PlcType,
                    Protocol = target.Protocol,
                    Name = path,
                    ElementCount = Math.Max(elements, 1),
                    Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
                };
            }

            return tag;
        }

        public void Dispose()
        {
            foreach (var tag in tagCache.Values)
                tag.Dispose();

            foreach (var subscribed in subscribedTags.Values)
                subscribed.Tag.Dispose();

            subscriptionCts?.Cancel();
            subscriptionTimer.Dispose();
        }
    }
}
