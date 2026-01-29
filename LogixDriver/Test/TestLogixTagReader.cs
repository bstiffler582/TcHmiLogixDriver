using libplctag;
using Logix.Tags;

namespace Logix.Test
{
    public class TestLogixTagReader : ILogixTagReader
    {
        private IDictionary<string, List<TagInfo>> tagMap;
        private IDictionary<ushort, UdtInfo> udtMap;

        public TestLogixTagReader(IDictionary<string, List<TagInfo>> tagMap, IDictionary<ushort, UdtInfo> udtMap)
        {
            this.tagMap = tagMap;
            this.udtMap = udtMap;
        }

        public Tag CreateTag(LogixTarget target, string path, int elementCount = 1)
        {
            throw new NotImplementedException();
        }

        public string ReadControllerInfo(LogixTarget target)
        {
            return "TEST_READER";
        }

        public IEnumerable<TagInfo> ReadProgramTags(LogixTarget target, string program)
        {
            tagMap.TryGetValue(program, out var tags);
            if (tags == null)
                throw new KeyNotFoundException();

            return tags;
        }

        public IEnumerable<TagInfo> ReadTagList(LogixTarget target)
        {
            tagMap.TryGetValue("@tags", out var tags);
            if (tags == null)
                throw new KeyNotFoundException();

            return tags;
        }

        public UdtInfo ReadUdtInfo(LogixTarget target, ushort udtId)
        {
            udtMap.TryGetValue(udtId, out var udtInfo);
            if (udtInfo == null)
                throw new KeyNotFoundException();

            return udtInfo;
        }
    }
}
