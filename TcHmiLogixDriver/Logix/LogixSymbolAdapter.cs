using System;
using System.Collections.Generic;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbol : Symbol
    {
        public LogixSymbol(LogixSymbolAdapter adapter) : base(LogixSymbolAdapter.BuildSchema(adapter))
        {
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

        // Build an inline schema (no $ref). Inline schemas avoid the internal JsonSchemaContainer requirement.
        public static JsonSchemaValue BuildSchema(LogixSymbolAdapter adapter)
        {
            var properties = new Value();

            foreach (var tag in adapter.Tags)
            {
                var instance = ResolveTagDefinition(tag);
                properties.Add(tag.Name, instance);
            }

            var root = new Value();
            root.Add("type", "object");
            root.Add("allowMapping", false);
            root.Add("properties", properties);

            // No $ref in the produced Value -> safe to create JsonSchemaValue from the Value/JsonValue
            return new JsonSchemaValue(root);
        }

        private static Value ResolveTagDefinition(TagDefinition tag)
        {
            if (LogixTypes.IsArray(tag.Type.Code))
            {
                // array base member type is in tag.Type.Members[0]
                var baseMember = tag.Type.Members?[0];
                var itemSchema = baseMember != null ? ResolveTagDefinition(baseMember) : new Value { { "type", "object" } };

                var arrayDef = new Value();
                arrayDef.Add("type", "array");
                arrayDef.Add("items", itemSchema);
                // ensure fixed-length arrays when Dims is set
                if (tag.Type.Dims > 0)
                {
                    arrayDef.Add("maxItems", (int)tag.Type.Dims);
                    arrayDef.Add("minItems", (int)tag.Type.Dims);
                }
                return arrayDef;
            }
            else if (LogixTypes.IsUdt(tag.Type.Code) || tag.Name.StartsWith("Program:"))
            {
                // STRING UDT special-case
                if (tag.Type.Name == "STRING")
                {
                    var s = new Value();
                    s.Add("type", "string");
                    s.Add("maxLength", 80);
                    return s;
                }

                var obj = new Value();
                obj.Add("type", "object");

                var props = new Value();

                if (tag.Type.Members != null)
                {
                    foreach (var member in tag.Type.Members)
                    {
                        if (member.Name.StartsWith("ZZZZZZZZZZ"))
                            continue;

                        props.Add(member.Name, ResolveTagDefinition(member));
                    }
                }

                obj.Add("properties", props);

                // preserve allowMapping hint for program-scoped containers
                if (tag.Name.StartsWith("Program:"))
                    obj.Add("allowMapping", false);

                return obj;
            }
            else
            {
                // map Logix primitive names to JSON schema types
                var prim = new Value();

                switch (tag.Type.Name.ToUpperInvariant())
                {
                    case "BOOL":
                        prim.Add("type", "boolean");
                        break;

                    case "SINT":
                    case "USINT":
                    case "INT":
                    case "UINT":
                    case "DINT":
                    case "UDINT":
                    case "LINT":
                    case "ULINT":
                    case "BYTE":
                    case "WORD":
                    case "DWORD":
                    case "LWORD":
                        prim.Add("type", "integer");
                        break;

                    case "REAL":
                    case "LREAL":
                        prim.Add("type", "number");
                        break;

                    case "STRING":
                    case "STRING2":
                    case "STRINGN":
                    case "STRINGI":
                    case "SHORT_STRING":
                    case "STRING_STRUCT":
                        prim.Add("type", "string");
                        prim.Add("maxLength", 80);
                        break;

                    default:
                        // fallback to string
                        prim.Add("type", "string");
                        break;
                }

                return prim;
            }
        }

        //public static JsonSchemaValue BuildSchema(LogixSymbolAdapter adapter)
        //{
        //    var typeCache = new HashSet<string>();

        //    var definitions = new Value();
        //    var properties = new Value();

        //    foreach(var tag in adapter.Tags)
        //    {
        //        var instance = ResolveTypeDefinition(tag, definitions, typeCache, adapter.TargetName);
        //        properties.Add(tag.Name, instance);
        //    }

        //    var root = new Value();
        //    root.Add("type", "object");
        //    root.Add("properties", properties);
        //    root.Add("definitions", definitions);

        //    return new JsonSchemaValue(root);
        //}

        // recursive type definition resolver
    //    private static Value ResolveTypeDefinition(TagDefinition tag, Value definitions, HashSet<string> cache, string targetName)
    //    {
    //        if (LogixTypes.IsArray(tag.Type.Code))
    //        {
    //            // get array base type name
    //            var baseTypeName = tag.Type.Members[0].Type.Name;

    //            // primitive vs UDT type name resolution
    //            var itemTypeName = (LogixTypes.IsUdt(tag.Type.Members[0].Type.Code) ?
    //                $"#/definitions/{targetName}.{baseTypeName}" :
    //                $"tchmi:general#/definitions/{baseTypeName}");

    //            // account for if array base type is unresolved udt
    //            if (LogixTypes.IsUdt(tag.Type.Members[0].Type.Code) && !cache.Contains(itemTypeName))
    //            {
    //                ResolveTypeDefinition(tag.Type.Members[0], definitions, cache, targetName);
    //            }

    //            // array instance definition name
    //            var defName = $"{targetName}.ARRAY_0..{tag.Type.Dims - 1}_OF-{baseTypeName}";

    //            if (!cache.Contains(defName))
    //            {
    //                var arrayDef = new Value();
    //                arrayDef.Add("type", "array");
    //                var items = new Value();

    //                items.Add("$ref", itemTypeName);
    //                arrayDef.Add("items", items);

    //                arrayDef.Add("maxItems", tag.Type.Dims);
    //                arrayDef.Add("minItems", tag.Type.Dims);

    //                definitions.Add(defName, arrayDef);
    //                cache.Add(defName);
    //            }

    //            return new Value { { "$ref", $"#/definitions/{defName}" } };
    //        }
    //        else if (LogixTypes.IsUdt(tag.Type.Code) || tag.Name.StartsWith("Program:"))
    //        {
    //            if (tag.Type.Name == "STRING")
    //                return new Value { { "$ref", $"#/definitions/STRING(80)" } };

    //            var defName = $"{targetName}.{tag.Type.Name}";

    //            if (!cache.Contains(defName))
    //            {
    //                // resolve UDT type / instance
    //                var udtDef = new Value();
    //                udtDef.Add("type", "object");

    //                if (tag.Name.StartsWith("Program:"))
    //                    udtDef.Add("allowMapping", false);

    //                var udtMembers = new Value();

    //                if (tag.Type.Members != null)
    //                {
    //                    foreach (var member in tag.Type.Members)
    //                    {
    //                        if (member.Name.StartsWith("ZZZZZZZZZZ"))
    //                            continue;

    //                        var memberDef = ResolveTypeDefinition(member, definitions, cache, targetName);
    //                        udtMembers.Add(member.Name, memberDef);
    //                    }
    //                }

    //                udtDef.Add("properties", udtMembers);
    //                definitions.Add(defName, udtDef);
    //                cache.Add(defName);
    //            }

    //            return new Value { { "$ref", $"#/definitions/{defName}" } };
    //        }
    //        else
    //        {
    //            // primitive type
    //            return new Value { { "$ref", $"tchmi:general#/definitions/{tag.Type.Name}" } };
    //        }
    //    }
    }
}
