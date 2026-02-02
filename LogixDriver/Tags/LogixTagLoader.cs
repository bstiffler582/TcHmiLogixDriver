namespace Logix.Tags
{
    public interface ILogixTagLoader
    {
        IEnumerable<TagDefinition> LoadAllTagDefinitions(LogixTarget target , ILogixTagReader reader);
        TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader);
    }

    public class LogixTagLoader : ILogixTagLoader
    {
        
        public IEnumerable<TagDefinition> LoadAllTagDefinitions(LogixTarget target, ILogixTagReader reader)
        {
            var typeCache = new Dictionary<ushort, TypeDefinition>();
            var typeResolver = new LogixTypeResolver(target, reader, typeCache);
            var tagInfos = reader.ReadTagList(target);

            var controllerTags = tagInfos
                    .Where(tag =>
                        !tag.Name.StartsWith("Program:") &&
                        !LogixTypes.IsSystem(tag.Type))
                    .Select(tag => new TagDefinition(tag.Name, typeResolver.Resolve(tag, true)))
                    .ToList();

            var programTags = tagInfos
                .Where(tag => tag.Name.StartsWith("Program:"))
                .Select(tag =>
                {
                    var progTagInfos = reader.ReadProgramTags(target, tag.Name);
                    var progTags = progTagInfos
                        .Where(t => !LogixTypes.IsSystem(t.Type))
                        .Select(tag => new TagDefinition(tag.Name, typeResolver.Resolve(tag, true)))
                        .ToList();
                    return new TagDefinition(tag.Name, new TypeDefinition(tag.Type, tag.Length, tag.Name, Array.Empty<uint>(), progTags));
                })
                .ToList();

            return programTags.Concat(controllerTags);
        }

        // Load individual tag definition
        public TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader)
        {
            var path = tagName
                .Replace('[', '.')
                .Replace(']', '.')
                .Split('.')
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            if (path.Length < 1)
                throw new ArgumentException("Invalid tag name");

            IEnumerable<TagInfo> tags;
            int index = 0;

            if (path[0].StartsWith("Program:"))
            {
                tags = reader.ReadProgramTags(target, path[0]);
                index++;
            }
            else
                tags = reader.ReadTagList(target);

            var root = tags.FirstOrDefault(t => t.Name == path[index]);

            if (tags.Count() < 1 || root is null)
                throw new Exception($"No tags match {tagName}.");

            var typeResolver = new LogixTypeResolver(target, reader);

            if (path.Length > index + 1)
            {
                var baseType = typeResolver.Resolve(root, deep: false);
                var type = ResolveTypeByPath(baseType, new Stack<string>(path.Skip(index + 1).Reverse()), typeResolver);
                return new TagDefinition(tagName, type);
            }
            else
            {
                return new TagDefinition(tagName, typeResolver.Resolve(root, deep: true));
            }
        }

        private TypeDefinition ResolveTypeByPath(TypeDefinition parent, Stack<string> path, LogixTypeResolver typeResolver)
        {
            // Base case: no more path segments to traverse
            if (path.Count == 0)
            {
                // Deep resolve the terminal type
                var terminalTagInfo = new TagInfo
                {
                    Type = parent.Code,
                    Dimensions = parent.Dimensions ?? Array.Empty<uint>()
                };
                return typeResolver.Resolve(terminalTagInfo, deep: true);
            }

            // Recursive case: navigate to the next member
            var segment = path.Pop();
            var member = parent.Members?.FirstOrDefault(m => m.Name == segment);

            if (member is null)
                throw new Exception($"Member '{segment}' not found in type '{parent.Name}'");

            // Continue recursing with shallow resolution for navigation
            var memberTagInfo = new TagInfo
            {
                Type = member.Type.Code,
                Dimensions = member.Type.Dimensions ?? Array.Empty<uint>()
            };
            var memberType = typeResolver.Resolve(memberTagInfo, deep: false);

            return ResolveTypeByPath(memberType, path, typeResolver);
        }
    }
}