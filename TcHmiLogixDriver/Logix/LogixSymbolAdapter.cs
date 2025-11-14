using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using System.Runtime.CompilerServices;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbolAdapter
    {
        private HashSet<string> typeCache = new HashSet<string>();

        public Value GetDefinitions(TagDefinition tag)
        {
            var target = new Value();
            target.Add("type", "object");

            var targetMembers = new Value();
            targetMembers.Add("type", "object");

            var ret = new Value();
            var properties = new Value();
            var definitions = new Value();

            if (LogixTypes.IsArray(tag.Type.Code))
            {
            }
            else if (LogixTypes.IsUdt(tag.Type.Code))
            {
                var udtInstance = new Value();
                udtInstance.Add("$ref", $"#/definitions/{tag.Type.Name}");
                targetMembers.Add(tag.Name, udtInstance);

                if (!typeCache.Contains(tag.Type.Name))
                {
                    GetTypeDefinition(tag, definitions);
                }
            }
            else
            {
                var primType = new Value();
                primType.Add("$ref", $"tchmi:general#/definitions/{tag.Type.Name}");
                targetMembers.Add(tag.Name, primType);
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

        public Value GetTypeDefinition(TagDefinition tag, Value definitions)
        {
            var definition = new Value();

            if (LogixTypes.IsArray(tag.Type.Code))
            {
            }
            else if (LogixTypes.IsUdt(tag.Type.Code))
            {
                definition.Add("type", "object");
                if (tag.Type.Members?.Count > 0)
                {
                    var properties = new Value();
                    properties.Add("type", "object");
                    foreach (var member in tag.Type.Members)
                    {
                        properties.Add(member.Name, GetTypeDefinition(member, definitions));
                    }
                    definition.Add("properties", properties);
                }
            }
            else
            {
                definition.Add("$ref", $"tchmi:general#/definitions/{tag.Type.Name}");
            }

            definitions.Add(tag.Name, definition);

            return definition;
        }
    }
}
