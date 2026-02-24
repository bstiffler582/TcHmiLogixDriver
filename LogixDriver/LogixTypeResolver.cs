using Logix.Tags;
using static Logix.LogixTypes;

namespace Logix
{
    public class LogixTypeResolver
    {
        private readonly LogixTarget target;
        private readonly ILogixTagReader tagReader;

        public LogixTypeResolver(LogixTarget target, ILogixTagReader tagReader)
        {
            this.target = target;
            this.tagReader = tagReader;
        }

        /// <summary>
        /// Resolves custom and primitive types based on the TagInfo read from the CIP template object.
        /// </summary>
        /// <param name="tagInfo"></param>
        /// <param name="deep">Recurse through nested members</param>
        /// <returns></returns>
        public TagDefinition Resolve(TagDefinition tagDef, bool deep = true)
            => ResolveInternal(tagDef, deep);

        private TagDefinition ResolveInternal(TagDefinition tagDef, bool deep)
        {
            if (IsArray(tagDef.TypeCode))
                return ResolveArray(tagDef, deep);
            else if (IsUdt(tagDef.TypeCode))
                return ResolveUdt(tagDef, deep);
            else if (tagDef.Name.StartsWith("Program:"))
                return ResolveProgram(tagDef, deep);
            else
                return ResolvePrimitive(tagDef);
        }

        private TagDefinition ResolveProgram(TagDefinition tagDef, bool deep)
        {
            var progTagInfos = tagReader.ReadProgramTags(target, tagDef.Name);

            var progTags = progTagInfos
                .Where(t => !IsSystem(t.TypeCode))
                .Select(tag => (deep) ? ResolveInternal(tag, true) : tag)
                .ToList();

            return new TagDefinition(tagDef) { TypeName = "Program", Children = progTags };
        }

        private TagDefinition ResolveArray(TagDefinition tagDef, bool deep)
        {
            var baseTypeCode = GetArrayBaseType(tagDef.TypeCode);
            var baseTag = ResolveInternal(new TagDefinition(tagDef) { TypeCode = baseTypeCode }, deep);

            var dims = tagDef.Dimensions?.Where(n => n > 0).ToArray();
            return BuildArrayType(tagDef, baseTag, dims, 0, deep);
        }

        private TagDefinition BuildArrayType(TagDefinition rootTag, TagDefinition baseTag, uint[]? dims, int idx = 0, bool deep = true)
        {
            if (dims is null || idx >= dims.Length || dims[idx] == 0)
                return baseTag;

            var child = BuildArrayType(rootTag, baseTag, dims, idx + 1);
            var dim = (int)dims[idx];
            var members = Enumerable.Range(0, dim)
                .Select(i => new TagDefinition(child) 
                { 
                    Name = $"{i}", 
                    Offset = (uint)i * child.Length
                })
                .ToList();

            return new TagDefinition(
                child.Name,
                rootTag.TypeCode,
                (uint)dim * child.Length,
                rootTag.Offset,
                0,
                $"ARRAY[{dim}] OF {child.TypeName}",
                dims.Skip(idx).ToArray(),
                members
            );
        }

        private TagDefinition ResolveUdt(TagDefinition tagDef, bool deep)
        {
            var udtId = GetUdtId(tagDef.TypeCode);

            TypeDefinition typeDef;
            if (target.TryGetCachedTypeDefinition(udtId, out var cached) && cached is not null)
                typeDef = cached;

            typeDef = tagReader.ReadUdtInfo(target, udtId);
            var tagMembers = typeDef.Members?
                .Select(TagDefinition.FromTypeMemberDefinition);
            var members = (deep) ? tagMembers?.Select(m => ResolveInternal(m, true)) : tagMembers;

            if (cached is null)
                target.CacheTypeDefinition(udtId, typeDef);

            return new TagDefinition(tagDef) { Length = typeDef.Length, TypeName = typeDef.Name, Children = members?.ToList() };
        }

        private TagDefinition ResolvePrimitive(TagDefinition tagDef)
        {
            var length = (tagDef.Length > 0) ? tagDef.Length : GetTypeLength(tagDef.TypeCode);
            return new TagDefinition(tagDef) { Length = length, TypeName = ResolveTypeName(tagDef.TypeCode) };
        }
    }
}
