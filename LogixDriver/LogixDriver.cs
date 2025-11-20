using Logix.Tags;

namespace Logix
{
    public class LogixDriver
    {
        private ILogixTagReader tagReader = new LogixTagReader();
        private ILogixTagWriter tagWriter = new LogixTagWriter();
        private ILogixTagLoader tagLoader = new LogixTagLoader();
        public ILogixValueResolver ValueResolver { get; set; }

        public LogixDriver()
        {
            ValueResolver = new LogixDefaultValueResolver();
        }

        public IEnumerable<TagDefinition> LoadTags(LogixTarget target)
        {
            var tags = tagLoader.LoadTags(target, tagReader);
            target.AddTagDefinition(tags);
            return tags;
        }

        // TODO: something with this so we can specify type param here
        // TODO: cache R/W tags so they don't get GC'd
        public object ReadTagValue(LogixTarget target, string tagName)
        {
            var definition = target.TryGetTagDefinition(tagName);
            if (definition is null)
                throw new Exception("Tag not found");
            else
            {
                var tag = tagReader.ReadTagValue(target, tagName, (int)definition.Type.Dims);
                return ValueResolver.ResolveValue(tag, definition);
            }
        }

        public string ReadControllerInfo(LogixTarget target)
        {
            return tagReader.ReadControllerInfo(target);
        }
    }
}
