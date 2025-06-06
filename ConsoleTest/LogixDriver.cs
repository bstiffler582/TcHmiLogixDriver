using libplctag;
using libplctag.DataTypes;
using System;
using System.IO;

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
        private TypeDefinition ResolveType(TypeDefinition type, LogixTarget target)
        {
            // TODO: only 1D arrays supported
            if (LogixTypes.IsArray(type.Code))
            {
                var baseTypeCode = LogixTypes.GetArrayBaseType(type.Code);
                var baseType = ResolveType(new TypeDefinition("", baseTypeCode), target);
                var members = Enumerable.Range(0, (int)type.Dims)
                    .Select(m => new TagDefinition($"{m}", baseType))
                    .ToList();

                return new TypeDefinition($"ARRAY OF {baseType.Name}", baseType.Code, type.Dims, members);
            }
            else if (LogixTypes.IsUdt(type.Code))
            {
                var udtId = LogixTypes.GetUdtId(type.Code);

                // get from cache if definition is already resolved
                if (target.TryGetUdtDef(udtId, out TypeDefinition? typeDef) && typeDef != null)
                    return typeDef;

                // read udt definition
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
                    var members = new List<TagDefinition>();
                    foreach (var m in udtTag.Value.Fields)
                    {
                        var mType = ResolveType(new TypeDefinition("", m.Type, m.Metadata), target);
                        members.Add(new TagDefinition(m.Name, mType));
                    }

                    var ret = new TypeDefinition(udtTag.Value.Name, type.Code, type.Dims, members);
                    target.AddUdtDef(udtId, ret);
                    return ret;
                }
            }
            else
            {
                // primitive or unknown type
                return new TypeDefinition(LogixTypes.ResolveTypeName(type.Code), type.Code);
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
                    TagDefinition tagDef;
                    var type = ResolveType(new TypeDefinition("", tag.Type, tag.Dimensions[0]), target);

                    if (type.Name.Contains("SystemType"))
                    {
                        if (tag.Name.StartsWith("Program:"))
                        {
                            // load program tags as child members
                            var progType = new TypeDefinition(type.Name, type.Code, 0, GetProgramTags(tag.Name, target));
                            var progTag = new TagDefinition(tag.Name, progType);
                            tagDef = progTag;
                        }
                        else continue;
                    }
                    else
                    {
                        tagDef = new TagDefinition(tag.Name, type);
                    }

                    target.AddTag(tagDef);
                    Console.WriteLine($"Name={tagDef.Name} Type={tagDef.Type.Name}");
                    Print(tagDef, tagDef.Name, 1);
                }

                target.Debug();
            }
        }

        private List<TagDefinition> GetProgramTags(string program, LogixTarget target)
        {
            using (var programTags = new Tag<TagInfoPlcMapper, TagInfo[]>
            {
                Gateway = target.Gateway,
                Path = target.Path,
                PlcType = target.PlcType,
                Protocol = target.Protocol,
                Name = $"{program}.@tags",
                Timeout = TimeSpan.FromMilliseconds(5000)
            })
            {
                programTags.Read();

                var members = new List<TagDefinition>();
                foreach(var programTag in programTags.Value)
                {
                    var type = ResolveType(new TypeDefinition("", programTag.Type, programTag.Dimensions[0]), target);
                    if (type.Name.Contains("SystemType")) continue;
                    var tagDef = new TagDefinition(programTag.Name, type);
                    members.Add(tagDef);
                }

                return members;
            }
        }

        private void Print(TagDefinition parent, string path, int level)
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
    }
}
