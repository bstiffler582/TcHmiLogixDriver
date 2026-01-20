using Logix;
using System;
using System.Collections.Concurrent;
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
        private LogixDriver driver;
        private LookupTrie<string> mappingTree;
        private ConcurrentDictionary<uint, HashSet<string>> subscriptionSymbols = new();

        public LogixSymbol(LogixDriver driver)
            : base(LogixSchemaAdapter.BuildSymbolSchema(driver))
        {
            this.driver = driver;
            driver.ValueResolver = new LogixSymbolValueResolver();
        }

        /// <summary>
        /// We receive a full requested symbol path, but we only want to read what is mapped in TcHmi.
        /// So we keep track of what's mapped (in the mappingTree) and compare with what is requested.
        /// Then we resolve the value based on the difference between what is read and what is requested.
        /// </summary>
        /// <param name="elements">Queue that represents requested symbol path</param>
        /// <param name="context"></param>
        /// <returns>Resolved TcHmi Value</returns>
        protected async override Task<Value> ReadAsync(Queue<string> elements, Context context)
        {
            if (!driver.IsConnected)
                throw new Exception($"No connection to target: {driver.Target.Name}");

            if (mappingTree is null)
                throw new Exception($"No symbols mapped for target {driver.Target.Name}");

            // get mapped element list with matching / partial matching path
            var match = mappingTree.TryDescend(elements).GetPath().ToList();

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

