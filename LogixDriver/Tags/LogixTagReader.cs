using libplctag;

namespace Logix.Tags
{
    public interface ILogixTagReader
    {
        IEnumerable<TagInfo> ReadTagList(LogixTarget target);
        UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId);
        IEnumerable<TagInfo> ReadProgramTags(LogixTarget target, string program);
        Tag CreateTag(LogixTarget target, string path, int elementCount = 1);
        string ReadControllerInfo(LogixTarget target);
    }

    public class LogixTagReader : ILogixTagReader
    {
        public IEnumerable<TagInfo> ReadTagList(LogixTarget target)
        {
            var tagList = new List<TagInfo>();

            var controllerTags = CreateTag(target, "@tags");
            controllerTags.Read();

            var tagSize = controllerTags.GetSize();

            int offset = 0;
            while (offset < tagSize)
            {
                var tagInfo = LogixTagDecoder.Decode(controllerTags, offset, out int elementSize);
                tagList.Add(tagInfo);
                offset += elementSize;
            }

            return tagList;
        }

        public UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId)
        {
            var tag = CreateTag(target, $"@udt/{udtId}");
            tag.Read();
            return LogixTagDecoder.DecodeUdt(tag);
        }

        public IEnumerable<TagInfo> ReadProgramTags(LogixTarget target, string program)
        {
            var tagList = new List<TagInfo>();

            var programTags = CreateTag(target, $"{program}.@tags");
            programTags.Read();

            var tagSize = programTags.GetSize();

            int offset = 0;
            while (offset < tagSize)
            {
                var tagInfo = LogixTagDecoder.Decode(programTags, offset, out int elementSize);
                tagList.Add(tagInfo);
                offset += elementSize;
            }

            return tagList;
        }

        public Tag CreateTag(LogixTarget target, string path, int elementCount = 1)
        {
            var tag = new Tag
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = path,
                ElementCount = elementCount,
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
