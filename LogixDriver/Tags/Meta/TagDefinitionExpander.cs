using static Logix.Tags.TagMetaHelpers;

namespace Logix.Tags
{
    /// <summary>
    /// Progressively builds a controller's tag tree based on what tags are requested.
    /// Defines TagDefinition parent->child relationships based on program/array/udt definitions.
    /// </summary>
    public class TagDefinitionExpander : ITagDefinitionExpander
    {
        private readonly ITagValueReader reader;
        private readonly ITagMetaDecoder decoder;
        private readonly ITagDefinitionCache tagCache;

        public TagDefinitionExpander(ITagValueReader reader, ITagDefinitionCache cache)
        {
            this.reader = reader;
            this.tagCache = cache;
            this.decoder = new TagMetaDecoder();
        }

        public TagDefinition ExpandTagDefinition(TagDefinition root, bool deep = true)
        {
            return ExpandTagDefinitionAsync(root, deep).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resolves type and array definitions to populate child nodes.
        /// </summary>
        /// <param name="rootNode">Top level node to expand</param>
        /// <param name="deep">Recurse through nested members</param>
        /// <returns>The root node back</returns>
        public async Task<TagDefinition> ExpandTagDefinitionAsync(TagDefinition root, bool deep = true)
        {
            await ExpandInternal(root, deep);
            return root;
        }

        private async Task ExpandInternal(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.Deep)
                return;

            if (tagDef.ExpansionLevel == ExpansionLevel.Shallow && !deep)
                return;

            if (IsArray(tagDef.TypeCode))
                await ExpandArray(tagDef, deep);
            else if (IsUdt(tagDef.TypeCode))
                await ExpandUdt(tagDef, deep);
            else if (tagDef.Name.StartsWith("Program:"))
                await ExpandProgram(tagDef, deep);
            else
            {
                ExpandPrimitive(tagDef);
                tagDef.ExpansionLevel = ExpansionLevel.Deep;
                return;
            }

            tagDef.ExpansionLevel = deep ? ExpansionLevel.Deep : ExpansionLevel.Shallow;
        }

        private async Task ExpandProgram(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.None)
            {
                var programMetaTag = await reader.ReadTagAsync($"{tagDef.Name}.@tags");
                var progTagInfos = decoder.DecodeProgramTags(programMetaTag!);

                var programTags = progTagInfos
                    .Where(t => !IsSystem(t.TypeCode))
                    .ToList();

                tagDef.Children = programTags;
            }

            if (deep)
            {
                foreach (var c in tagDef.Children!)
                    await ExpandInternal(c, true);
            }
            
            tagDef.TypeName = tagDef.Name;
        }

        private async Task ExpandArray(TagDefinition tagDef, bool deep)
        {
            var baseTypeCode = GetArrayBaseType(tagDef.TypeCode);
            var baseTag = new TagDefinition(tagDef) { TypeCode = baseTypeCode };
            await ExpandInternal(baseTag, deep);

            var dims = tagDef.Dimensions?.Where(n => n > 0).ToArray();
            var arrayNode = BuildArrayType(tagDef, baseTag, dims, 0);

            tagDef.Length = arrayNode.Length;
            tagDef.TypeName = arrayNode.TypeName;
            tagDef.Dimensions = arrayNode.Dimensions;
            tagDef.Children = arrayNode.Children;
        }

        private TagDefinition BuildArrayType(TagDefinition rootTag, TagDefinition baseTag, uint[]? dims, int idx = 0)
        {
            if (dims is null || idx >= dims.Length || dims[idx] == 0)
                return baseTag;

            var child = BuildArrayType(rootTag, baseTag, dims, idx + 1);
            var dim = (int)dims[idx];
            var members = Enumerable.Range(0, dim)
                .Select(i => new TagDefinition(child)
                {
                    Name = $"{i}",
                    Offset = (uint)i * child.Length,
                    ExpansionLevel = baseTag.ExpansionLevel
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

        private async Task ExpandUdt(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.None)
            {
                var udtId = GetUdtId(tagDef.TypeCode);

                TypeDefinition typeDef;
                if (tagCache.TryGetTypeDefinition(udtId, out var cached) && cached is not null)
                    typeDef = cached;

                var typeMetaTag = await reader.ReadTagAsync($"@udt/{udtId}");
                typeDef = decoder.DecodeUdtMeta(typeMetaTag!);

                var tagMembers = typeDef.Members?
                    .Select(TagDefinition.FromTypeMemberDefinition)
                    .ToList();

                if (deep)
                {
                    foreach (var member in tagMembers!)
                        await ExpandInternal(member, true);
                }

                if (cached is null)
                    tagCache.AddTypeDefinition(udtId, typeDef);

                tagDef.Length = typeDef.Length;
                tagDef.TypeName = typeDef.Name;
                tagDef.Children = tagMembers;
            }

            if (deep && tagDef.ExpansionLevel == ExpansionLevel.Shallow)
            {
                foreach (var child in tagDef.Children!)
                    await ExpandInternal(child, true);
            }
        }

        private void ExpandPrimitive(TagDefinition tagDef)
        {
            var length = tagDef.Length > 0 ? tagDef.Length : GetTypeLength(tagDef.TypeCode);
            tagDef.Length = length;
            tagDef.TypeName = ResolveTypeName(tagDef.TypeCode);
            tagDef.Children = null;
        }
    }
}
