using libplctag;
using libplctag.DataTypes;
using System;
using System.IO;

namespace ConsoleTest
{
    public class LogixDriver
    {
        private static LogixDriver? instance;
        public static LogixDriver Instance
        {
            get
            {
                if (instance == null)
                    instance = new LogixDriver();
                return instance;
            }
        }

        public IEnumerable<TagDefinition> LoadTags(LogixTarget target, int timeoutMs = 5000)
        {
            var typeCache = new Dictionary<ushort, TypeDefinition>();
            
            UdtInfo udtInfoHandler(ushort udtId) => ReadUdtInfo(target, udtId, timeoutMs);
            TypeDefinition arrayResolver(TypeDefinition type) => LogixTypes.ArrayResolver(type, typeResolver);
            TypeDefinition udtResolver(TypeDefinition type) => LogixTypes.UdtResolver(type, typeCache, typeResolver, udtInfoHandler);
            TypeDefinition typeResolver(TypeDefinition type) => LogixTypes.ResolveType(type, arrayResolver, udtResolver);

            TagDefinition GetTagDefinition(TagInfo tag)
            {
                var type = typeResolver(new TypeDefinition(tag.Type, Dims: tag.Dimensions[0]));
                return new TagDefinition(tag.Name, type);
            }

            var tagInfos = ReadTagInfo(target, timeoutMs);

            var controllerTags = ResolveControllerTags(tagInfos, GetTagDefinition);
            var programTags = ResolveProgramTags(tagInfos, GetTagDefinition, (string program) => ReadProgramTags(target, program));

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

        private UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId, int timeoutMs = 5000)
        {
            using (var udtTag = new Tag<UdtInfoPlcMapper, UdtInfo>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = $"@udt/{udtId}",
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            })
            {
                return udtTag.Read();
            }
        }

        private TagInfo[] ReadTagInfo(LogixTarget target, int timeoutMs = 5000)
        {
            using (var tagList = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = "@tags",
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
            })
            {
                return tagList.Read();
            }
        }

        private TagInfo[] ReadProgramTags(LogixTarget target, string program, int timeoutMs = 5000)
        {
            using (var programTags = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = $"{program}.@tags",
                Timeout = TimeSpan.FromMilliseconds(timeoutMs)
            })
            {
                return programTags.Read();
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

            var space = new String(' ', (level * 2));
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
