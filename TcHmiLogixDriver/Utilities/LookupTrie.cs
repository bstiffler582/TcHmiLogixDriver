using System.Collections.Generic;

namespace TcHmiLogixDriver.Utilities
{
    public class LookupTrie<TKey> where TKey : notnull
    {
        public class Node
        {
            public TKey Key { get; }
            public Node? Parent { get; }
            public Dictionary<TKey, Node> Children { get; }
            public bool IsTerminal { get; internal set; }

            public Node(TKey key, Node? parent, IEqualityComparer<TKey> comparer)
            {
                Key = key;
                Parent = parent;
                Children = new Dictionary<TKey, Node>(comparer);
            }

            public IEnumerable<TKey> GetPath()
            {
                var stack = new Stack<TKey>();
                var n = this;

                while (n.Parent != null)
                {
                    stack.Push(n.Key);
                    n = n.Parent;
                }

                return stack;
            }
        }

        private readonly IEqualityComparer<TKey> _comparer;
        private readonly Node _root;  // internal root with no key

        public LookupTrie(IEqualityComparer<TKey>? comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _root = new Node(default!, null, _comparer);
        }

        public void AddPath(IEnumerable<TKey> elements)
        {
            using var e = elements.GetEnumerator();
            if (!e.MoveNext())
                return;

            // first element lives directly under the internal root
            var node = _root;

            do
            {
                var key = e.Current;

                if (!node.Children.TryGetValue(key, out var next))
                {
                    next = new Node(key, node, _comparer);
                    node.Children[key] = next;
                }

                node = next;
            }
            while (e.MoveNext());

            node.IsTerminal = true;
        }

        /// <summary>
        /// Descend the tree along the supplied sequence of elements.
        /// Return the longest added path that the sequence starts with.
        /// </summary>
        /// <param name="elements">Ordered sequence of elements</param>
        /// <returns>The deepest terminal node on the way down, or null if no added path matches</returns>
        public Node? TryDescend(IEnumerable<TKey> elements)
        {
            Node? match = null;
            var node = _root;

            foreach (var key in elements)
            {
                if (!node.Children.TryGetValue(key, out var next))
                    break;

                node = next;

                // intermediate nodes only exist because a deeper path was added; skip past them
                if (node.IsTerminal)
                    match = node;
            }

            return match;
        }
    }
}
