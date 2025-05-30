using libplctag;
using libplctag.DataTypes;
using System;

namespace ConsoleTest
{
    public class LogixDriver
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

        // recursively resolve type definitions
        private TypeDef resolveType(TypeDef type, LogixTarget target)
        {
            // TODO: only 1D arrays supported
            if (LogixTypes.IsArray(type.Code))
            {
                var baseTypeCode = (ushort)(type.Code & ~0x2000);
                var baseType = resolveType(new TypeDef("", baseTypeCode, new List<TagDef>()), target);
                var members = Enumerable.Range(0, (int)type.Dims)
                    .Select(m => new TagDef($"{m}", baseType))
                    .ToList();

                return new TypeDef($"ARRAY OF {baseType.Name}", baseType.Code, members);
            }
            else if (LogixTypes.IsUdt(type.Code))
            {
                var udtId = LogixTypes.GetUdtId(type.Code);

                // type def already resolved
                if (target.TryGetUdtDef(udtId, out TypeDef? typeDef) && typeDef != null)
                    return typeDef;

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

                    var members = new List<TagDef>();
                    foreach (var m in udtTag.Value.Fields)
                    {
                        var mType = resolveType(new TypeDef("", m.Type, new List<TagDef>(), m.Metadata), target);
                        members.Add(new TagDef(m.Name, mType));
                    }

                    var ret = new TypeDef(udtTag.Value.Name, type.Code, members);
                    target.AddUdtDef(udtId, ret);
                    return ret;
                }
            }
            else
            {
                return new TypeDef(LogixTypes.ResolveTypeName(type.Code), type.Code, new List<TagDef>());
            }
        }

        public void GetTags(string address, string path)
        {
            var target = new LogixTarget("Test", address, path, PlcType.ControlLogix, Protocol.ab_eip);
            
            var gateway = address;

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
                    // TODO: Program Tags
                    var type = resolveType(new TypeDef(LogixTypes.ResolveTypeName(tag.Type), tag.Type, new List<TagDef>(), tag.Dimensions[0]), target);
                    var tagDef = new TagDef(tag.Name, type);
                    Console.WriteLine($"Name={tagDef.Name} Type={tagDef.Type.Name}");
                    Print(tagDef.Name, tagDef.Type.Members, 1);
                }
            }
        }

        private void Print(string parent, List<TagDef> members, int level)
        {
            var space = new String(' ', (level * 2));
            foreach(var m in members)
            {
                Console.WriteLine($"{space}Name={parent}.{m.Name} Type={m.Type.Name}");
                if (m.Type.Members.Count > 0)
                    Print($"{parent}.{m.Name}", m.Type.Members, level + 1);
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
