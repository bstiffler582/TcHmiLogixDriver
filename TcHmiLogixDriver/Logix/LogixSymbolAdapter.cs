using System;
using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbol : Symbol
    {
        private IEnumerable<TagDefinition> tags;
        public LogixSymbol(LogixSymbolAdapter adapter) : base(LogixSymbolAdapter.BuildSchema(adapter))
        {
            tags = adapter.Tags;
        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            throw new NotImplementedException();
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }

    public class LogixSymbolAdapter
    {
        public readonly string TargetName;
        public readonly IEnumerable<TagDefinition> Tags;
        public Symbol Symbol => new LogixSymbol(this);

        public LogixSymbolAdapter(string target, IEnumerable<TagDefinition> tags)
        {
            TargetName = target;
            Tags = tags;
        }

        public static JsonSchemaValue BuildSchema(LogixSymbolAdapter adapter)
        {
            var typeCache = new HashSet<string>();

            var definitions = new Value();
            var properties = new Value();

            foreach(var tag in adapter.Tags)
            {
                var instance = ResolveTypeDefinition(tag, definitions, typeCache, adapter.TargetName);
                properties.Add(tag.Name, instance);
            }

            var root = new Value();
            root.Add("type", "object");
            root.Add("properties", properties);
            root.Add("definitions", definitions);

            return new JsonSchemaValue(root);
        }

        // recursive type definition resolver
        private static Value ResolveTypeDefinition(TagDefinition tag, Value definitions, HashSet<string> cache, string targetName)
        {
            if (LogixTypes.IsArray(tag.Type.Code))
            {
                // get array base type name
                var baseTypeName = tag.Type.Members[0].Type.Name;

                // primitive vs UDT type name resolution
                var itemTypeName = (LogixTypes.IsUdt(tag.Type.Members[0].Type.Code) ?
                    $"#/definitions/{targetName}.{baseTypeName}" :
                    $"tchmi:general#/definitions/{baseTypeName}");

                // account for if array base type is unresolved udt
                if (LogixTypes.IsUdt(tag.Type.Members[0].Type.Code) && !cache.Contains(itemTypeName))
                {
                    ResolveTypeDefinition(tag.Type.Members[0], definitions, cache, targetName);
                }

                // array instance definition name
                var defName = $"{targetName}.ARRAY_0..{tag.Type.Dims - 1}_OF-{baseTypeName}";

                if (!cache.Contains(defName))
                {
                    var arrayDef = new Value();
                    arrayDef.Add("type", "array");
                    var items = new Value();

                    items.Add("$ref", itemTypeName);
                    arrayDef.Add("items", items);

                    arrayDef.Add("maxItems", tag.Type.Dims);
                    arrayDef.Add("minItems", tag.Type.Dims);

                    definitions.Add(defName, arrayDef);
                    cache.Add(defName);
                }

                return new Value { { "$ref", $"#/definitions/{defName}" } };
            }
            else if (LogixTypes.IsUdt(tag.Type.Code) || tag.Name.StartsWith("Program:"))
            {
                if (tag.Type.Name == "STRING")
                    return new Value { { "$ref", $"#/definitions/STRING(80)" } };

                var defName = $"{targetName}.{tag.Type.Name}";

                if (!cache.Contains(defName))
                {
                    // resolve UDT type / instance
                    var udtDef = new Value();
                    udtDef.Add("type", "object");

                    if (tag.Name.StartsWith("Program:"))
                        udtDef.Add("allowMapping", false);

                    var udtMembers = new Value();

                    if (tag.Type.Members != null)
                    {
                        foreach (var member in tag.Type.Members)
                        {
                            if (member.Name.StartsWith("ZZZZZZZZZZ"))
                                continue;

                            var memberDef = ResolveTypeDefinition(member, definitions, cache, targetName);
                            udtMembers.Add(member.Name, memberDef);
                        }
                    }

                    udtDef.Add("properties", udtMembers);
                    definitions.Add(defName, udtDef);
                    cache.Add(defName);
                }

                return new Value { { "$ref", $"#/definitions/{defName}" } };
            }
            else
            {
                // primitive type
                return new Value { { "$ref", $"tchmi:general#/definitions/{tag.Type.Name}" } };
            }
        }
    }
}
