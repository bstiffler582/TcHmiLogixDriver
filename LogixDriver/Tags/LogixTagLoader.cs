using libplctag.DataTypes;

namespace Logix.Tags
{
    public interface ILogixTagLoader
    {
        IEnumerable<TagDefinition> LoadTags(LogixTarget target , ILogixTagReader reader, string selector = "*");
    }

    public class LogixTagLoader : ILogixTagLoader
    {
        
        // TODO: Refine selector
        public IEnumerable<TagDefinition> LoadTags(LogixTarget target, ILogixTagReader reader, string selector = "*")
        {
            var typeCache = new Dictionary<ushort, TypeDefinition>();

            // partial function to read UDT info
            Func<ushort, UdtInfo> udtInfoReader = (udtId) =>
                reader.ReadUdtInfo(target, udtId);

            var tagInfos = reader.ReadTagList(target);

            // read all tags
            if (string.IsNullOrEmpty(selector) || selector == "*")
            {
                var controllerTags = tagInfos
                .Where(tag =>
                    !tag.Name.StartsWith("Program:") &&
                    !LogixTypes.ResolveTypeName(tag.Type).Contains("SystemType"))
                .Select(tag => new TagDefinition(tag.Name, LogixTypes.TypeResolver(tag, typeCache, udtInfoReader)))
                .ToList();

                var programTags = tagInfos
                    .Where(tag => tag.Name.StartsWith("Program:"))
                    .Select(tag =>
                    {
                        var progTagInfos = reader.ReadProgramTags(target, tag.Name);
                        var progTags = progTagInfos
                            .Where(t => !LogixTypes.ResolveTypeName(t.Type).Contains("SystemType"))
                            .Select(tag => new TagDefinition(tag.Name, LogixTypes.TypeResolver(tag, typeCache, udtInfoReader)))
                            .ToList();
                        return new TagDefinition(tag.Name, new TypeDefinition(tag.Type, tag.Length, tag.Name, 0, progTags));
                    })
                    .ToList();

                return programTags.Concat(controllerTags);
            }
            else
            {
                if (selector.StartsWith("Program:"))
                {
                    // WIP
                    // isolate program / tag names
                    var programName = selector.Split(':')[1];
                    var split = programName.Split('.');
                    var tagName = (split.Length > 1) ? split[1] : null;
                }

                var programTags = tagInfos
                    .Where(tag => tag.Name.StartsWith($"Program:{selector}"))
                    .Select(tag =>
                    {
                        var progTagInfos = reader.ReadProgramTags(target, tag.Name);
                        var progTags = progTagInfos
                            .Where(t => !LogixTypes.ResolveTypeName(t.Type).Contains("SystemType"))
                            .Select(tag => new TagDefinition(tag.Name, LogixTypes.TypeResolver(tag, typeCache, udtInfoReader)))
                            .ToList();
                        return new TagDefinition(tag.Name, new TypeDefinition(tag.Type, tag.Length, tag.Name, 0, progTags));
                    })
                    .ToList();

                return programTags;
            }
        }
    }
}