using libplctag.DataTypes;
using Logix;

namespace Logix.Tags
{
    public interface ILogixTagLoader
    {
        IEnumerable<TagDefinition> LoadTags(LogixTarget target , ILogixTagReader reader);
    }

    public class LogixTagLoader : ILogixTagLoader
    {

        public IEnumerable<TagDefinition> LoadTags(LogixTarget target, ILogixTagReader reader)
        {
            var typeCache = new Dictionary<ushort, TypeDefinition>();

            // partial function to read UDT info
            Func<ushort, UdtInfo> udtInfoReader = (udtId) =>
                reader.ReadUdtInfo(target, udtId);

            var tagInfos = reader.ReadTagList(target);

            var controllerTags = tagInfos
                .Where(tag =>
                    !tag.Name.StartsWith("Program:") &&
                    !LogixTypes.ResolveTypeName(tag.Type).Contains("SystemType"))
                .Select(tag => new TagDefinition(tag.Name, LogixTypes.TypeResolver(tag, typeCache, udtInfoReader)))
                .ToList();

            var programTags = tagInfos
                .Where(tag => tag.Name.StartsWith("Program:"))
                .Select(tag =>
                {
                    var progTagInfos = reader.ReadProgramTags(target, tag.Name);
                    var progTags = progTagInfos
                        .Where(t => !LogixTypes.ResolveTypeName(t.Type).Contains("SystemType"))
                        .Select(tag => new TagDefinition(tag.Name, LogixTypes.TypeResolver(tag, typeCache, udtInfoReader)))
                        .ToList();
                    return new TagDefinition(tag.Name, new TypeDefinition(tag.Type, tag.Length, tag.Name, 0, progTags));
                })
                .ToList();

            return programTags.Concat(controllerTags);
        }
    }
}