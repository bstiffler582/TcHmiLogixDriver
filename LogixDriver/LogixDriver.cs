using Logix.Tags;
using libplctag;

namespace Logix
{
    public class LogixDriver : IDisposable
    {
        public ILogixTagReader TagReader { get; set; } = new LogixTagReader();
        public ILogixTagWriter TagWriter { get; set; } = new LogixTagWriter();
        public ILogixTagLoader TagLoader { get; set; } = new LogixTagLoader();
        public ILogixValueResolver ValueResolver { get; set; } = new LogixDefaultValueResolver();

        public readonly LogixTarget Target;

        private Dictionary<string, Tag> tagCache = new Dictionary<string, Tag>();

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
                throw new Exception("Tag not found");
            else
            {
                if (!tagCache.TryGetValue(tagName, out var tag))
                {
                    tag = TagReader.ReadTagValue(Target, tagName, (int)definition.Type.Dims);
                    tagCache.Add(tagName, tag);
                } 
                else
                {
                    tag.Read();
                }
                
                return ValueResolver.ResolveValue(tag, definition);
            }
        }

        public string ReadControllerInfo()
        {
            return TagReader.ReadControllerInfo(Target);
        }

        public void Dispose()
        {
            foreach (var tag in tagCache.Values)
                tag.Dispose();
        }
    }
}
