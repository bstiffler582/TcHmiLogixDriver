using Logix;
using System;
using System.Collections.Generic;
using System.Linq;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix.Symbols
{
    public static class LogixSchemaAdapter
    {
        /// <summary>
        /// Generates the JSON schema that represents all the types and members
        /// of the PLC server symbol. This is the information the framework uses to
        /// render the tag browser, create mappings, resolve types, etc.
        /// </summary>
        /// <param name="driver"></param>
        /// <returns></returns>
        public static JsonSchemaValue BuildSymbolSchema(LogixDriver driver)
        {
            var typeCache = new HashSet<string>();

            var definitions = new Value();
            var properties = new Value();

            foreach (var tag in driver.Target.TagDefinitions.Values)
            {
                if (tag.Name.StartsWith("__DEFVAL_"))
                    continue;

                var instance = ResolveTypeDefinitionSchema(tag, definitions, typeCache, driver.Target.Name);
                properties.Add(tag.Name, instance);
            }

            var root = new Value();
            root.Add("definitions", definitions);
            root.Add("properties", properties);
            root.Add("type", "object");

            var extractDefinitions = (driver.Target.TagDefinitions.Count > 0);

            return new JsonSchemaValue(root, extractDefinitions);
        }

        // Builds type definition and instance schemas for tag browser, and TcHmi framework value resolutions
        // mutates definitions reference parameter
        private static Value ResolveTypeDefinitionSchema(TagDefinition tag, Value definitions, HashSet<string> cache, string targetName)
        {
            // recurse through nested/complex types (arrays and UDTs)
            (string typeName, Value schema) InnerResolver(TagDefinition node)
            {
                if (LogixTypes.IsArray(node.Type.Code))
                {
                    if (node.Type.Members is null || node.Type.Members.Count == 0)
                        throw new Exception($"Array type {node.Name} has no members to infer item type.");

                    var member = node.Type.Members.First();

                    // build inner item schema and type name
                    var (innerTypeName, innerSchema) = InnerResolver(member);

                    var dims = node.Type.Dimensions ?? Array.Empty<uint>();
                    var currentDim = dims.Length > 0 ? (int)dims[0] : 1;
                    var arrTypeName = $"ARRAY_0..{currentDim - 1}_OF-{innerTypeName}";

                    // full definition name
                    var fullDefName = $"{targetName}.{arrTypeName}";

                    if (!cache.Contains(fullDefName))
                    {
                        var arrayDef = new Value();

                        arrayDef.Add("type", "array");
                        arrayDef.Add("items", innerSchema);
                        arrayDef.Add("maxItems", currentDim);
                        arrayDef.Add("minItems", currentDim);

                        definitions.Add(fullDefName, arrayDef);
                        cache.Add(fullDefName);
                    }

                    return (arrTypeName, new Value { { "$ref", $"#/definitions/{fullDefName}" } });
                }
                else if (LogixTypes.IsUdt(node.Type.Code) || node.Name.StartsWith("Program:"))
                {
                    if (node.Type.Name == "STRING")
                        return ("String", new Value { { "$ref", "tchmi:general#/definitions/String" } });

                    var defName = $"{targetName}.{node.Type.Name}";

                    if (!cache.Contains(defName))
                    {
                        var udtDef = new Value();
                        udtDef.Add("type", "object");

                        if (node.Name.StartsWith("Program:"))
                            udtDef.Add("allowMapping", false);

                        var udtMembers = new Value();

                        if (node.Type.Members != null)
                        {
                            foreach (var member in node.Type.Members)
                            {
                                var (_, memberSchema) = InnerResolver(member);
                                udtMembers.Add(member.Name, memberSchema);
                            }
                        }

                        udtDef.Add("properties", udtMembers);
                        definitions.Add(defName, udtDef);
                        cache.Add(defName);
                    }

                    return (node.Type.Name, new Value { { "$ref", $"#/definitions/{defName}" } });
                }
                else
                {
                    // primitive type
                    var primName = node.Type.Name;
                    return (primName, new Value { { "$ref", $"tchmi:general#/definitions/{primName}" } });
                }
            }

            var result = InnerResolver(tag);
            return result.schema;
        }
    }
}
