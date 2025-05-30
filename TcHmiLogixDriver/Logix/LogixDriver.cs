using libplctag;
using libplctag.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    class LogixDriver
    {
        private static LogixDriver instance;
        public static LogixDriver Instance
        {
            get
            {
                if (instance == null)
                    instance = new LogixDriver();
                return instance;
            }
        }

        const int TAG_STRING_SIZE = 200;

        public void GetTags(string name, string address, string path)
        {
            var tags = new Tag()
            {
                Gateway = address,
                Path = path,
                PlcType = PlcType.ControlLogix,
                Protocol = Protocol.ab_eip,
                Name = "@tags",
                Timeout = TimeSpan.FromMilliseconds(1000),
            };

            tags.Read();

            var controllerTags = DecodeAllTagInfos(tags);

            foreach (var tag in controllerTags)
                Console.WriteLine($"Id={tag.Id} Name={tag.Name} Type={tag.Type} Length={tag.Length}");
        }

        static TagInfo[] DecodeAllTagInfos(Tag tag)
        {
            var buffer = new List<TagInfo>();

            var tagSize = tag.GetSize();

            int offset = 0;
            while (offset < tagSize)
            {
                buffer.Add(DecodeOneTagInfo(tag, offset, out int elementSize));
                offset += elementSize;
            }

            return buffer.ToArray();
        }


        static TagInfo DecodeOneTagInfo(Tag tag, int offset, out int elementSize)
        {

            var tagInstanceId = tag.GetUInt32(offset);
            var tagType = tag.GetUInt16(offset + 4);
            var tagLength = tag.GetUInt16(offset + 6);
            var tagArrayDims = new uint[]
            {
                tag.GetUInt32(offset + 8),
                tag.GetUInt32(offset + 12),
                tag.GetUInt32(offset + 16)
            };

            var apparentTagNameLength = (int)tag.GetUInt16(offset + 20);
            var actualTagNameLength = Math.Min(apparentTagNameLength, TAG_STRING_SIZE * 2 - 1);

            var tagNameBytes = Enumerable.Range(offset + 22, actualTagNameLength)
                .Select(o => tag.GetUInt8(o))
                .Select(Convert.ToByte)
                .ToArray();

            var tagName = Encoding.ASCII.GetString(tagNameBytes);

            elementSize = 22 + actualTagNameLength;

            return new TagInfo()
            {
                Id = tagInstanceId,
                Type = tagType,
                Name = tagName,
                Length = tagLength,
                Dimensions = tagArrayDims
            };

        }
    }
}
