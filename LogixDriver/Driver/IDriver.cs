using Logix.Tags;

namespace Logix.Driver
{
    public interface IDriver
    {
        public Task LoadTagsAsync(IEnumerable<string> tagFilter);
        public void LoadTags(IEnumerable<string> tagFilter);
        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat();
        public IEnumerable<TagDefinition> GetTagDefinitions();
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; }
        public bool IsConnected { get; }
        public string ReadControllerInfo();
        public object? ReadTagValue(string tagName);
        public Task<object?> ReadTagValueAsync(string tagName);
        public void WriteTagValue(string tagName, object value);
        public Task WriteTagValueAsync(string tagName, object value);
    }
}
