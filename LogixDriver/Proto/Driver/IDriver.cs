namespace Logix.Proto
{
    public interface IDriver
    {
        public Task LoadTagsAsync(IEnumerable<string> tagFilter);
        public void LoadTags(IEnumerable<string> tagFilter);
        public IReadOnlyDictionary<string, TagDefinition> GetTagDefinitions();
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; }
        public bool IsConnected { get; }
        public string ReadControllerInfo();
        public object? ReadTagValue(string tagName);
        public Task<object?> ReadTagValueAsync(string tagName);
        public bool WriteTagValue(string tagName, object value);
        public Task WriteTagValueAsync(string tagName, object value);
    }
}
