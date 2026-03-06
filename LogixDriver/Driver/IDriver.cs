using Logix.Tags;

namespace Logix.Driver
{
    public interface IDriver : IDisposable
    {
        public Task LoadTagsAsync(IEnumerable<string>? tagFilter = null);
        public void LoadTags(IEnumerable<string>? tagFilter = null);
        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitionsFlat();
        public IEnumerable<TagDefinition> GetTagDefinitions();
        public Target Target { get; }
        public bool IsConnected { get; }
        public string ControllerInfo { get; }
        public object? ReadTagValue(string tagName);
        public Task<object?> ReadTagValueAsync(string tagName);
        public void WriteTagValue(string tagName, object value);
        public Task WriteTagValueAsync(string tagName, object value);
        public bool TryConnect();
    }
}
