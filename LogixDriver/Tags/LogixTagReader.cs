using libplctag;

namespace Logix.Tags
{
    public interface ILogixTagReader
    {
        IEnumerable<TagDefinition> ReadTagList(LogixTarget target);
        TypeDefinition ReadUdtInfo(LogixTarget target, ushort udtId);
        IEnumerable<TagDefinition> ReadProgramTags(LogixTarget target, string program);
        Tag CreateTag(LogixTarget target, string path, int elementCount = 1);
        string ReadControllerInfo(LogixTarget target);
    }

    /// <summary>
    /// Helper class for reading and decoding tag requests.
    /// </summary>
    public class LogixTagReader : ILogixTagReader
    {
        public IEnumerable<TagDefinition> ReadTagList(LogixTarget target)
        {
            var tagList = new List<TagDefinition>();

            var controllerTags = CreateTag(target, "@tags");
            controllerTags.Read();

            var tagSize = controllerTags.GetSize();

            int offset = 0;
            while (offset < tagSize)
            {
                var tagDef = LogixTagDecoder.Decode(controllerTags, offset, out int elementSize);
                tagList.Add(tagDef);
                offset += elementSize;
            }

            return tagList;
        }

        public TypeDefinition ReadUdtInfo(LogixTarget target, ushort udtId)
        {
            var tag = CreateTag(target, $"@udt/{udtId}");
            tag.Read();
            var typeDef = LogixTagDecoder.DecodeUdt(tag);

            return typeDef;
        }

        public IEnumerable<TagDefinition> ReadProgramTags(LogixTarget target, string program)
        {
            var tagList = new List<TagDefinition>();

            var programTags = CreateTag(target, $"{program}.@tags");
            programTags.Read();

            var tagSize = programTags.GetSize();

            int offset = 0;
            while (offset < tagSize)
            {
                var tagDef = LogixTagDecoder.Decode(programTags, offset, out int elementSize);
                tagList.Add(tagDef);
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
