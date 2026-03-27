using Logix.Driver;
using Logix;
using System;
using System.Collections.Generic;
using System.Linq;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using Logix.Tags;

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
        public static JsonSchemaValue BuildSymbolSchema(IDriver driver)
        {
            var typeCache = new HashSet<string>();

            var definitions = new Value();
            var properties = new Value();

            var tagDefinitions = driver.GetTagDefinitions();

            foreach (var tag in tagDefinitions)
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
            root.Add("allowMapping", false);

            var extractDefinitions = (tagDefinitions.Any());

            return new JsonSchemaValue(root, extractDefinitions);
        }

        // Builds type definition and instance schemas for tag browser, and TcHmi framework value resolutions
        // mutates definitions and cache reference parameters
        private static Value ResolveTypeDefinitionSchema(TagDefinition tag, Value definitions, HashSet<string> cache, string targetName)
        {
            // recurse through nested/complex types (arrays and UDTs)
            (string typeName, Value schema) InnerResolver(TagDefinition node)
            {
                if (node.ExpansionLevel != ExpansionLevel.Deep)
                {
                    var unresolved = new Value();
                    unresolved.Add("type", "object");
                    unresolved.Add("allowMapping", false);
                    unresolved.Add("hidden", true);
                    return (node.Name, unresolved);
                }

                if (TagMetaHelpers.IsArray(node.TypeCode))
                {
                    if (node.Children is null || node.Children.Count == 0)
                        throw new Exception($"Array type {node.Name} has no members to infer item type.");

                    var member = node.Children.First();

                    // build inner item schema and type name
                    var (innerTypeName, innerSchema) = InnerResolver(member);

                    var dims = node.Dimensions ?? [];
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
                else if (TagMetaHelpers.IsUdt(node.TypeCode) || node.Name.StartsWith("Program:"))
                {
                    if (node.TypeName == "STRING")
                        return ("String", new Value { { "$ref", "tchmi:general#/definitions/String" } });

                    var defName = $"{targetName}.{node.TypeName}";

                    if (!cache.Contains(defName))
                    {
                        var udtDef = new Value();
                        udtDef.Add("type", "object");

                        if (node.Name.StartsWith("Program:"))
                            udtDef.Add("allowMapping", false);

                        var udtMembers = new Value();

                        if (node.Children != null)
                        {
                            foreach (var member in node.Children)
                            {
                                var (_, memberSchema) = InnerResolver(member);
                                udtMembers.Add(member.Name, memberSchema);
                            }
                        }

                        udtDef.Add("properties", udtMembers);
                        definitions.Add(defName, udtDef);
                        cache.Add(defName);
                    }

                    return (node.TypeName, new Value { { "$ref", $"#/definitions/{defName}" } });
                }
                else
                {
                    var primName = node.TypeName;

                    // generate bool members for bitwise addressing
                    if (bitWidths.TryGetValue(primName, out var bitWidth))
                    {
                        var defName = $"{targetName}.{primName}";

                        if (!cache.Contains(defName))
                        {
                            var bitProps = new Value();
                            for (var i = 0; i < bitWidth; i++)
                                bitProps.Add(i.ToString(), new Value { { "$ref", "tchmi:general#/definitions/BOOL" } });

                            var bitObj = new Value();
                            bitObj.Add("type", "object");
                            bitObj.Add("properties", bitProps);

                            var anyOf = new Value();
                            anyOf.Add(new Value { { "$ref", $"tchmi:general#/definitions/{primName}" } });
                            anyOf.Add(bitObj);

                            var def = new Value();
                            def.Add("anyOf", anyOf);

                            definitions.Add(defName, def);
                            cache.Add(defName);
                        }

                        return (primName, new Value { { "$ref", $"#/definitions/{defName}" } });
                    }

                    // non-integer primitive (REAL, LREAL, BOOL, STRING, etc.)
                    return (primName, new Value { { "$ref", $"tchmi:general#/definitions/{primName}" } });
                }
            }

            var (typeName, schema) = InnerResolver(tag);
            return schema;
        }

        private static readonly Dictionary<string, int> bitWidths = new()
        {
            { "SINT",   8 }, { "USINT",  8 }, { "BYTE",   8 },
            { "INT",   16 }, { "UINT",  16 }, { "WORD",  16 },
            { "DINT",  32 }, { "UDINT", 32 }, { "DWORD", 32 },
            { "LINT",  64 }, { "ULINT", 64 }, { "LWORD", 64 },
        };
    }
}
