using libplctag;
using libplctag.DataTypes;
using System;

namespace ConsoleTest
{
    public class LogixDriver
    {
        private static LogixDriver? instance;
        public static LogixDriver Instance
        {
            get
            {
                if (instance == null)
                    instance = new LogixDriver();
                return instance;
            }
        }

        // recursively resolve type definitions
        private TypeDef resolveType(TypeDef type, LogixTarget target)
        {
            // TODO: only 1D arrays supported
            if (LogixTypes.IsArray(type.Code))
            {
                var baseTypeCode = (ushort)(type.Code & ~0x2000);
                var baseType = resolveType(new TypeDef("", baseTypeCode), target);
                var members = Enumerable.Range(0, (int)type.Dims)
                    .Select(m => new TagDef($"{m}", baseType))
                    .ToList();

                return new TypeDef($"ARRAY OF {baseType.Name}", baseType.Code, type.Dims, members);
            }
            else if (LogixTypes.IsUdt(type.Code))
            {
                var udtId = LogixTypes.GetUdtId(type.Code);

                // get from cache if definition is already resolved
                if (target.TryGetUdtDef(udtId, out TypeDef? typeDef) && typeDef != null)
                    return typeDef;

                // read udt def
                using (var udtTag = new Tag<UdtInfoPlcMapper, UdtInfo>
                {
                    Gateway = target.Gateway,
                    Path = target.Path,
                    PlcType = target.PlcType,
                    Protocol = target.Protocol,
                    Name = $"@udt/{udtId}",
                })
                {
                    udtTag.Read();

                    // resolve udt members
                    var members = new List<TagDef>();
                    foreach (var m in udtTag.Value.Fields)
                    {
                        var mType = resolveType(new TypeDef("", m.Type, m.Metadata), target);
                        members.Add(new TagDef(m.Name, mType));
                    }

                    var ret = new TypeDef(udtTag.Value.Name, type.Code, type.Dims, members);
                    target.AddUdtDef(udtId, ret);
                    return ret;
                }
            }
            else
            {
                // primitive or unknown type
                return new TypeDef(LogixTypes.ResolveTypeName(type.Code), type.Code);
            }
        }

        public void GetTags(string address, string path)
        {
            var target = new LogixTarget("Test", address, path, PlcType.ControlLogix, Protocol.ab_eip);

            using (var tagList = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = address,
                Path = path,
                PlcType = PlcType.ControlLogix,
                Protocol = Protocol.ab_eip,
                Name = "@tags",
                Timeout = TimeSpan.FromMilliseconds(5000),
            })
            {
                tagList.Read();

                foreach (var tag in tagList.Value)
                {
                    var type = resolveType(new TypeDef(LogixTypes.ResolveTypeName(tag.Type), tag.Type, tag.Dimensions[0]), target);
                    var tagDef = new TagDef(tag.Name, type);

                    if (tagDef.Type.Name.Contains("Unknown"))
                    {
                        if (tagDef.Name.StartsWith("Program:"))
                        {
                            // TODO
                        }
                        else continue;
                    }

                    target.AddTag(tagDef);
                    Console.WriteLine($"Name={tagDef.Name} Type={tagDef.Type.Name}");
                    Print(tagDef, tagDef.Name, 1);
                }

                target.Debug();
            }
        }

        private void GetProgramTags(string programName)
        {

        }

        private void Print(TagDef parent, string path, int level)
        {
            if (parent.Type.Members is null) return;

            var space = new String(' ', (level * 2));
            foreach(var m in parent.Type.Members)
            {
                string name;
                if (parent.Type.Name.Contains("ARRAY"))
                    name = $"{path}[{m.Name}]";
                else
                    name = $"{path}.{m.Name}";

                Console.WriteLine($"{space}Name={name} Type={m.Type.Name}");
                if (m.Type.Members?.Count > 0 && m.Type.Name != "STRING")
                    Print(m, name, level + 1);
            }
        }

        static bool TagIsProgram(TagInfo tag, out string prefix)
        {
            if (tag.Name.StartsWith("Program:"))
            {
                prefix = tag.Name;
                return true;
            }
            else
            {
                prefix = string.Empty;
                return false;
            }
        }
    }
}
