using libplctag;

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
            var typeResolver = new LogixTypeResolver(target, reader);
            var baseTags = ReadAllBaseTags(target, reader);

            var resolved = baseTags
                .Select(tag => typeResolver.Resolve(tag, true))
                .ToList();

            target.AddTagDefinition(resolved);

            return resolved;
        }

        private IEnumerable<TagDefinition> ReadAllBaseTags(LogixTarget target, ILogixTagReader reader)
        {
            var tagDefs = reader.ReadTagList(target);
            return tagDefs.Where(tag => tag.Name.StartsWith("Program:") || !LogixTypes.IsSystem(tag.TypeCode));
        }

        // Load individual tag definition
        public TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader)
        {
            throw new NotImplementedException();
            //if (target.TagDefinitionsFlat.TryGetValue(tagName, out var tagDef))
            //    return tagDef;

            //if (target.TagDefinitions.Count == 0)
            //    target.AddTagDefinition(ReadAllBaseTags(target, reader));

            //var pathParts = tagName
            //    .Replace('[', '.')
            //    .Replace(']', '.')
            //    .Split('.')
            //    .Where(s => !string.IsNullOrEmpty(s))
            //    .ToArray();

            //if (pathParts.Length < 1)
            //    throw new ArgumentException("Invalid tag name");

            //if (!target.TagDefinitions.TryGetValue(pathParts[0], out var root))
            //    throw new Exception($"Root tag {pathParts[0]} not found.");

            //var typeResolver = new LogixTypeResolver(target, reader);

            //var type = ResolveNestedMember(root, new Queue<string>(pathParts.Skip(1)), typeResolver);

            //root = new TagDefinition(root.Name, type, root.Offset, root.BitOffset);

            //return root;
        }

        //private TypeDefinition ResolveNestedMember(TagDefinition tag, Queue<string> path, LogixTypeResolver resolver)
        //{
        //    if (path.Count == 0)
        //        return resolver.Resolve(tag, deep: true);

        //    var segment = path.Dequeue();

        //    var shallowType = resolver.Resolve(tag, deep: false);

        //    var member = shallowType.Members?.FirstOrDefault(m => m.Name == segment);
        //    if (member == null)
        //        throw new Exception($"Member '{segment}' not found in type '{shallowType.Name}'");

        //    var resolvedMemberType = ResolveNestedMember(member, path, resolver);

        //    var updatedMembers = shallowType.Members!
        //        .Select(m => m.Name == segment
        //            ? new TagDefinition(m.Name, resolvedMemberType, m.Offset, m.BitOffset)
        //            : m)
        //        .ToList();

        //    return new TypeDefinition(
        //        shallowType.Code,
        //        shallowType.Length,
        //        shallowType.Name,
        //        shallowType.Dimensions,
        //        updatedMembers
        //    );
        //}
    }
}