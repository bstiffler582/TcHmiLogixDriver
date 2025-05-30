using System;
using libplctag;
using libplctag.DataTypes;
using libplctag.DataTypes.Simple;

namespace ConsoleTest
{
    class LogixDriver
    {
        private string gateway = "";

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

        public void GetTags(string address, string path)
        {
            
            gateway = address;

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
                    Console.WriteLine($"Id={tag.Id} Name={tag.Name} Type={LogixTypes.ResolveTypeName(tag.Type)} Length={tag.Length} Dims={tag.Dimensions[0]}");

                Console.WriteLine();
                Console.WriteLine("Programs");
                Console.WriteLine("========");

                foreach (var tag in tagList.Value)
                {
                    if (TagIsProgram(tag, out string programTagListingPrefix))
                    {
                        using (var programTags = new Tag<TagInfoPlcMapper, TagInfo[]>
                        {
                            Gateway = gateway,
                            Path = path,
                            PlcType = PlcType.ControlLogix,
                            Protocol = Protocol.ab_eip,
                            Name = $"{programTagListingPrefix}.@tags",
                            Timeout = TimeSpan.FromSeconds(10)
                        })
                        {
                            programTags.Read();

                            Console.WriteLine(programTagListingPrefix);
                            foreach (var program in programTags.Value)
                                Console.WriteLine($"    Name={program.Name} Type={LogixTypes.ResolveTypeName(program.Type)}");
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("UDTs");
                Console.WriteLine("====");

                var uniqueUdtTypeIds = tagList.Value
                    .Where(tagInfo => LogixTypes.IsUdt(tagInfo.Type))
                    .Select(tagInfo => GetUdtId(tagInfo))
                    .Distinct();

                foreach (var udtId in uniqueUdtTypeIds)
                {
                    using (var udtTag = new Tag<UdtInfoPlcMapper, UdtInfo>
                    {
                        Gateway = gateway,
                        Path = path,
                        PlcType = PlcType.ControlLogix,
                        Protocol = Protocol.ab_eip,
                        Name = $"@udt/{udtId}",
                    })
                    {
                        udtTag.Read();

                        Console.WriteLine($"Id={udtTag.Value.Id} Name={udtTag.Value.Name} NumFields={udtTag.Value.NumFields} Size={udtTag.Value.Size}");
                        foreach (var f in udtTag.Value.Fields)
                            Console.WriteLine($"    Name={f.Name} Offset={f.Offset} Metadata={f.Metadata} Type={LogixTypes.ResolveTypeName(f.Type)}");
                    }
                }
            }
        }

        static int GetUdtId(TagInfo tag)
        {
            const ushort TYPE_UDT_ID_MASK = 0x0FFF;
            return tag.Type & TYPE_UDT_ID_MASK;
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
