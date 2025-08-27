using libplctag.DataTypes;

namespace ConsoleTest
{
    public class LogixDriver
    {
        private readonly ILogixTagReader tagReader;

        public LogixDriver(ILogixTagReader? tagReader = null)
        {
            this.tagReader = tagReader ?? new LogixTagReader();
        }

        public IEnumerable<TagDefinition> LoadTags(LogixTarget target)
        {
            var typeCache = new Dictionary<ushort, TypeDefinition>();

            Func<ushort, UdtInfo> udtInfoHandler = (udtInfo) => 
                tagReader.ReadUdtInfo(target, udtInfo);

            Func<TagInfo, TagDefinition> getTagDefintion = (TagInfo tag) =>
                new TagDefinition(tag.Name, LogixTypes.ResolveType(tag, typeCache, udtInfoHandler));

            var tagInfos = tagReader.ReadTagInfo(target);

            var controllerTags = ResolveControllerTags(tagInfos, getTagDefintion);
            var programTags = ResolveProgramTags(tagInfos, getTagDefintion, (string program) => tagReader.ReadProgramTags(target, program));

            return programTags.Concat(controllerTags);
        }

        private IEnumerable<TagDefinition> ResolveControllerTags(IEnumerable<TagInfo> tagInfos, Func<TagInfo, TagDefinition> getTagDefinition)
        {
            return tagInfos
                .Where(tag =>
                    !tag.Name.StartsWith("Program:") &&
                    !LogixTypes.ResolveTypeName(tag.Type).Contains("SystemType"))
                .Select(getTagDefinition)
                .ToList();
        }

        private IEnumerable<TagDefinition> ResolveProgramTags(IEnumerable<TagInfo> tagInfos, Func<TagInfo, TagDefinition> getTagDefinition, Func<string, TagInfo[]> readProgramTags)
        {
            return tagInfos
                .Where(tag => tag.Name.StartsWith("Program:"))
                .Select(tag =>
                {
                    var progTagInfos = readProgramTags(tag.Name);
                    var progTags = progTagInfos
                        .Where(t => !LogixTypes.ResolveTypeName(t.Type).Contains("SystemType"))
                        .Select(getTagDefinition)
                        .ToList();
                    return new TagDefinition(tag.Name, new TypeDefinition(tag.Type, tag.Name, 0, progTags));
                })
                .ToList();
        }

        public object ReadTagValue(LogixTarget target, string tagName)
        {
            var definition = target.GetTagDefinitionByName(tagName);
            if (definition is null)
                return "Tag not found!";
            else
            {
                var tag = tagReader.ReadTagValue(target, tagName);
                return LogixTypes.ResolveValue(tag, definition);
            }
        }

        public void PrintTags(IEnumerable<TagDefinition> tags)
        {
            foreach (var tag in tags)
            {
                Console.WriteLine($"Name={tag.Name} Type={tag.Type.Name}");
                PrintChildTags(tag, tag.Name, 1);
            }
        }

        private void PrintChildTags(TagDefinition parent, string path, int level)
        {
            if (parent.Type.Members is null) return;

            var space = new String('.', level);
            foreach(var m in parent.Type.Members)
            {
                string name;
                if (parent.Type.Name.Contains("ARRAY"))
                    name = $"{path}[{m.Name}]";
                else
                    name = $"{path}.{m.Name}";

                Console.WriteLine($"{space}Name={name} Type={m.Type.Name}");
                if (m.Type.Members?.Count > 0 && m.Type.Name != "STRING")
                    PrintChildTags(m, name, level + 1);
            }
        }
    }
}
