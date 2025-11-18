using System;
using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using LogixDriver;

namespace TcHmiLogixDriver.Logix
{
    
    public class LogixSymbolAdapter
    {
        public readonly string TargetName;
        public readonly IEnumerable<TagDefinition> Tags;
        public readonly Symbol Symbol;

        public LogixSymbolAdapter(string target, IEnumerable<TagDefinition> tags)
        {
            TargetName = target;
            Tags = tags;
            Symbol = new LogixSymbol(this);
        }

        public static JsonSchemaValue BuildSymbolSchema(LogixSymbolAdapter adapter)
        {
            var typeCache = new HashSet<string>();

            var definitions = new Value();
            var properties = new Value();
            var root = new Value();

            foreach (var tag in adapter.Tags)
            {
                if (tag.Name.StartsWith("__DEFVAL_"))
                    continue;

                var instance = ResolveTypeDefinition(tag, definitions, typeCache, adapter.TargetName);
                properties.Add(tag.Name, instance);
            }

            root.Add("definitions", definitions);
            root.Add("properties", properties);
            root.Add("type", "object");

            return new JsonSchemaValue(root, true);
        }

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

                    arrayDef.Add("maxItems", (int)tag.Type.Dims);
                    arrayDef.Add("minItems", (int)tag.Type.Dims);

                    definitions.Add(defName, arrayDef);
                    cache.Add(defName);
                }

                return new Value { { "$ref", $"#/definitions/{defName}" } };
            }
            else if (LogixTypes.IsUdt(tag.Type.Code) || tag.Name.StartsWith("Program:"))
            {
                if (tag.Type.Name == "STRING")
                    return new Value { { "$ref", "tchmi:general#/definitions/String" } };

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
