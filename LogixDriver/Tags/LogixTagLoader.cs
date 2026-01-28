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

        // TODO: implement individual tag definition loads via shallow type resolution
        public TagDefinition LoadTagDefinition(string tagName, LogixTarget target, ILogixTagReader reader)
        {
            var pathParts = tagName
                .Replace('[', '.')
                .Replace(']', '.')
                .Split('.');

            throw new NotImplementedException();
        }
    }
}