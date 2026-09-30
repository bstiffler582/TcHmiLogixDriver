using Logix.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TcHmiLogixDriver.Logix.Symbols
{
    /// <summary>
    /// Which top-level tags the tag browser shows: everything, or, when a tag selector is configured,
    /// only what its entries cover. An entry selects a controller tag ("Counter"), a whole program
    /// ("Program:Main"), or one program tag ("Program:Main.Recipe"). Deeper paths ("Recipes[3].Temp")
    /// select their top-level tag.
    /// </summary>
    internal sealed class TagSelection
    {
        private readonly HashSet<string> controllerTags = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> wholePrograms = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> programTags = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool selectsAll;

        private TagSelection(IReadOnlyCollection<string> selector)
        {
            selectsAll = selector.Count == 0;

            foreach (var entry in selector)
            {
                var root = TopLevelName(entry);
                if (!root.StartsWith("Program:", StringComparison.OrdinalIgnoreCase))
                {
                    controllerTags.Add(root);
                    continue;
                }

                var rest = entry.Length > root.Length ? entry[(root.Length + 1)..] : "";
                if (rest.Length == 0)
                {
                    wholePrograms.Add(root);
                    continue;
                }

                if (!programTags.TryGetValue(root, out var tags))
                    programTags[root] = tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tags.Add(TopLevelName(rest));
            }
        }

        public static TagSelection From(IEnumerable<string>? selector) =>
            new(selector?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>());

        public IEnumerable<TagNode> Apply(IEnumerable<TagNode> nodes)
        {
            if (selectsAll)
                return nodes;

            return nodes.Select(node => node switch
            {
                TagInfo tag when controllerTags.Contains(tag.Name) => tag,
                ProgramInfo program when wholePrograms.Contains(program.Name) => program,
                ProgramInfo program when programTags.TryGetValue(program.Name, out var names) =>
                    program with { Tags = program.Tags?.Where(t => names.Contains(t.Name)).ToList() },
                _ => (TagNode?)null
            })
            .OfType<TagNode>();
        }

        // the name up to the first member access or index: "Recipes[3].Temp" -> "Recipes"
        private static string TopLevelName(string path)
        {
            var end = path.IndexOfAny(new[] { '.', '[' });
            return end < 0 ? path : path[..end];
        }
    }
}
