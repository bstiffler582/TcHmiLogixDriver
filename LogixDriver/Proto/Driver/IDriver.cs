namespace Logix.Proto
{
    public interface IDriver : ITagValueReaderWriter
    {
        public IEnumerable<TagDefinition> GetTagDefinitions();
        public Target Target { get; }
        public ITagValueResolver ValueResolver { get; set; }
        public ITagMetaProvider MetaProvider { get; set; }
        public bool IsConnected { get; }
        public string ReadControllerInfo();
    }
}
