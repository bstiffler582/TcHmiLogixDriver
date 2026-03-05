using static Logix.Tags.TagMetaHelpers;

namespace Logix
{
    public class TagDefinition
    {
        public string Name { get; set; }
        public ushort TypeCode { get; set; }
        public uint Length { get; set; }
        public uint Offset { get; set; }
        public uint BitOffset { get; set; }
        public string TypeName { get; set; }
        public uint[]? Dimensions { get; set; }
        public List<TagDefinition>? Children { get; set; }
        public ExpansionLevel ExpansionLevel { get; set; } = ExpansionLevel.None;

        public TagDefinition(
            string name,
            ushort typeCode,
            uint length,
            uint offset = 0,
            uint bitOffset = 0,
            string typeName = "",
            uint[]? dimensions = null,
            List<TagDefinition>? children = null)
        {
            Name = name;
            TypeCode = typeCode;
            Length = length;
            Offset = offset;
            BitOffset = bitOffset;
            TypeName = typeName;
            Dimensions = dimensions;
            Children = children;
        }
        public TagDefinition(TagDefinition tagDefinition)
        {
            Name = tagDefinition.Name;
            TypeCode = tagDefinition.TypeCode;
            Length = tagDefinition.Length;
            Offset = tagDefinition.Offset;
            BitOffset = tagDefinition.BitOffset;
            TypeName = tagDefinition.TypeName;
            Dimensions = tagDefinition.Dimensions;
            Children = tagDefinition.Children;
        }

        public static TagDefinition FromTypeMemberDefinition(TypeMemberDefinition typeMember)
        {
            return new TagDefinition(
                typeMember.Name,
                typeMember.Code,
                GetTypeLength(typeMember.Code),
                typeMember.Offset,
                typeMember.BitOffset,
                ResolveTypeName(typeMember.Code),
                [typeMember.Dimension]
            );
        }

        public bool IsArray => IsArray(TypeCode);
        public bool IsPrimitive => IsPrimitive(TypeCode);
        public int ElementCount()
        {
            if (Dimensions is null || Dimensions.Length == 0) return 1;
            long prod = 1;
            foreach (var d in Dimensions)
                prod = prod * Math.Max(1, (long)d);
            return (int)Math.Max(1, prod);
        }
    }

    public enum ExpansionLevel
    {
        None = 0,
        Shallow = 1,
        Deep = 2
    }
}
