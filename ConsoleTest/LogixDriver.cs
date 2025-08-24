using libplctag;
using libplctag.DataTypes;
using System;
using System.IO;

namespace ConsoleTest
{
    public interface ILogixTagReader
    {
        TagInfo[] ReadTagInfo(LogixTarget target);
        UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId);
        TagInfo[] ReadProgramTags(LogixTarget target, string program);
    }

    public class LogixTagReader : ILogixTagReader
    {
        public TagInfo[] ReadTagInfo(LogixTarget target)
        {
            using (var tagList = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = "@tags",
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs),
            })
            {
                return tagList.Read();
            }
        }

        public UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId)
        {
            using (var udtTag = new Tag<UdtInfoPlcMapper, UdtInfo>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = $"@udt/{udtId}",
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs),
            })
            {
                return udtTag.Read();
            }
        }
        
        public TagInfo[] ReadProgramTags(LogixTarget target, string program)
        {
            using (var programTags = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = $"{program}.@tags",
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
            })
            {
                return programTags.Read();
            }
        }
    }

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
