using Logix.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TcHmiLogixDriver.Utilities;
using TcHmiSrv.Core;
using TcHmiSrv.Core.General;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix.Symbols
{
    public class LogixSymbol : AsyncSymbol, IDisposable
    {
        // a missed lookup refreshes the mapping tree at most this often
        private const long MappingRefreshInterval = 5000;

        // mapped symbol paths and the tree built from them, published together by reference swap
        private sealed record Mappings(IReadOnlyList<string> Symbols, LookupTrie<string> Tree);

        private readonly IDriver driver;
        private volatile Mappings mappings = new(Array.Empty<string>(), new LookupTrie<string>(StringComparer.OrdinalIgnoreCase));
        private long nextMappingRefreshAt;

        private LogixSymbol(IDriver driver, IEnumerable<string>? tagSelector)
            : base(LogixSchemaAdapter.BuildSymbolSchema(driver, tagSelector))
        {
            this.driver = driver;
        }

        /// <summary>
        /// Creates the symbol with its mapping tree already built, so it never serves requests from an empty tree.
        /// </summary>
        /// <param name="tagSelector">when it has entries, the tag browser shows only the tags they select</param>
        public static async Task<LogixSymbol> CreateAsync(IDriver driver, IEnumerable<string>? tagSelector = null)
        {
            var symbol = new LogixSymbol(driver, tagSelector);
            await symbol.UpdateMappedSymbolsAsync();
            return symbol;
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
            if (!driver.IsConnected)
                throw new Exception($"Driver {driver.Target.Name} disconnected.");

            // get mapped element list with matching / partial matching path
            var node = mappings.Tree.TryDescend(elements);

            // requested symbol not in mapping tree, try to refresh mappings
            if (node is null && CheckMappingRefreshInterval())
            {
                await UpdateMappedSymbolsAsync();
                node = mappings.Tree.TryDescend(elements);
            }

            if (node is null)
                throw new Exception($"Requested symbol path: {string.Join(".", elements)} not found in map tree.");

            var match = node.GetPath().ToList();

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

        protected async override Task<Value> WriteAsync(Queue<string> elements, Value value, Context context)
        {
            if (!driver.IsConnected)
                throw new Exception($"No connection to target {driver.Target.Name}.");

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

        public async Task UpdateMappedSymbolsAsync()
        {
            var symbols = await GetMappedSymbolsAsync();

            // keep the current tree when the server couldn't be asked; an empty list would unmap everything
            if (symbols is null || mappings.Symbols.SequenceEqual(symbols))
                return;

            mappings = new Mappings(symbols, BuildMappingTree(symbols));
        }

        // lets one caller per interval refresh after a miss, so a stale binding won't refresh every poll
        private bool CheckMappingRefreshInterval()
        {
            var now = Environment.TickCount64;
            var due = Interlocked.Read(ref nextMappingRefreshAt);
            return now >= due &&
                Interlocked.CompareExchange(ref nextMappingRefreshAt, now + MappingRefreshInterval, due) == due;
        }

        // A tree structure (trie) gives us an efficient way to compare the requested symbol path
        // (in the form of element <string> queues) against mapped symbols
        private LookupTrie<string> BuildMappingTree(IEnumerable<string> symbols)
        {
            var tree = new LookupTrie<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in symbols)
                tree.AddPath(symbol.Split('.'));

            return tree;
        }

        // request mapped symbol list from TcHmiSrv; returns each of this target's symbols as its
        // path below the target, e.g. "TcHmiLogixDriver.PLC1.Line.Motors" -> "Line.Motors"; null if the request failed
        private async Task<List<string>?> GetMappedSymbolsAsync()
        {
            var (res, ctx, cmd) =
                await TcHmiApplication.AsyncHost.ExecuteAsync(TcHmiApplication.Context, new Command("ListSymbols"));

            if (res != ErrorValue.HMI_SUCCESS)
                return null;

            // match "<domain>.<target>." prefix
            var prefix = $"{ctx.Domain}.{driver.Target.Name}.";

            return cmd.ReadValue.Keys
                .Where(s => s.StartsWith(prefix, StringComparison.Ordinal))
                .Select(s => s[prefix.Length..])
                .ToList();
        }
    }
}

