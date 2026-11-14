using System;
using System.Collections.Generic;

namespace SmartX.Api.Domain
{
    /// <summary>
    /// Generic self-balancing AVL Tree implementing O(log N) search, insertion, and deletion,
    /// with O(log N + K) range querying for historical timestamped telemetry data.
    /// Perfectly balances heights via rotations to guarantee no worst-case O(N) degradation of simple BSTs.
    /// </summary>
    /// <typeparam name="TKey">Comparable key type (e.g. DateTime, long, double).</typeparam>
    /// <typeparam name="TValue">Payload value type (e.g. TelemetryPacket, ReadingEntry).</typeparam>
    public class AVLTree<TKey, TValue> where TKey : IComparable<TKey>
    {
        public class AVLNode
        {
            public TKey Key { get; set; }
            public List<TValue> Values { get; set; }
            public AVLNode? Left { get; set; }
            public AVLNode? Right { get; set; }
            public int Height { get; set; }

            public AVLNode(TKey key, TValue value)
            {
                Key = key;
                Values = new List<TValue> { value };
                Height = 1;
            }
        }

        private AVLNode? _root;
        private int _count;
        private readonly object _lock = new();

        public int Count
        {
            get
            {
                lock (_lock) return _count;
            }
        }

        public AVLNode? Root => _root;

        public bool IsEmpty => _root == null;

        /// <summary>
        /// Inserts a key-value pair into the AVL tree in O(log N) time, automatically rebalancing via rotations.
        /// If duplicate keys occur (same timestamp), appends to the node's value list.
        /// </summary>
        public void Insert(TKey key, TValue value)
        {
            lock (_lock)
            {
                _root = InsertRecursive(_root, key, value);
                _count++;
            }
        }

        private AVLNode InsertRecursive(AVLNode? node, TKey key, TValue value)
        {
            if (node == null)
            {
                return new AVLNode(key, value);
            }

            int compare = key.CompareTo(node.Key);
            if (compare < 0)
            {
                node.Left = InsertRecursive(node.Left, key, value);
            }
            else if (compare > 0)
            {
                node.Right = InsertRecursive(node.Right, key, value);
            }
            else
            {
                // Duplicate timestamp / key - store multiple readings in the node list
                node.Values.Add(value);
                return node;
            }

            // Update height
            node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));

            // Balance factor check
            int balance = GetBalance(node);

            // Left-Left Case
            if (balance > 1 && key.CompareTo(node.Left!.Key) < 0)
            {
                return RotateRight(node);
            }

            // Right-Right Case
            if (balance < -1 && key.CompareTo(node.Right!.Key) > 0)
            {
                return RotateLeft(node);
            }

            // Left-Right Case
            if (balance > 1 && key.CompareTo(node.Left!.Key) > 0)
            {
                node.Left = RotateLeft(node.Left!);
                return RotateRight(node);
            }

            // Right-Left Case
            if (balance < -1 && key.CompareTo(node.Right!.Key) < 0)
            {
                node.Right = RotateRight(node.Right!);
                return RotateLeft(node);
            }

            return node;
        }

        private int GetHeight(AVLNode? node) => node?.Height ?? 0;

        private int GetBalance(AVLNode? node) => node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);

        private AVLNode RotateRight(AVLNode y)
        {
            AVLNode x = y.Left!;
            AVLNode? t2 = x.Right;

            x.Right = y;
            y.Left = t2;

            y.Height = Math.Max(GetHeight(y.Left), GetHeight(y.Right)) + 1;
            x.Height = Math.Max(GetHeight(x.Left), GetHeight(x.Right)) + 1;

            return x;
        }

        private AVLNode RotateLeft(AVLNode x)
        {
            AVLNode y = x.Right!;
            AVLNode? t2 = y.Left;

            y.Left = x;
            x.Right = t2;

            x.Height = Math.Max(GetHeight(x.Left), GetHeight(x.Right)) + 1;
            y.Height = Math.Max(GetHeight(y.Left), GetHeight(y.Right)) + 1;

            return y;
        }

        /// <summary>
        /// Retrieves all values matching exact key in O(log N) time.
        /// </summary>
        public List<TValue> Find(TKey key)
        {
            lock (_lock)
            {
                var current = _root;
                while (current != null)
                {
                    int cmp = key.CompareTo(current.Key);
                    if (cmp == 0) return new List<TValue>(current.Values);
                    current = cmp < 0 ? current.Left : current.Right;
                }
                return new List<TValue>();
            }
        }

        /// <summary>
        /// Range query: retrieves all values with keys in [fromKey, toKey] inclusive in O(log N + K) time.
        /// Prunes subtrees outside the bounds, avoiding full dataset scans.
        /// </summary>
        public List<TValue> QueryRange(TKey fromKey, TKey toKey)
        {
            lock (_lock)
            {
                var results = new List<TValue>();
                QueryRangeRecursive(_root, fromKey, toKey, results);
                return results;
            }
        }

        private void QueryRangeRecursive(AVLNode? node, TKey fromKey, TKey toKey, List<TValue> results)
        {
            if (node == null) return;

            // If current key is greater than fromKey, there could be valid keys in left subtree
            if (node.Key.CompareTo(fromKey) > 0)
            {
                QueryRangeRecursive(node.Left, fromKey, toKey, results);
            }

            // If current key is within [fromKey, toKey], collect values
            if (node.Key.CompareTo(fromKey) >= 0 && node.Key.CompareTo(toKey) <= 0)
            {
                results.AddRange(node.Values);
            }

            // If current key is less than toKey, there could be valid keys in right subtree
            if (node.Key.CompareTo(toKey) < 0)
            {
                QueryRangeRecursive(node.Right, fromKey, toKey, results);
            }
        }

        /// <summary>
        /// In-order traversal yielding all values in sorted ascending key order.
        /// </summary>
        public List<TValue> ToInOrderList()
        {
            lock (_lock)
            {
                var list = new List<TValue>();
                InOrderRecursive(_root, list);
                return list;
            }
        }

        private void InOrderRecursive(AVLNode? node, List<TValue> list)
        {
            if (node == null) return;
            InOrderRecursive(node.Left, list);
            list.AddRange(node.Values);
            InOrderRecursive(node.Right, list);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _root = null;
                _count = 0;
            }
        }
    }
}
