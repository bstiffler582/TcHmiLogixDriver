using Logix.Driver;
using Logix.Tags;
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
        /// <param name="tagSelector">when it has entries, only the tags they select are shown</param>
        public static JsonSchemaValue BuildSymbolSchema(IDriver driver, IEnumerable<string>? tagSelector = null)
        {
            var builder = new SchemaBuilder(driver.Target.Name);
            var properties = new Value();

            var nodes = TagSelection.From(tagSelector).Apply(driver.Tags.GetLoadedTags()).ToList();
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case TagInfo tag when !tag.Name.StartsWith("__DEFVAL_"):
                        properties.Add(tag.Name, builder.TypeSchema(tag.Type).Schema);
                        break;
                    case ProgramInfo program:
                        properties.Add(program.Name, builder.ProgramSchema(program));
                        break;
                }
            }

            var root = new Value();
            root.Add("definitions", builder.Definitions);
            root.Add("properties", properties);
            root.Add("type", "object");
            root.Add("allowMapping", false);

            return new JsonSchemaValue(root, nodes.Count > 0);
        }

        /// <summary>
        /// Builds instance schemas, adding each named type (structures, arrays, programs) to the
        /// definitions once and referring to it from there.
        /// </summary>
        private sealed class SchemaBuilder
        {
            private readonly string targetName;
            private readonly HashSet<string> defined = new();

            public Value Definitions { get; } = new();

            public SchemaBuilder(string targetName) => this.targetName = targetName;

            public (string TypeName, Value Schema) TypeSchema(TypeRef type)
            {
                // not loaded yet: shown, but can't be browsed into or mapped
                if (!type.IsResolved)
                    return (type.ToString(), Hidden());

                if (type.IsArray)
                {
                    var (itemTypeName, itemSchema) = TypeSchema(type.Index(1));
                    var count = type.Dims[0];
                    var typeName = $"ARRAY_0..{count - 1}_OF-{itemTypeName}";

                    return (typeName, Define(typeName, () => new Value
                    {
                        { "type", "array" },
                        { "items", itemSchema },
                        { "maxItems", count },
                        { "minItems", count },
                    }));
                }

                return type.ElementType switch
                {
                    StringType => ("String", Reference("tchmi:general#/definitions/String")),
                    StructType structType => (structType.Name, Define(structType.Name, () =>
                    {
                        var members = new Value();
                        foreach (var member in structType.Members)
                            members.Add(member.Name, TypeSchema(member.Type).Schema);
                        return new Value { { "type", "object" }, { "properties", members } };
                    })),
                    var primitive => (primitive!.Name, Reference($"tchmi:general#/definitions/{primitive.Name}")),
                };
            }

            public Value ProgramSchema(ProgramInfo program)
            {
                if (!program.IsLoaded)
                    return Hidden();

                return Define(program.Name, () =>
                {
                    var tags = new Value();
                    foreach (var tag in program.Tags!)
                        tags.Add(tag.Name, TypeSchema(tag.Type).Schema);
                    return new Value { { "type", "object" }, { "allowMapping", false }, { "properties", tags } };
                });
            }

            // adds the definition the first time a type name is seen; returns a reference to it
            private Value Define(string typeName, System.Func<Value> build)
            {
                var definitionName = $"{targetName}.{typeName}";
                if (defined.Add(definitionName))
                    Definitions.Add(definitionName, build());
                return Reference($"#/definitions/{definitionName}");
            }

            private static Value Reference(string target) => new() { { "$ref", target } };

            private static Value Hidden() => new()
            {
                { "type", "object" },
                { "allowMapping", false },
                { "hidden", true },
            };
        }
    }
}
