using libplctag;
using System.Text;
using static Logix.LogixTypes;

namespace Logix.Tags
{
    public static class LogixTagDecoder
    {
        const int TAG_STRING_SIZE = 200;

        public static TagDefinition Decode(Tag tag, int offset, out int elementSize)
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

            return new TagDefinition(tagName, tagType, tagLength, (uint)offset, 0, ResolveTypeName(tagType), tagArrayDims);
        }

        public static TypeDefinition DecodeUdt(Tag tag)
        {

            var template_id = tag.GetUInt16(0);
            var member_desc_size = tag.GetUInt32(2);
            var udt_instance_size = tag.GetUInt32(6);
            var num_members = tag.GetUInt16(10);
            var struct_handle = tag.GetUInt16(12);

            var udtInfo = new UdtInfo()
            {
                Fields = new UdtFieldInfo[num_members],
                NumFields = num_members,
                Handle = struct_handle,
                Id = template_id,
                Size = udt_instance_size
            };

            var offset = 14;

            for (int field_index = 0; field_index < num_members; field_index++)
            {
                ushort field_metadata = tag.GetUInt16(offset);
                offset += 2;

                ushort field_element_type = tag.GetUInt16(offset);
                offset += 2;

                ushort field_offset = tag.GetUInt16(offset);
                offset += 4;

                var field = new UdtFieldInfo()
                {
                    Offset = field_offset,
                    Metadata = field_metadata,
                    Type = field_element_type,
                };

                udtInfo.Fields[field_index] = field;
            }

            var name_str = tag.GetString(offset).Split(';')[0];
            udtInfo.Name = name_str;

            offset += tag.GetStringTotalLength(offset);

            for (int field_index = 0; field_index < num_members; field_index++)
            {
                udtInfo.Fields[field_index].Name = tag.GetString(offset);
                offset += tag.GetStringTotalLength(offset);
            }

            var members = udtInfo.Fields.Select(m =>
            {
                var bitOffset = (m.Type == (ushort)Code.BOOL) ? m.Metadata : 0;
                var dimension = IsArray(m.Type) ? m.Metadata : 0;
                return new TypeMemberDefinition(m.Type, m.Name, m.Offset, (ushort)dimension, (ushort)bitOffset);
            })
            .ToList();

            return new TypeDefinition(udtInfo.Id, udtInfo.Size, udtInfo.Name, members);
        }
    }

    internal class UdtFieldInfo
    {
        public string Name { get; set; } = "";
        public ushort Type { get; set; }
        public ushort Metadata { get; set; }
        public uint Offset { get; set; }
    }

    internal class UdtInfo
    {
        public uint Size { get; set; }
        public string Name { get; set; } = "";
        public ushort Id { get; set; }
        public ushort NumFields { get; set; }
        public ushort Handle { get; set; }
        public UdtFieldInfo[] Fields { get; set; } = new UdtFieldInfo[0];
    }
}
