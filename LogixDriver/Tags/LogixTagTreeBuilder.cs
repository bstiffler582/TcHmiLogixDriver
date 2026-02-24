using static Logix.LogixTypes;

namespace Logix.Tags
{
    public class LogixTagTreeBuilder
    {
        private readonly LogixTarget target;
        private readonly ILogixTagReader tagReader;

        public LogixTagTreeBuilder(LogixTarget target, ILogixTagReader tagReader)
        {
            this.target = target;
            this.tagReader = tagReader;
        }

        /// <summary>
        /// Resolves type and array definitions to populate child nodes.
        /// </summary>
        /// <param name="rootNode">Top level node to expand</param>
        /// <param name="deep">Recurse through nested members</param>
        /// <returns></returns>
        public void ExpandNode(TagDefinition rootNode, bool deep = true)
            => ExpandInternal(rootNode, deep);

        private void ExpandInternal(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.Deep)
                return;

            if (tagDef.ExpansionLevel == ExpansionLevel.Shallow && !deep)
                return;

            if (IsArray(tagDef.TypeCode))
                ExpandArray(tagDef, deep);
            else if (IsUdt(tagDef.TypeCode))
                ExpandUdt(tagDef, deep);
            else if (tagDef.Name.StartsWith("Program:"))
                ExpandProgram(tagDef, deep);
            else
            {
                ExpandPrimitive(tagDef);
                tagDef.ExpansionLevel = ExpansionLevel.Deep;
                return;
            }

            tagDef.ExpansionLevel = deep ? ExpansionLevel.Deep : ExpansionLevel.Shallow;
        }

        private void ExpandProgram(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.None)
            {
                var progTagInfos = tagReader.ReadProgramTags(target, tagDef.Name);

                var programTags = progTagInfos
                    .Where(t => !IsSystem(t.TypeCode))
                    .ToList();

                tagDef.Children = programTags;
            }

            if (deep)
            {
                foreach (var c in tagDef.Children!)
                    ExpandInternal(c, true);
            }
            
            tagDef.TypeName = "Program";
        }

        private void ExpandArray(TagDefinition tagDef, bool deep)
        {
            var baseTypeCode = GetArrayBaseType(tagDef.TypeCode);
            var baseTag = new TagDefinition(tagDef) { TypeCode = baseTypeCode };
            ExpandInternal(baseTag, deep);

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

        private void ExpandUdt(TagDefinition tagDef, bool deep)
        {
            if (tagDef.ExpansionLevel == ExpansionLevel.None)
            {
                var udtId = GetUdtId(tagDef.TypeCode);

                TypeDefinition typeDef;
                if (target.TryGetCachedTypeDefinition(udtId, out var cached) && cached is not null)
                    typeDef = cached;

                typeDef = tagReader.ReadUdtInfo(target, udtId);

                var tagMembers = typeDef.Members?
                    .Select(TagDefinition.FromTypeMemberDefinition)
                    .ToList();

                if (deep)
                {
                    foreach (var member in tagMembers!)
                        ExpandInternal(member, true);
                }

                if (cached is null)
                    target.CacheTypeDefinition(udtId, typeDef);

                tagDef.Length = typeDef.Length;
                tagDef.TypeName = typeDef.Name;
                tagDef.Children = tagMembers;
            }

            if (deep && tagDef.ExpansionLevel == ExpansionLevel.Shallow)
            {
                foreach (var child in tagDef.Children!)
                    ExpandInternal(child, true);
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
