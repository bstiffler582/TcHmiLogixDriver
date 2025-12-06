using libplctag;
using libplctag.DataTypes;
using Logix;

namespace Logix.Tags
{
    public interface ILogixTagReader
    {
        TagInfo[] ReadTagList(LogixTarget target);
        UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId);
        TagInfo[] ReadProgramTags(LogixTarget target, string program);
        Tag GetTag(LogixTarget target, string path, int elementCount = 0);
        string ReadControllerInfo(LogixTarget target);
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

        public Tag GetTag(LogixTarget target, string path, int elementCount = 0)
        {
            var tag = new Tag
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = path,
                ElementCount = Math.Max(elementCount, 1),
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
            };

            return tag;
        }

        public string ReadControllerInfo(LogixTarget target)
        {
            var tag = new Tag
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = "@raw",
                Timeout = TimeSpan.FromMilliseconds(target.TimeoutMs)
            };

            var rawPayload = new byte[] {
                0x01, 0x02,
                0x20, 0x01,
                0x24, 0x01
            };

            tag.Initialize();
            tag.SetSize(rawPayload.Length);
            tag.SetBuffer(rawPayload);
            tag.Write();
            var buffer = tag.GetBuffer();

            var offset = 10;
            var major = buffer[offset].ToString();
            offset += 1;
            var minor = buffer[offset].ToString();
            offset += 8;
            var model = System.Text.Encoding.ASCII.GetString(buffer, offset, buffer.Length - offset).TrimEnd('\0');

            return $"{model} v{major}.{minor}";
        }
    }
}
