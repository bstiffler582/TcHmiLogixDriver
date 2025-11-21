using Logix;
using System;
using System.Collections.Generic;
using System.Linq;
using TcHmiLogixDriver.Utilities;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix.Symbols
{
    public class LogixSymbol : Symbol
    {
        private LogixDriver driver;
        private IEnumerable<string> mappedSymbols;
        private int mappedSymbolsCount = 0;
        private LookupTrie<string> mappingTree;

        public LogixSymbol(LogixDriver driver, IEnumerable<string> mappedSymbols) 
            : base(LogixSchemaAdapter.BuildSymbolSchema(driver))
        {
            this.driver = driver;
            this.mappedSymbols = mappedSymbols;
            driver.ValueResolver = new LogixSymbolValueResolver();
        }

        // A tree structure (trie) gives us an efficient way to compare the requested symbol path
        // (in the form of element <string> queues) against mapped symbols
        private LookupTrie<string> BuildMappingTree(IEnumerable<string> symbols)
        {
            var tree = new LookupTrie<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols)
            {
                var path = TcHmiApplication.SplitSymbolPath(symbol, StringSplitOptions.RemoveEmptyEntries).Skip(1);
                tree.AddPath(path);
            }

            return tree;
        }

        /// <summary>
        /// We receive a full requested symbol path, but we only want to read what is mapped in TcHmi.
        /// So we keep track of what's mapped (in the mappingTree) and compare with what is requested.
        /// Then we resolve the value based on the difference between what is read and what is requested.
        /// </summary>
        /// <param name="elements">Queue that represents requested symbol path</param>
        /// <param name="context"></param>
        /// <returns>Resolved TcHmi Value</returns>
        protected override Value Read(Queue<string> elements, Context context)
        {
            // rebuild symbol mapping tree
            if (mappedSymbols.Count() != mappedSymbolsCount)
            {
                mappingTree = BuildMappingTree(mappedSymbols.Where(s => s.StartsWith(driver.Target.Name)));
                mappedSymbolsCount = mappedSymbols.Count();
            }

            // nested symbol requested
            if (elements.Count > 1)
            {
                // get mapped element list with matching / partial matching path
                var match = mappingTree.TryDescend(elements).GetPath().ToList();

                if (match.Count() > 0)
                {
                    elements.Dequeue();

                    // build tag string
                    var symbolPath = match.Aggregate((acc, s) =>
                    {
                        elements.Dequeue();
                        return int.TryParse(s, out var _) ? acc += $"[{s}]" :
                            acc += $".{s}";
                    });

                    // read tag
                    var read = driver.ReadTagValue(symbolPath) as Value;

                    // generate return value
                    while (elements.Count > 0)
                    {
                        var member = elements.Dequeue();
                        if (int.TryParse(member, out var i))
                            read = read[i];
                        else
                            read = read[member];
                    }
                    return read;
                }
                else
                {
                    throw new Exception($"Requested symbol path: {string.Join("::", elements)} not found in map tree.");
                }
            }

            // read root tag and return full value
            driver.Target.TagDefinitions.TryGetValue(elements.Dequeue(), out var tagDef);
            var root = driver.ReadTagValue(tagDef.Name) as Value;
            return root;
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }
}

