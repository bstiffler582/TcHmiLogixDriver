using System;
using System.Collections.Generic;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolAdapter
    {
        // todo: implement adapter for symbol schema resolution
        // this is a PITA
        public Value GetDefinitions(IEnumerable<TagDefinition> tags)
        {
            // base return value
            var ret = new Value();
            // type definitions
            var definitions = new Value();
            // properties (members / instances of definitions)
            var properties = new Value();

            // top level target/plc object
            var target = new Value();
            target.Add("type", "object");

            // members of target (all symbols)
            var targetMembers = new Value();

            var typeCache = new HashSet<string>();
            foreach (var tag in tags)
            {
                var instance = ResolveTypeDefinition(tag, definitions, typeCache);
                targetMembers.Add(tag.Name, instance);
            }

            target.Add("properties", targetMembers);
            definitions.Add("Target", target);

            var targetInstance = new Value();
            targetInstance.Add("$ref", $"#/definitions/Target");
            properties.Add("Logix", targetInstance);

            ret.Add("definitions", definitions);
            ret.Add("properties", properties);

            return ret;
        }

        // mutates definitions Value if new type def
        private Value ResolveTypeDefinition(TagDefinition tag, Value definitions, HashSet<string> cache)
        {
            var ret = new Value();

            if (LogixTypes.IsArray(tag.Type.Code))
            {
                // resolve array types / instances
                // schema from ADS extension:
                /*
                "definitions": {
                    "PLC1.ARRAY_0..4_OF-DINT": {
                        "items": {
                            "$ref": "tchmi:general#/definitions/DINT"
                        },
                        "maxItems": 5,
                        "minItems": 5,
                        "type": "array"
                    },
                    "PLC1.ARRAY_0..4_OF-ST_TestChild": {
                        "items": {
                            "$ref": "#/definitions/PLC1.ST_TestChild"
                        },
                        "maxItems": 5,
                        "minItems": 5,
                        "type": "array"
                    },...
                 */
                throw new NotImplementedException();
            }
            else if (LogixTypes.IsUdt(tag.Type.Code))
            {
                if (!cache.Contains(tag.Type.Name))
                {
                    // resolve UDT type / instance
                    var udtDef = new Value();
                    udtDef.Add("type", "object");
                    var udtMembers = new Value();

                    foreach (var member in tag.Type.Members)
                    {
                        var memberDef = ResolveTypeDefinition(member, definitions, cache);
                        udtMembers.Add(member.Name, memberDef);
                    }

                    udtDef.Add("properties", udtMembers);
                    definitions.Add(tag.Type.Name, udtDef);
                    cache.Add(tag.Type.Name);
                }

                ret.Add("$ref", $"#/definitions/{tag.Type.Name}");
                return ret;
            }
            else
            {
                // primitive type
                ret.Add("$ref", $"tchmi:general#/definitions/{tag.Type.Name}");
                return ret;
            }
        }
    }
}
