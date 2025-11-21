using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TcHmiSrv.Core.General;

namespace TcHmiLogixDriver.Logix
{
    internal class SubscriptionManager
    {
        record Subscription(uint Id, string Target, IEnumerable<string[]> Symbols);

        private List<Subscription> activeSubscriptions = new();

        private object writeLock = new object();

        public SubscriptionManager() { }

        public void AddSymbols(uint subscriptionId, IEnumerable<string> symbols) 
        {
            var paths = new List<string[]>();
            string target = "";

            foreach (var symbol in symbols)
            {
                // strip out extension name, save as split path
                var symbolPath = TcHmiApplication.SplitSymbolPath(symbol, StringSplitOptions.RemoveEmptyEntries).Skip(1);
                if (symbolPath.Count() < 2) continue;
                if (string.IsNullOrEmpty(target))
                    target = symbolPath.ElementAt(0);

                paths.Add(symbolPath.ToArray());
            }

            lock (writeLock)
            {
                activeSubscriptions.Add(new Subscription(subscriptionId, target, paths));
            }
        }

        public void RemoveSubscription(uint subscriptionId)
        {
            lock (writeLock)
            {
                activeSubscriptions = activeSubscriptions.Where(s => s.Id != subscriptionId).ToList();
            }
        }

        public IEnumerable<string[]> GetSymbolPathsByTarget(string target)
        {
            return activeSubscriptions
                .Where(s => s.Target == target)
                .SelectMany(s => s.Symbols);
        }
    }
}
