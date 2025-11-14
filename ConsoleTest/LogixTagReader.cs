using libplctag;
using libplctag.DataTypes;

namespace ConsoleTest
{
    public interface ILogixTagReader
    {
        TagInfo[] ReadTagList(LogixTarget target);
        UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId);
        TagInfo[] ReadProgramTags(LogixTarget target, string program);
        Tag ReadTagValue(LogixTarget target, string path, int elements = 1);
    }

    public class LogixTagReader : ILogixTagReader
    {
        public TagInfo[] ReadTagList(LogixTarget target)
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

        public Tag ReadTagValue(LogixTarget target, string path, int elements = 1)
        {
            var tag = new Tag
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = path,
                ElementCount = Math.Max(elements, 1),
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
            };
            
            tag.Read();
            return tag;
        }
    }
}
