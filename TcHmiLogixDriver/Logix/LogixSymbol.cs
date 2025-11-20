using Logix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;

namespace TcHmiLogixDriver.Logix
{
    public class LogixSymbol : Symbol
    {
        private LogixTarget target;
        private LogixDriver driver;
        private MappingTree mappedSymbols;

        public LogixSymbol(LogixTarget target, LogixDriver driver, MappingTree mappedSymbols) : base(LogixSchemaAdapter.BuildSymbolSchema(target))
        {
            this.target = target;
            this.driver = driver;
            this.mappedSymbols = mappedSymbols;
            driver.ValueResolver = new LogixSymbolValueResolver();
        }

        protected override Value Read(Queue<string> elements, Context context)
        {
            if (elements.Count > 1)
            {
                var match = mappedSymbols.FindBestMatch(target.Name, elements).ToList();

                if (match.Count() > 0)
                {
                    elements.Dequeue();
                    var symbolPath = match.Aggregate((acc, s) => {
                        elements.Dequeue();
                        return (int.TryParse(s, out var _)) ? acc += $"[{s}]" :
                            acc += $".{s}";
                    });

                    if (elements.Count > 0)
                    {
                        var ret = driver.ReadTagValue(target, symbolPath) as Value;
                        return ret[elements.Dequeue()];
                    }
                    else
                    {
                        return driver.ReadTagValue(target, symbolPath) as Value;
                    }
                }
            }

            target.TagDefinitions.TryGetValue(elements.Dequeue(), out var tagDef);
            var root = driver.ReadTagValue(target, tagDef.Name) as Value;
            return root;
        }

        protected override Value Write(Queue<string> elements, Value value, Context context)
        {
            throw new NotImplementedException();
        }
    }

    public class MappingTree
    {
        public class Node
        {
            public string Value;
            public Node Parent;
            public Dictionary<string, Node> Children = new();
            public bool IsTerminal;

            public string[] GetPath()
            {
                var list = new List<string>();
                var n = this;

                while (n.Parent.Value != "Root")
                {
                    list.Add(n.Value!);
                    n = n.Parent;
                }

                list.Reverse();
                return list.ToArray();
            }
        }

        public Node Root = new Node() { Value = "Root" };

        public void AddPath(IEnumerable<string> elements)
        {
            var node = Root;

            foreach (var elem in elements)
            {
                if (!node.Children.TryGetValue(elem, out var child))
                {
                    child = new Node { Value = elem, Parent = node };
                    node.Children[elem] = child;
                }

                node = child;
            }

            node.IsTerminal = true;
        }

        public IEnumerable<string> FindBestMatch(string root, IEnumerable<string> elements)
        {
            if (!Root.Children.TryGetValue(root, out var node))
                return Enumerable.Empty<string>();

            foreach (var elem in elements) 
            {
                if (!node.Children.TryGetValue(elem, out var next))
                    break;

                node = next;
            }

            if (node == null)
                return Enumerable.Empty<string>();

            return node.GetPath();
        }
    }
}

