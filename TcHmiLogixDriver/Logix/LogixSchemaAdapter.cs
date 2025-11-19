using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using Logix;

namespace TcHmiLogixDriver.Logix
{
    public static class LogixSchemaAdapter
    {
        public static JsonSchemaValue BuildSymbolSchema(LogixTarget target)
        {
            var typeCache = new HashSet<string>();

            var definitions = new Value();
            var properties = new Value();

            foreach (var tag in target.TagDefinitions.Values)
            {
                if (tag.Name.StartsWith("__DEFVAL_"))
                    continue;

                var instance = ResolveTypeDefinitionSchema(tag, definitions, typeCache, target.Name);
                properties.Add(tag.Name, instance);
            }

            var root = new Value();
            root.Add("definitions", definitions);
            root.Add("properties", properties);
            root.Add("type", "object");

            var extractDefinitions = (target.TagDefinitions.Count > 0);

            return new JsonSchemaValue(root, extractDefinitions);
        }

        private static Value ResolveTypeDefinitionSchema(TagDefinition tag, Value definitions, HashSet<string> cache, string targetName)
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
                    ResolveTypeDefinitionSchema(tag.Type.Members[0], definitions, cache, targetName);
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

                            var memberDef = ResolveTypeDefinitionSchema(member, definitions, cache, targetName);
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
