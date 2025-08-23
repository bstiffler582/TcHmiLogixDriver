using libplctag;
using System.Text.Json;
using System.IO;

namespace ConsoleTest
{
    public class LogixTarget
    {
        public string Name { get; }
        public string Gateway { get; }
        public string Path { get; }
        public PlcType PlcType { get; }
        public Protocol Protocol { get; }
        public int TimeoutMs { get; set; } = 5000;

        // map of tags
        private readonly Dictionary<string, TagDefinition> tags = new();
        public IReadOnlyDictionary<string, TagDefinition> Tags => tags;

        public LogixTarget(string Name, string Gateway, 
            string Path = "1,0", 
            PlcType PlcType = PlcType.ControlLogix, 
            Protocol Protocol = Protocol.ab_eip, 
            int TimeoutMs = 5000)
        {
            this.Name = Name;
            this.Gateway = Gateway;
            this.Path = Path;
            this.PlcType = PlcType;
            this.Protocol = Protocol;
            this.TimeoutMs = TimeoutMs;
        }

        bool TryGetTag(string name, out TagDefinition? tag) => tags.TryGetValue(name, out tag);

        public void AddTag(TagDefinition tag)
        {
            tags.Add(tag.Name, tag);

            //if (tag.Type.Members?.Count > 0)
            //{
            //    AddChildTags(tag, tag.Name, tags);
            //    //var children = tag.Type.Members?.SelectMany(m =>
            //    //{
            //    //    return m.Type.Members;
            //    //});
            //}
        }

        public void AddTags(IEnumerable<TagDefinition> tagList)
        {
            foreach (var tag in tagList)
            {
                if (!tags.ContainsKey(tag.Name))
                {
                    tags.Add(tag.Name, tag);
                }
            }
        }

        //private void AddChildTags(TagDefinition parent, string path, Dictionary<string, TagDefinition> children)
        //{
        //    if (parent.Type.Members is null) return;

        //    foreach (var m in parent.Type.Members)
        //    {
        //        string name;
        //        if (parent.Type.Name.Contains("ARRAY"))
        //            name = $"{path}[{m.Name}]";
        //        else
        //            name = $"{path}.{m.Name}";

        //        children.Add(name, m);
        //        if (m.Type.Members?.Count > 0 && m.Type.Name != "STRING")
        //            AddChildTags(m, name, children);
        //    }
        //}

        //public void Debug()
        //{
        //    var tags = JsonSerializer.Serialize(tagMap);
        //    File.WriteAllText("tags.json", tags);
        //    var udts = JsonSerializer.Serialize(udtIdMap);
        //    File.WriteAllText("udts.json", udts);
        //}
    }
}
