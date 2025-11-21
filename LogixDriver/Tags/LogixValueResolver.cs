using libplctag;

namespace Logix.Tags
{
    public interface ILogixValueResolver 
    {
        Type ValueType { get; }
        object ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
    }
    public interface ILogixValueResolver<T> : ILogixValueResolver
    {
        new T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
    }

    public abstract class LogixValueResolverBase<T> : ILogixValueResolver<T>
    {
        public Type ValueType => typeof(T);
        public abstract T ResolveValue(Tag tag, TagDefinition definition, int offset = 0);
        object ILogixValueResolver.ResolveValue(Tag tag, TagDefinition definition, int offset)
            => ResolveValue(tag, definition, offset) ?? default!;
    }

    public class LogixDefaultValueResolver : LogixValueResolverBase<object>
    {
        public override object ResolveValue(Tag tag, TagDefinition definition, int offset = 0)
        {
            if (LogixTypes.IsArray(definition.Type.Code))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new List<object>();
                foreach (var m in definition.Type.Members)
                    ret.Add(ResolveValue(tag, m, offset + (int)m.Offset));

                return ret;
            }
            else if (LogixTypes.IsUdt(definition.Type.Code) && !definition.Type.Name.Contains("STRING"))
            {
                if (definition.Type.Members is null || definition.Type.Members.Count < 1)
                    return 0;

                var ret = new Dictionary<string, object>();
                foreach (var m in definition.Type.Members)
                    ret[m.Name] = ResolveValue(tag, m, offset + (int)m.Offset);

                return ret;
            }
            else
            {
                return LogixTypes.PrimitiveValueResolver(tag, definition.Type.Code, offset);
            }
        }
    }
}