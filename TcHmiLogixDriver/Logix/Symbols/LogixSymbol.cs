using Logix;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TcHmiLogixDriver.Utilities;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix.Symbols
{
    public class LogixSymbol : Symbol, IDisposable
    {
        private LogixDriver driver;
        private IEnumerable<string> mappedSymbols;
        private int mappedSymbolsCount = 0;
        private LookupTrie<string> mappingTree;
        private ConcurrentDictionary<uint, List<string>> subscriptionSymbols = new();

        public LogixSymbol(LogixDriver driver, IEnumerable<string> mappedSymbols) 
            : base(LogixSchemaAdapter.BuildSymbolSchema(driver))
        {
            this.driver = driver;
            this.mappedSymbols = mappedSymbols;
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
        protected override Value Read(Queue<string> elements, Context context)
        {
            if (!driver.IsConnected)
                throw new Exception($"No connection to target: {driver.Target.Name}");
            
            // rebuild the symbol mapping tree if new symbols are found
            if (mappedSymbols.Count() != mappedSymbolsCount)
            {
                mappingTree = BuildMappingTree(mappedSymbols.Where(s => s.StartsWith(driver.Target.Name)));
                mappedSymbolsCount = mappedSymbols.Count();
            }

            // get mapped element list with matching / partial matching path
            var match = mappingTree.TryDescend(elements).GetPath().ToList();

            if (match.Count() > 0)
            {
                elements.Dequeue();

                // build tag string
                var tagName = match.Aggregate((acc, s) =>
                {
                    elements.Dequeue();
                    return int.TryParse(s, out var _) ? acc += $"[{s}]" :
                        acc += $".{s}";
                });

                AddSymbolSubscription(context.SubscriptionId, tagName);
                var readValue = driver.ReadTagValue(tagName) as Value;

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
                throw new Exception($"Requested symbol path: {string.Join("::", elements)} not found in map tree.");
            }
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            // build tag string
            string tagName = elements.Dequeue();
            while (elements.TryDequeue(out var element))
            {
                tagName += int.TryParse(element, out var _) ? 
                    $"[{element}]" : $".{element}";
            }

            driver.WriteTagValue(tagName, value);

            return value;
        }

        // manage read subscriptions
        private void AddSymbolSubscription(uint subscriptionId, string symbol)
        {
            driver.SubscribeTag(symbol);

            if (subscriptionSymbols.ContainsKey(subscriptionId))
                subscriptionSymbols[subscriptionId].Add(symbol);
            else
                subscriptionSymbols.TryAdd(subscriptionId, new List<string>() { symbol });
        }

        public void UnsubscribeById(uint subscriptionId)
        {
            if (subscriptionSymbols.TryGetValue(subscriptionId, out var symbols))
            {
                driver.UnsubscribeTags(symbols);
                subscriptionSymbols.Remove(subscriptionId, out _);
            }
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

        public void Dispose()
        {
            //subscriptionListener.OnUnsubscribe -= onUnsubscribe;
        }
    }
}

