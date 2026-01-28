using Logix.Tags;
using static Logix.LogixTypes;

namespace Logix
{
    public class LogixTypeResolver
    {
        private readonly Dictionary<ushort, TypeDefinition> typeCache;
        private readonly LogixTarget target;
        private readonly ILogixTagReader tagReader;

        public LogixTypeResolver(LogixTarget target, ILogixTagReader tagReader, Dictionary<ushort, TypeDefinition>? cache = null)
        {
            this.target = target;
            this.tagReader = tagReader;
            typeCache = cache ?? new Dictionary<ushort, TypeDefinition>();
        }

        /// <summary>
        /// Resolves custom and primitive types based on the TagInfo read from the CIP template object.
        /// </summary>
        /// <param name="tagInfo"></param>
        /// <param name="deep">Recurse through nested types</param>
        /// <returns></returns>
        public TypeDefinition Resolve(TagInfo tagInfo, bool deep = true)
            => ResolveInternal(tagInfo, deep);

        private TypeDefinition ResolveInternal(TagInfo tagInfo, bool deep)
        {
            if (IsArray(tagInfo.Type))
                return ResolveArray(tagInfo, deep);
            else if (IsUdt(tagInfo.Type))
                return ResolveUdt(tagInfo, deep);
            else
                return ResolvePrimitive(tagInfo);
        }

        private TypeDefinition ResolveArray(TagInfo tagInfo, bool deep)
        {
            var baseTypeCode = GetArrayBaseType(tagInfo.Type);
            var baseType = ResolveInternal(new TagInfo { Type = baseTypeCode }, deep);

            var dims = tagInfo.Dimensions.Where(n => n > 0).ToArray();
            return BuildArrayType(tagInfo.Type, baseType, dims);
        }

        private TypeDefinition BuildArrayType(ushort typeCode, TypeDefinition baseType, uint[] dims, int idx = 0)
        {
            if (idx >= dims.Length || dims[idx] == 0)
                return baseType;

            var child = BuildArrayType(typeCode, baseType, dims, idx + 1);
            var dim = (int)dims[idx];
            var members = Enumerable.Range(0, dim)
                .Select(i => new TagDefinition($"{i}", child, (uint)i * child.Length))
                .ToList();

            return new TypeDefinition(
                typeCode,
                (uint)dim * child.Length,
                $"ARRAY[{dim}] OF {child.Name}",
                dims.Skip(idx).ToArray(),
                members
            );
        }

        private TypeDefinition ResolveUdt(TagInfo tagInfo, bool deep)
        {
            var udtId = GetUdtId(tagInfo.Type);
            if (typeCache.TryGetValue(udtId, out var cached))
                return cached;

            var udtInfo = tagReader.ReadUdtInfo(target, udtId);
            var members = deep
                ? ResolveMembersDeep(udtInfo)
                : ResolveMembersShallow(udtInfo);

            var udtDef = new TypeDefinition(tagInfo.Type, udtInfo.Size, udtInfo.Name, Array.Empty<uint>(), members);
            typeCache.Add(udtId, udtDef);

            return udtDef;
        }

        private List<TagDefinition> ResolveMembersDeep(UdtInfo udtInfo)
        {
            return udtInfo.Fields
                .Select(m =>
                {
                    var memberInfo = new TagInfo
                    {
                        Type = m.Type,
                        Dimensions = IsArray(m.Type) ? [m.Metadata] : [0]
                    };
                    var bitOffset = (m.Type == (ushort)Code.BOOL) ? m.Metadata : 0;
                    return new TagDefinition(m.Name, ResolveInternal(memberInfo, deep: true), m.Offset, (uint)bitOffset);
                }).ToList();
        }

        private List<TagDefinition> ResolveMembersShallow(UdtInfo udtInfo)
        {
            return udtInfo.Fields
                .Select(m =>
                {
                    var bitOffset = (m.Type == (ushort)Code.BOOL) ? m.Metadata : 0;
                    var memberType = new TypeDefinition(m.Type, GetTypeLength(m.Type), ResolveTypeName(m.Type));
                    return new TagDefinition(m.Name, memberType, m.Offset, (uint)bitOffset);
                }).ToList();
        }

        private TypeDefinition ResolvePrimitive(TagInfo tagInfo)
        {
            var length = (tagInfo.Length > 0) ? tagInfo.Length : GetTypeLength(tagInfo.Type);
            return new TypeDefinition(tagInfo.Type, length, ResolveTypeName(tagInfo.Type));
        }
    }
}
