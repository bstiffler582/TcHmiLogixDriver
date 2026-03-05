namespace Logix.Tags
{
    public interface ITagDefinitionExpander
    {
        TagDefinition ExpandTagDefinition(TagDefinition root, bool deep = true);
        Task<TagDefinition> ExpandTagDefinitionAsync(TagDefinition root, bool deep = true);
    }
}
