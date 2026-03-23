using Logix.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TcHmiLogixDriver.Utilities;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix.Symbols
{
    public class LogixSymbol : AsyncSymbol, IDisposable
    {
        private IDriver driver;
        private LookupTrie<string>? mappingTree;

        public LogixSymbol(IDriver driver)
            : base(LogixSchemaAdapter.BuildSymbolSchema(driver))
        {
            this.driver = driver;
        }

        /// <summary>
        /// Uses the requested symbol path to descend the mapping tree and determine which Tag to read.
        /// If a child node is being requested but its parent is what's mapped, the whole parent is read.
        /// This gives the mapper control over how data is read from the PLC.
        /// </summary>
        /// <param name="elements">Queue that represents requested symbol path</param>
        /// <param name="context"></param>
        /// <returns>Resolved TcHmi Value</returns>
        protected async override Task<Value?> ReadAsync(Queue<string> elements, Context context)
        {
            if (mappingTree is null)
            {
                throw new Exception($"No symbols mapped for target {driver.Target.Name}");
            }

            if (!driver.IsConnected)
            {
                return null;
                throw new Exception($"Connection to target {driver.Target.Name} lost!");
            }

            // get mapped element list with matching / partial matching path
            var match = mappingTree.TryDescend(elements)!.GetPath().ToList();

            if (match.Count > 0)
            {
                elements.Dequeue();

                // build tag string
                var tagName = match.Aggregate((acc, s) =>
                {
                    elements.Dequeue();
                    return int.TryParse(s, out var _) ? acc += $"[{s}]" :
                        acc += $".{s}";
                });

                var readValue = await driver.ReadTagValueAsync(tagName) as Value;

                // generate return value
                while (elements.Count > 0)
                {
                    var member = elements.Dequeue();
                    if (readValue is null) continue;
                    if (int.TryParse(member, out var i))
                        readValue = readValue[i];
                    else
                        readValue = readValue[member];
                }

                return readValue;
            }
            else
            {
                throw new Exception($"Requested symbol path: {string.Join(".", elements)} not found in map tree.");
            }
        }

        protected async override Task<Value> WriteAsync(Queue<string> elements, Value value, Context context)
        {
            // build tag string
            string tagName = elements.Dequeue();
            while (elements.TryDequeue(out var element))
            {
                tagName += int.TryParse(element, out var _) ? 
                    $"[{element}]" : $".{element}";
            }

            await driver.WriteTagValueAsync(tagName, value);

            return value;
        }

        public void SetMappedSymbols(IEnumerable<string> symbolNames)
        {
            var symbols = symbolNames.ToList();
            if (symbols.Count > 0)
            {
                mappingTree = BuildMappingTree(symbols);
            }
        }

        // A tree structure (trie) gives us an efficient way to compare the requested symbol path
        // (in the form of element <string> queues) against mapped symbols
        private LookupTrie<string> BuildMappingTree(IEnumerable<string> symbols)
        {
            var tree = new LookupTrie<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols)
            {
                // skip extension and target name
                var path = symbol.Split('.').Skip(2);
                tree.AddPath(path);
            }

            return tree;
        }
    }
}

