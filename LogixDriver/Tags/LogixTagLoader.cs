namespace Logix.Tags
{
    public interface ILogixTagLoader
    {
        IEnumerable<TagDefinition> LoadTagDefinitions(LogixTarget target , ILogixTagReader reader, bool deep = true);
        TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader);
    }

    public class LogixTagLoader : ILogixTagLoader
    {
        public IEnumerable<TagDefinition> LoadTagDefinitions(LogixTarget target, ILogixTagReader reader, bool deep = true)
        {
            var treeBuilder = new LogixTagDefinitionExpander(target, reader);
            var baseTags = ReadAllBaseTags(target, reader);

            if (deep)
            {
                foreach (var tag in baseTags)
                    treeBuilder.ExpandNode(tag, true);
            }

            target.AddTagDefinition(baseTags);
            return baseTags;
        }

        private IEnumerable<TagDefinition> ReadAllBaseTags(LogixTarget target, ILogixTagReader reader)
        {
            var tagDefs = reader.ReadTagList(target);
            return tagDefs.Where(tag => tag.Name.StartsWith("Program:") || !LogixTypes.IsSystem(tag.TypeCode));
        }

        // Load individual tag definition
        public TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader)
        {
            if (target.TagDefinitions.Count == 0)
                target.AddTagDefinition(ReadAllBaseTags(target, reader));

            var pathParts = tagName
                .Replace('[', '.')
                .Replace(']', '.')
                .Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 1)
                throw new ArgumentException("Invalid tag name");

            if (!target.TagDefinitions.TryGetValue(pathParts[0], out var root))
                throw new Exception($"Root tag {pathParts[0]} not found.");

            var treeBuilder = new LogixTagDefinitionExpander(target, reader);
            var pathQueue = new Queue<string>(pathParts.Skip(1));

            TagDefinition tag = root;
            while (pathQueue.Count > 0)
            {
                var memberName = pathQueue.Dequeue();
                if (tag?.ExpansionLevel < ExpansionLevel.Shallow)
                    treeBuilder.ExpandNode(tag, false);
                var member = tag?.Children!.FirstOrDefault(c => c.Name == memberName);
                tag = member!;
            }

            if (!tag.IsPrimitive && tag.ExpansionLevel < ExpansionLevel.Deep)
                treeBuilder.ExpandNode(tag, true);

            target.AddTagDefinition(root);
            return new TagDefinition(tag) { Name = tagName };
        }
    }
}