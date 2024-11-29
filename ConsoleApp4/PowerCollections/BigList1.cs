using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace Wintellect.PowerCollections
{
    [Serializable]
    public class BigList1<T> : ListBase<T>, ICloneable
    {
        const uint MAXITEMS = int.MaxValue - 1;
#if DEBUG
        const int MAXLEAF = 8;
#else
        const int MAXLEAF = 120;
#endif
        const int
            BALANCEFACTOR =
                6;
        static readonly int[] FIBONACCI =
        {
            1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584,
            4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040,
            1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986,
            102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903, int.MaxValue
        };
        const int MAXFIB = 44;
        private Node root;
        private int changeStamp;
        private void StopEnumerations()
        {
            ++changeStamp;
        }
        private void CheckEnumerationStamp(int startStamp)
        {
            if (startStamp != changeStamp)
            {
                throw new InvalidOperationException(Strings.ChangeDuringEnumeration);
            }
        }
        public BigList1()
        {
            root = null;
        }
        public BigList1(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            root = NodeFromEnumerable(collection);
            CheckBalance();
        }
        public BigList1(IEnumerable<T> collection, int copies)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            root = NCopiesOfNode(copies, NodeFromEnumerable(collection));
            CheckBalance();
        }
        public BigList1(BigList1<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if (list.root == null)
                root = null;
            else
            {
                list.root.MarkShared();
                root = list.root;
            }
        }
        public BigList1(BigList1<T> list, int copies)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if (list.root == null)
                root = null;
            else
            {
                list.root.MarkShared();
                root = NCopiesOfNode(copies, list.root);
            }
        }
        private BigList1(Node node)
        {
            this.root = node;
            CheckBalance();
        }
        public sealed override int Count
        {
            get
            {
                if (root == null)
                    return 0;
                else
                    return root.Count;
            }
        }
        public sealed override T this[int index]
        {
            get
            {
                if (root == null || index < 0 || index >= root.Count)
                    throw new ArgumentOutOfRangeException("index");
                Node current = root;
                ConcatNode curConcat = current as ConcatNode;
                while (curConcat != null)
                {
                    int leftCount = curConcat.left.Count;
                    if (index < leftCount)
                        current = curConcat.left;
                    else
                    {
                        current = curConcat.right;
                        index -= leftCount;
                    }
                    curConcat = current as ConcatNode;
                }
                LeafNode curLeaf = (LeafNode)current;
                return curLeaf.items[index];
            }
            set
            {
                if (root == null || index < 0 || index >= root.Count)
                    throw new ArgumentOutOfRangeException("index");
                StopEnumerations();
                if (root.Shared)
                    root = root.SetAt(index, value);
                Node current = root;
                ConcatNode curConcat = current as ConcatNode;
                while (curConcat != null)
                {
                    int leftCount = curConcat.left.Count;
                    if (index < leftCount)
                    {
                        current = curConcat.left;
                        if (current.Shared)
                        {
                            curConcat.left = current.SetAt(index, value);
                            return;
                        }
                    }
                    else
                    {
                        current = curConcat.right;
                        index -= leftCount;
                        if (current.Shared)
                        {
                            curConcat.right = current.SetAt(index, value);
                            return;
                        }
                    }
                    curConcat = current as ConcatNode;
                }
                LeafNode curLeaf = (LeafNode)current;
                curLeaf.items[index] = value;
            }
        }
        public sealed override void Clear()
        {
            StopEnumerations();
            root = null;
        }
        public sealed override void Insert(int index, T item)
        {
            StopEnumerations();
            if ((uint)Count + 1 > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            if (index <= 0 || index >= Count)
            {
                if (index == 0)
                    AddToFront(item);
                else if (index == Count)
                    Add(item);
                else
                    throw new ArgumentOutOfRangeException("index");
            }
            else
            {
                if (root == null)
                    root = new LeafNode(item);
                else
                {
                    Node newRoot = root.InsertInPlace(index, item);
                    if (newRoot != root)
                    {
                        root = newRoot;
                        CheckBalance();
                    }
                }
            }
        }
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            StopEnumerations();
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (index <= 0 || index >= Count)
            {
                if (index == 0)
                    AddRangeToFront(collection);
                else if (index == Count)
                    AddRange(collection);
                else
                    throw new ArgumentOutOfRangeException("index");
            }
            else
            {
                Node node = NodeFromEnumerable(collection);
                if (node == null)
                    return;
                else if (root == null)
                    root = node;
                else
                {
                    if ((uint)Count + (uint)node.Count > MAXITEMS)
                        throw new InvalidOperationException(Strings.CollectionTooLarge);
                    Node newRoot = root.InsertInPlace(index, node, true);
                    if (newRoot != root)
                    {
                        root = newRoot;
                        CheckBalance();
                    }
                }
            }
        }
        public void InsertRange(int index, BigList1<T> list)
        {
            StopEnumerations();
            if (list == null)
                throw new ArgumentNullException("list");
            if ((uint)Count + (uint)list.Count > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            if (index <= 0 || index >= Count)
            {
                if (index == 0)
                    AddRangeToFront(list);
                else if (index == Count)
                    AddRange(list);
                else
                    throw new ArgumentOutOfRangeException("index");
            }
            else
            {
                if (list.Count == 0)
                    return;
                if (root == null)
                {
                    list.root.MarkShared();
                    root = list.root;
                }
                else
                {
                    if (list.root == root)
                        root.MarkShared();
                    Node newRoot = root.InsertInPlace(index, list.root, false);
                    if (newRoot != root)
                    {
                        root = newRoot;
                        CheckBalance();
                    }
                }
            }
        }
        public sealed override void RemoveAt(int index)
        {
            RemoveRange(index, 1);
        }
        public void RemoveRange(int index, int count)
        {
            if (count == 0)
                return;
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > Count - index)
                throw new ArgumentOutOfRangeException("count");
            StopEnumerations();
            Node newRoot = root.RemoveRangeInPlace(index, index + count - 1);
            if (newRoot != root)
            {
                root = newRoot;
                CheckBalance();
            }
        }
        public sealed override void Add(T item)
        {
            if ((uint)Count + 1 > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            StopEnumerations();
            if (root == null)
                root = new LeafNode(item);
            else
            {
                Node newRoot = root.AppendInPlace(item);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public void AddToFront(T item)
        {
            if ((uint)Count + 1 > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            StopEnumerations();
            if (root == null)
                root = new LeafNode(item);
            else
            {
                Node newRoot = root.PrependInPlace(item);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            StopEnumerations();
            Node node = NodeFromEnumerable(collection);
            if (node == null)
                return;
            else if (root == null)
            {
                root = node;
                CheckBalance();
            }
            else
            {
                if ((uint)Count + (uint)node.count > MAXITEMS)
                    throw new InvalidOperationException(Strings.CollectionTooLarge);
                Node newRoot = root.AppendInPlace(node, true);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public void AddRangeToFront(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            StopEnumerations();
            Node node = NodeFromEnumerable(collection);
            if (node == null)
                return;
            else if (root == null)
            {
                root = node;
                CheckBalance();
            }
            else
            {
                if ((uint)Count + (uint)node.Count > MAXITEMS)
                    throw new InvalidOperationException(Strings.CollectionTooLarge);
                Node newRoot = root.PrependInPlace(node, true);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public BigList1<T> Clone()
        {
            if (root == null)
                return new BigList1<T>();
            else
            {
                root.MarkShared();
                return new BigList1<T>(root);
            }
        }
        object ICloneable.Clone()
        {
            return Clone();
        }
        public BigList1<T> CloneContents()
        {
            if (root == null)
                return new BigList1<T>();
            else
            {
                bool itemIsValueType;
                if (!Util.IsCloneableType(typeof(T), out itemIsValueType))
                    throw new InvalidOperationException(string.Format(Strings.TypeNotCloneable, typeof(T).FullName));
                if (itemIsValueType)
                    return Clone();
                return new BigList1<T>(Algorithms.Convert<T, T>(this, delegate(T item)
                {
                    if (item == null)
                        return default(T);
                    else
                        return (T)(((ICloneable)item).Clone());
                }));
            }
        }
        public void AddRange(BigList1<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if ((uint)Count + (uint)list.Count > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            if (list.Count == 0)
                return;
            StopEnumerations();
            if (root == null)
            {
                list.root.MarkShared();
                root = list.root;
            }
            else
            {
                Node newRoot = root.AppendInPlace(list.root, false);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public void AddRangeToFront(BigList1<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if ((uint)Count + (uint)list.Count > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            if (list.Count == 0)
                return;
            StopEnumerations();
            if (root == null)
            {
                list.root.MarkShared();
                root = list.root;
            }
            else
            {
                Node newRoot = root.PrependInPlace(list.root, false);
                if (newRoot != root)
                {
                    root = newRoot;
                    CheckBalance();
                }
            }
        }
        public static BigList1<T> operator +(BigList1<T> first, BigList1<T> second)
        {
            if (first == null)
                throw new ArgumentNullException("first");
            if (second == null)
                throw new ArgumentNullException("second");
            if ((uint)first.Count + (uint)second.Count > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            if (first.Count == 0)
                return second.Clone();
            else if (second.Count == 0)
                return first.Clone();
            else
            {
                BigList1<T> result = new BigList1<T>(first.root.Append(second.root, false));
                result.CheckBalance();
                return result;
            }
        }
        public BigList1<T> GetRange(int index, int count)
        {
            if (count == 0)
                return new BigList1<T>();
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > Count - index)
                throw new ArgumentOutOfRangeException("count");
            return new BigList1<T>(root.Subrange(index, index + count - 1));
        }
        public sealed override IList<T> Range(int index, int count)
        {
            if (index < 0 || index > this.Count || (index == this.Count && count != 0))
                throw new ArgumentOutOfRangeException("index");
            if (count < 0 || count > this.Count || count + index > this.Count)
                throw new ArgumentOutOfRangeException("count");
            return new BigListRange(this, index, count);
        }
        private IEnumerator<T> GetEnumerator(int start, int maxItems)
        {
            int startStamp = changeStamp;
            if (root != null && maxItems > 0)
            {
                ConcatNode[] stack = new ConcatNode[root.Depth];
                bool[] leftStack = new bool[root.Depth];
                int stackPtr = 0, startIndex = 0;
                Node current = root;
                LeafNode currentLeaf;
                ConcatNode currentConcat;
                if (start != 0)
                {
                    if (start < 0 || start >= root.Count)
                        throw new ArgumentOutOfRangeException("start");
                    currentConcat = current as ConcatNode;
                    startIndex = start;
                    while (currentConcat != null)
                    {
                        stack[stackPtr] = currentConcat;
                        int leftCount = currentConcat.left.Count;
                        if (startIndex < leftCount)
                        {
                            leftStack[stackPtr] = true;
                            current = currentConcat.left;
                        }
                        else
                        {
                            leftStack[stackPtr] = false;
                            current = currentConcat.right;
                            startIndex -= leftCount;
                        }
                        ++stackPtr;
                        currentConcat = current as ConcatNode;
                    }
                }
                for (;;)
                {
                    while ((currentConcat = current as ConcatNode) != null)
                    {
                        stack[stackPtr] = currentConcat;
                        leftStack[stackPtr] = true;
                        ++stackPtr;
                        current = currentConcat.left;
                    }
                    currentLeaf = (LeafNode)current;
                    int limit = currentLeaf.Count;
                    if (limit > startIndex + maxItems)
                        limit = startIndex + maxItems;
                    for (int i = startIndex; i < limit; ++i)
                    {
                        yield return currentLeaf.items[i];
                        CheckEnumerationStamp(startStamp);
                    }
                    maxItems -= limit - startIndex;
                    if (maxItems <= 0)
                        yield break;
                    startIndex = 0;
                    for (;;)
                    {
                        ConcatNode parent;
                        if (stackPtr == 0)
                            yield break;
                        parent = stack[--stackPtr];
                        if (leftStack[stackPtr])
                        {
                            leftStack[stackPtr] = false;
                            ++stackPtr;
                            current = parent.right;
                            break;
                        }
                        current = parent;
                    }
                }
            }
        }
        public sealed override IEnumerator<T> GetEnumerator()
        {
            return GetEnumerator(0, int.MaxValue);
        }
        private static Node NodeFromEnumerable(IEnumerable<T> collection)
        {
            Node node = null;
            LeafNode leaf;
            IEnumerator<T> enumerator = collection.GetEnumerator();
            while ((leaf = LeafFromEnumerator(enumerator)) != null)
            {
                if (node == null)
                    node = leaf;
                else
                {
                    if ((uint)(node.count) + (uint)(leaf.count) > MAXITEMS)
                        throw new InvalidOperationException(Strings.CollectionTooLarge);
                    node = node.AppendInPlace(leaf, true);
                }
            }
            return node;
        }
        private static LeafNode LeafFromEnumerator(IEnumerator<T> enumerator)
        {
            int i = 0;
            T[] items = null;
            while (i < MAXLEAF && enumerator.MoveNext())
            {
                if (i == 0)
                    items = new T[MAXLEAF];
                if (items != null)
                    items[i++] = enumerator.Current;
            }
            if (items != null)
                return new LeafNode(i, items);
            else
                return null;
        }
        private static Node NCopiesOfNode(int copies, Node node)
        {
            if (copies < 0)
                throw new ArgumentOutOfRangeException("copies", Strings.ArgMustNotBeNegative);
            if (copies == 0 || node == null)
                return null;
            if (copies == 1)
                return node;
            if (copies * (long)(node.count) > MAXITEMS)
                throw new InvalidOperationException(Strings.CollectionTooLarge);
            int n = 1;
            Node power = node, builder = null;
            while (copies > 0)
            {
                power.MarkShared();
                if ((copies & n) != 0)
                {
                    copies -= n;
                    if (builder == null)
                        builder = power;
                    else
                        builder = builder.Append(power, false);
                }
                n *= 2;
                power = power.Append(power, false);
            }
            return builder;
        }
        private void CheckBalance()
        {
            if (root != null &&
                (root.Depth > BALANCEFACTOR && !(root.Depth - BALANCEFACTOR <= MAXFIB &&
                                                 Count >= FIBONACCI[root.Depth - BALANCEFACTOR])))
            {
                Rebalance();
            }
        }
        internal void Rebalance()
        {
            Node[] rebalanceArray;
            int slots;
            if (root == null)
                return;
            if (root.Depth <= 1 || (root.Depth - 2 <= MAXFIB && Count >= FIBONACCI[root.Depth - 2]))
                return;
            for (slots = 0; slots <= MAXFIB; ++slots)
                if (root.Count < FIBONACCI[slots])
                    break;
            rebalanceArray = new Node[slots];
            AddNodeToRebalanceArray(rebalanceArray, root, false);
            Node result = null;
            for (int slot = 0; slot < slots; ++slot)
            {
                Node n = rebalanceArray[slot];
                if (n != null)
                {
                    if (result == null)
                        result = n;
                    else
                        result = result.PrependInPlace(n, !n.Shared);
                }
            }
            root = result;
            Debug.Assert(root.Depth <= 1 || (root.Depth - 2 <= MAXFIB && Count >= FIBONACCI[root.Depth - 2]));
        }
        private void AddNodeToRebalanceArray(Node[] rebalanceArray, Node node, bool shared)
        {
            if (node.Shared)
                shared = true;
            if (node.IsBalanced())
            {
                if (shared)
                    node.MarkShared();
                AddBalancedNodeToRebalanceArray(rebalanceArray, node);
            }
            else
            {
                ConcatNode n = (ConcatNode)node;
                AddNodeToRebalanceArray(rebalanceArray, n.left, shared);
                AddNodeToRebalanceArray(rebalanceArray, n.right, shared);
            }
        }
        private static void AddBalancedNodeToRebalanceArray(Node[] rebalanceArray, Node balancedNode)
        {
            int slot;
            int count;
            Node accum = null;
            Debug.Assert(balancedNode.IsBalanced());
            count = balancedNode.Count;
            slot = 0;
            while (count >= FIBONACCI[slot + 1])
            {
                Node n = rebalanceArray[slot];
                if (n != null)
                {
                    rebalanceArray[slot] = null;
                    if (accum == null)
                        accum = n;
                    else
                        accum = accum.PrependInPlace(n, !n.Shared);
                }
                ++slot;
            }
            if (accum != null)
                balancedNode = balancedNode.PrependInPlace(accum, !accum.Shared);
            for (;;)
            {
                Node n = rebalanceArray[slot];
                if (n != null)
                {
                    rebalanceArray[slot] = null;
                    balancedNode = balancedNode.PrependInPlace(n, !n.Shared);
                }
                if (balancedNode.Count < FIBONACCI[slot + 1])
                {
                    rebalanceArray[slot] = balancedNode;
                    break;
                }
                ++slot;
            }
#if DEBUG
            for (int i = 0; i < rebalanceArray.Length; ++i)
            {
                if (rebalanceArray[i] != null)
                    Debug.Assert(rebalanceArray[i].IsAlmostBalanced());
            }
#endif //DEBUG
        }
        public new BigList<TDest> ConvertAll<TDest>(Converter<T, TDest> converter)
        {
            return new BigList<TDest>(Algorithms.Convert(this, converter));
        }
        public void Reverse()
        {
            Algorithms.ReverseInPlace(this);
        }
        public void Reverse(int start, int count)
        {
            Algorithms.ReverseInPlace(Range(start, count));
        }
        public void Sort()
        {
            Sort(Comparers.DefaultComparer<T>());
        }
        public void Sort(IComparer<T> comparer)
        {
            Algorithms.SortInPlace(this, comparer);
        }
        public void Sort(Comparison<T> comparison)
        {
            Sort(Comparers.ComparerFromComparison(comparison));
        }
        public int BinarySearch(T item)
        {
            return BinarySearch(item, Comparers.DefaultComparer<T>());
        }
        public int BinarySearch(T item, IComparer<T> comparer)
        {
            int count, index;
            count = Algorithms.BinarySearch(this, item, comparer, out index);
            if (count == 0)
                return (~index);
            else
                return index;
        }
        public int BinarySearch(T item, Comparison<T> comparison)
        {
            return BinarySearch(item, Comparers.ComparerFromComparison(comparison));
        }
#if DEBUG
        public void Validate()
        {
            if (root != null)
            {
                root.Validate();
                Debug.Assert(Count != 0);
            }
            else
                Debug.Assert(Count == 0);
        }
        public void Print()
        {
            Console.WriteLine("SERIES: Count={0}", Count);
            if (Count > 0)
            {
                Console.Write("ITEMS: ");
                foreach (T item in this)
                {
                    Console.Write("{0} ", item);
                }
                Console.WriteLine();
                Console.WriteLine("TREE:");
                root.Print("      ", "      ");
            }
            Console.WriteLine();
        }
#endif //DEBUG
        [Serializable]
        private abstract class Node
        {
            public int count;
            protected volatile bool shared;
            public int Count
            {
                get { return count; }
            }
            public bool Shared
            {
                get { return shared; }
            }
            public void MarkShared()
            {
                shared = true;
            }
            public abstract int Depth { get; }
            public abstract T GetAt(int index);
            public abstract Node Subrange(int first, int last);
            public abstract Node SetAt(int index, T item);
            public abstract Node SetAtInPlace(int index, T item);
            public abstract Node Append(Node node, bool nodeIsUnused);
            public abstract Node AppendInPlace(Node node, bool nodeIsUnused);
            public abstract Node AppendInPlace(T item);
            public abstract Node RemoveRange(int first, int last);
            public abstract Node RemoveRangeInPlace(int first, int last);
            public abstract Node Insert(int index, Node node, bool nodeIsUnused);
            public abstract Node InsertInPlace(int index, T item);
            public abstract Node InsertInPlace(int index, Node node, bool nodeIsUnused);
#if DEBUG
            public abstract void Validate();
            public abstract void Print(string prefixNode, string prefixChildren);
#endif //DEBUG
            public Node Prepend(Node node, bool nodeIsUnused)
            {
                if (nodeIsUnused)
                    return node.AppendInPlace(this, false);
                else
                    return node.Append(this, false);
            }
            public Node PrependInPlace(Node node, bool nodeIsUnused)
            {
                if (nodeIsUnused)
                    return node.AppendInPlace(this, !this.shared);
                else
                    return node.Append(this, !this.shared);
            }
            public abstract Node PrependInPlace(T item);
            public bool IsBalanced()
            {
                return (Depth <= MAXFIB && Count >= FIBONACCI[Depth]);
            }
            public bool IsAlmostBalanced()
            {
                return (Depth == 0 || (Depth - 1 <= MAXFIB && Count >= FIBONACCI[Depth - 1]));
            }
        }
        [Serializable]
        private sealed class LeafNode : Node
        {
            public T[] items;
            public LeafNode(T item)
            {
                count = 1;
                items = new T[MAXLEAF];
                items[0] = item;
            }
            public LeafNode(int count, T[] newItems)
            {
                Debug.Assert(count <= newItems.Length && count > 0);
                Debug.Assert(newItems.Length <= MAXLEAF);
                this.count = count;
                items = newItems;
            }
            public override int Depth
            {
                get { return 0; }
            }
            public override T GetAt(int index)
            {
                return items[index];
            }
            public override Node SetAtInPlace(int index, T item)
            {
                if (shared)
                    return SetAt(index, item);
                items[index] = item;
                return this;
            }
            public override Node SetAt(int index, T item)
            {
                T[] newItems = (T[])items.Clone();
                newItems[index] = item;
                return new LeafNode(count, newItems);
            }
            private bool MergeLeafInPlace(Node other)
            {
                Debug.Assert(!shared);
                LeafNode otherLeaf = (other as LeafNode);
                int newCount;
                if (otherLeaf != null && (newCount = otherLeaf.Count + this.count) <= MAXLEAF)
                {
                    if (newCount > items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        Array.Copy(items, 0, newItems, 0, count);
                        items = newItems;
                    }
                    Array.Copy(otherLeaf.items, 0, items, count, otherLeaf.count);
                    count = newCount;
                    return true;
                }
                return false;
            }
            private Node MergeLeaf(Node other)
            {
                LeafNode otherLeaf = (other as LeafNode);
                int newCount;
                if (otherLeaf != null && (newCount = otherLeaf.Count + this.count) <= MAXLEAF)
                {
                    T[] newItems = new T[MAXLEAF];
                    Array.Copy(items, 0, newItems, 0, count);
                    Array.Copy(otherLeaf.items, 0, newItems, count, otherLeaf.count);
                    return new LeafNode(newCount, newItems);
                }
                return null;
            }
            public override Node PrependInPlace(T item)
            {
                if (shared)
                    return Prepend(new LeafNode(item), true);
                if (count < MAXLEAF)
                {
                    if (count == items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        Array.Copy(items, 0, newItems, 1, count);
                        items = newItems;
                    }
                    else
                    {
                        Array.Copy(items, 0, items, 1, count);
                    }
                    items[0] = item;
                    count += 1;
                    return this;
                }
                else
                {
                    return new ConcatNode(new LeafNode(item), this);
                }
            }
            public override Node AppendInPlace(T item)
            {
                if (shared)
                    return Append(new LeafNode(item), true);
                if (count < MAXLEAF)
                {
                    if (count == items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        Array.Copy(items, 0, newItems, 0, count);
                        items = newItems;
                    }
                    items[count] = item;
                    count += 1;
                    return this;
                }
                else
                {
                    return new ConcatNode(this, new LeafNode(item));
                }
            }
            public override Node AppendInPlace(Node node, bool nodeIsUnused)
            {
                if (shared)
                    return Append(node, nodeIsUnused);
                if (MergeLeafInPlace(node))
                {
                    return this;
                }
                ConcatNode otherConcat = (node as ConcatNode);
                if (otherConcat != null && MergeLeafInPlace(otherConcat.left))
                {
                    if (!nodeIsUnused)
                        otherConcat.right.MarkShared();
                    return new ConcatNode(this, otherConcat.right);
                }
                if (!nodeIsUnused)
                    node.MarkShared();
                return new ConcatNode(this, node);
            }
            public override Node Append(Node node, bool nodeIsUnused)
            {
                Node result;
                if ((result = MergeLeaf(node)) != null)
                    return result;
                ConcatNode otherConcat = (node as ConcatNode);
                if (otherConcat != null && (result = MergeLeaf(otherConcat.left)) != null)
                {
                    if (!nodeIsUnused)
                        otherConcat.right.MarkShared();
                    return new ConcatNode(result, otherConcat.right);
                }
                if (!nodeIsUnused)
                    node.MarkShared();
                MarkShared();
                return new ConcatNode(this, node);
            }
            public override Node InsertInPlace(int index, T item)
            {
                if (shared)
                    return Insert(index, new LeafNode(item), true);
                if (count < MAXLEAF)
                {
                    if (count == items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        if (index > 0)
                            Array.Copy(items, 0, newItems, 0, index);
                        if (count > index)
                            Array.Copy(items, index, newItems, index + 1, count - index);
                        items = newItems;
                    }
                    else
                    {
                        if (count > index)
                            Array.Copy(items, index, items, index + 1, count - index);
                    }
                    items[index] = item;
                    count += 1;
                    return this;
                }
                else
                {
                    if (index == count)
                    {
                        return new ConcatNode(this, new LeafNode(item));
                    }
                    else if (index == 0)
                    {
                        return new ConcatNode(new LeafNode(item), this);
                    }
                    else
                    {
                        T[] leftItems = new T[MAXLEAF];
                        Array.Copy(items, 0, leftItems, 0, index);
                        leftItems[index] = item;
                        Node leftNode = new LeafNode(index + 1, leftItems);
                        T[] rightItems = new T[count - index];
                        Array.Copy(items, index, rightItems, 0, count - index);
                        Node rightNode = new LeafNode(count - index, rightItems);
                        return new ConcatNode(leftNode, rightNode);
                    }
                }
            }
            public override Node InsertInPlace(int index, Node node, bool nodeIsUnused)
            {
                if (shared)
                    return Insert(index, node, nodeIsUnused);
                LeafNode otherLeaf = (node as LeafNode);
                int newCount;
                if (otherLeaf != null && (newCount = otherLeaf.Count + this.count) <= MAXLEAF)
                {
                    if (newCount > items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        Array.Copy(items, 0, newItems, 0, index);
                        Array.Copy(otherLeaf.items, 0, newItems, index, otherLeaf.Count);
                        Array.Copy(items, index, newItems, index + otherLeaf.Count, count - index);
                        items = newItems;
                    }
                    else
                    {
                        Array.Copy(items, index, items, index + otherLeaf.Count, count - index);
                        Array.Copy(otherLeaf.items, 0, items, index, otherLeaf.count);
                    }
                    count = newCount;
                    return this;
                }
                else if (index == 0)
                {
                    return PrependInPlace(node, nodeIsUnused);
                }
                else if (index == count)
                {
                    return AppendInPlace(node, nodeIsUnused);
                }
                else
                {
                    T[] leftItems = new T[index];
                    Array.Copy(items, 0, leftItems, 0, index);
                    Node leftNode = new LeafNode(index, leftItems);
                    T[] rightItems = new T[count - index];
                    Array.Copy(items, index, rightItems, 0, count - index);
                    Node rightNode = new LeafNode(count - index, rightItems);
                    leftNode = leftNode.AppendInPlace(node, nodeIsUnused);
                    leftNode = leftNode.AppendInPlace(rightNode, true);
                    return leftNode;
                }
            }
            public override Node Insert(int index, Node node, bool nodeIsUnused)
            {
                LeafNode otherLeaf = (node as LeafNode);
                int newCount;
                if (otherLeaf != null && (newCount = otherLeaf.Count + this.count) <= MAXLEAF)
                {
                    T[] newItems = new T[MAXLEAF];
                    Array.Copy(items, 0, newItems, 0, index);
                    Array.Copy(otherLeaf.items, 0, newItems, index, otherLeaf.Count);
                    Array.Copy(items, index, newItems, index + otherLeaf.Count, count - index);
                    return new LeafNode(newCount, newItems);
                }
                else if (index == 0)
                {
                    return Prepend(node, nodeIsUnused);
                }
                else if (index == count)
                {
                    return Append(node, nodeIsUnused);
                }
                else
                {
                    T[] leftItems = new T[index];
                    Array.Copy(items, 0, leftItems, 0, index);
                    Node leftNode = new LeafNode(index, leftItems);
                    T[] rightItems = new T[count - index];
                    Array.Copy(items, index, rightItems, 0, count - index);
                    Node rightNode = new LeafNode(count - index, rightItems);
                    leftNode = leftNode.AppendInPlace(node, nodeIsUnused);
                    leftNode = leftNode.AppendInPlace(rightNode, true);
                    return leftNode;
                }
            }
            public override Node RemoveRangeInPlace(int first, int last)
            {
                if (shared)
                    return RemoveRange(first, last);
                Debug.Assert(first <= last);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    return null;
                }
                if (first < 0)
                    first = 0;
                if (last >= count)
                    last = count - 1;
                int newCount = first + (count - last - 1);
                if (count > last + 1)
                    Array.Copy(items, last + 1, items, first, count - last - 1);
                for (int i = newCount; i < count; ++i)
                    items[i] = default(T);
                count = newCount;
                return this;
            }
            public override Node RemoveRange(int first, int last)
            {
                Debug.Assert(first <= last);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    return null;
                }
                if (first < 0)
                    first = 0;
                if (last >= count)
                    last = count - 1;
                int newCount = first + (count - last - 1);
                T[] newItems = new T[newCount];
                if (first > 0)
                    Array.Copy(items, 0, newItems, 0, first);
                if (count > last + 1)
                    Array.Copy(items, last + 1, newItems, first, count - last - 1);
                return new LeafNode(newCount, newItems);
            }
            public override Node Subrange(int first, int last)
            {
                Debug.Assert(first <= last);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    MarkShared();
                    return this;
                }
                else
                {
                    if (first < 0)
                        first = 0;
                    if (last >= count)
                        last = count - 1;
                    int n = last - first + 1;
                    T[] newItems = new T[n];
                    Array.Copy(items, first, newItems, 0, n);
                    return new LeafNode(n, newItems);
                }
            }
#if DEBUG
            public override void Validate()
            {
                Debug.Assert(count > 0);
                Debug.Assert(items != null);
                Debug.Assert(items.Length > 0);
                Debug.Assert(count <= MAXLEAF);
                Debug.Assert(items.Length <= MAXLEAF);
                Debug.Assert(count <= items.Length);
            }
            public override void Print(string prefixNode, string prefixChildren)
            {
                Console.Write("{0}LEAF {1} count={2}/{3} ", prefixNode, shared ? "S" : " ", count, items.Length);
                for (int i = 0; i < count; ++i)
                    Console.Write("{0} ", items[i]);
                Console.WriteLine();
            }
#endif //DEBUG
        }
        [Serializable]
        private sealed class ConcatNode : Node
        {
            public Node left, right;
            private short depth;
            public override int Depth
            {
                get { return depth; }
            }
            public ConcatNode(Node left, Node right)
            {
                Debug.Assert(left != null && right != null);
                this.left = left;
                this.right = right;
                this.count = left.Count + right.Count;
                if (left.Depth > right.Depth)
                    this.depth = (short)(left.Depth + 1);
                else
                    this.depth = (short)(right.Depth + 1);
            }
            private Node NewNode(Node newLeft, Node newRight)
            {
                if (left == newLeft)
                {
                    if (right == newRight)
                    {
                        MarkShared();
                        return this;
                    }
                    else
                        left.MarkShared();
                }
                else
                {
                    if (right == newRight)
                        right.MarkShared();
                }
                if (newLeft == null)
                    return newRight;
                else if (newRight == null)
                    return newLeft;
                else
                    return new ConcatNode(newLeft, newRight);
            }
            private Node NewNodeInPlace(Node newLeft, Node newRight)
            {
                Debug.Assert(!shared);
                if (newLeft == null)
                    return newRight;
                else if (newRight == null)
                    return newLeft;
                left = newLeft;
                right = newRight;
                count = left.Count + right.Count;
                if (left.Depth > right.Depth)
                    depth = (short)(left.Depth + 1);
                else
                    depth = (short)(right.Depth + 1);
                return this;
            }
            public override T GetAt(int index)
            {
                int leftCount = left.Count;
                if (index < leftCount)
                    return left.GetAt(index);
                else
                    return right.GetAt(index - leftCount);
            }
            public override Node SetAtInPlace(int index, T item)
            {
                if (shared)
                    return SetAt(index, item);
                int leftCount = left.Count;
                if (index < leftCount)
                {
                    Node newLeft = left.SetAtInPlace(index, item);
                    if (newLeft != left)
                        return NewNodeInPlace(newLeft, right);
                    else
                        return this;
                }
                else
                {
                    Node newRight = right.SetAtInPlace(index - leftCount, item);
                    if (newRight != right)
                        return NewNodeInPlace(left, newRight);
                    else
                        return this;
                }
            }
            public override Node SetAt(int index, T item)
            {
                int leftCount = left.Count;
                if (index < leftCount)
                {
                    return NewNode(left.SetAt(index, item), right);
                }
                else
                {
                    return NewNode(left, right.SetAt(index - leftCount, item));
                }
            }
            public override Node PrependInPlace(T item)
            {
                if (shared)
                    return Prepend(new LeafNode(item), true);
                LeafNode leftLeaf;
                if (left.Count < MAXLEAF && !left.Shared && (leftLeaf = left as LeafNode) != null)
                {
                    int c = leftLeaf.Count;
                    if (c == leftLeaf.items.Length)
                    {
                        T[] newItems = new T[MAXLEAF];
                        Array.Copy(leftLeaf.items, 0, newItems, 1, c);
                        leftLeaf.items = newItems;
                    }
                    else
                    {
                        Array.Copy(leftLeaf.items, 0, leftLeaf.items, 1, c);
                    }
                    leftLeaf.items[0] = item;
                    leftLeaf.count += 1;
                    this.count += 1;
                    return this;
                }
                else
                    return new ConcatNode(new LeafNode(item), this);
            }
            public override Node AppendInPlace(T item)
            {
                if (shared)
                    return Append(new LeafNode(item), true);
                LeafNode rightLeaf;
                if (right.Count < MAXLEAF && !right.Shared && (rightLeaf = right as LeafNode) != null)
                {
                    int c = rightLeaf.Count;
                    if (c == rightLeaf.items.Length)
                    {
                        T[] newItems =
                            new T[MAXLEAF];
                        Array.Copy(rightLeaf.items, 0, newItems, 0, c);
                        rightLeaf.items = newItems;
                    }
                    rightLeaf.items[c] = item;
                    rightLeaf.count += 1;
                    this.count += 1;
                    return this;
                }
                else
                    return new ConcatNode(this, new LeafNode(item));
            }
            public override Node AppendInPlace(Node node, bool nodeIsUnused)
            {
                if (shared)
                    return Append(node, nodeIsUnused);
                if (right.Count + node.Count <= MAXLEAF && right is LeafNode && node is LeafNode)
                    return NewNodeInPlace(left, right.AppendInPlace(node, nodeIsUnused));
                if (!nodeIsUnused)
                    node.MarkShared();
                return new ConcatNode(this, node);
            }
            public override Node Append(Node node, bool nodeIsUnused)
            {
                if (right.Count + node.Count <= MAXLEAF && right is LeafNode && node is LeafNode)
                    return NewNode(left, right.Append(node, nodeIsUnused));
                this.MarkShared();
                if (!nodeIsUnused)
                    node.MarkShared();
                return new ConcatNode(this, node);
            }
            public override Node InsertInPlace(int index, T item)
            {
                if (shared)
                    return Insert(index, new LeafNode(item), true);
                int leftCount = left.Count;
                if (index <= leftCount)
                    return NewNodeInPlace(left.InsertInPlace(index, item), right);
                else
                    return NewNodeInPlace(left, right.InsertInPlace(index - leftCount, item));
            }
            public override Node InsertInPlace(int index, Node node, bool nodeIsUnused)
            {
                if (shared)
                    return Insert(index, node, nodeIsUnused);
                int leftCount = left.Count;
                if (index < leftCount)
                    return NewNodeInPlace(left.InsertInPlace(index, node, nodeIsUnused), right);
                else
                    return NewNodeInPlace(left, right.InsertInPlace(index - leftCount, node, nodeIsUnused));
            }
            public override Node Insert(int index, Node node, bool nodeIsUnused)
            {
                int leftCount = left.Count;
                if (index < leftCount)
                    return NewNode(left.Insert(index, node, nodeIsUnused), right);
                else
                    return NewNode(left, right.Insert(index - leftCount, node, nodeIsUnused));
            }
            public override Node RemoveRangeInPlace(int first, int last)
            {
                if (shared)
                    return RemoveRange(first, last);
                Debug.Assert(first < count);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    return null;
                }
                int leftCount = left.Count;
                Node newLeft = left, newRight = right;
                if (first < leftCount)
                    newLeft = left.RemoveRangeInPlace(first, last);
                if (last >= leftCount)
                    newRight = right.RemoveRangeInPlace(first - leftCount, last - leftCount);
                return NewNodeInPlace(newLeft, newRight);
            }
            public override Node RemoveRange(int first, int last)
            {
                Debug.Assert(first < count);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    return null;
                }
                int leftCount = left.Count;
                Node newLeft = left, newRight = right;
                if (first < leftCount)
                    newLeft = left.RemoveRange(first, last);
                if (last >= leftCount)
                    newRight = right.RemoveRange(first - leftCount, last - leftCount);
                return NewNode(newLeft, newRight);
            }
            public override Node Subrange(int first, int last)
            {
                Debug.Assert(first < count);
                Debug.Assert(last >= 0);
                if (first <= 0 && last >= count - 1)
                {
                    MarkShared();
                    return this;
                }
                int leftCount = left.Count;
                Node leftPart = null, rightPart = null;
                if (first < leftCount)
                    leftPart = left.Subrange(first, last);
                if (last >= leftCount)
                    rightPart = right.Subrange(first - leftCount, last - leftCount);
                Debug.Assert(leftPart != null || rightPart != null);
                if (leftPart == null)
                    return rightPart;
                else if (rightPart == null)
                    return leftPart;
                else
                    return new ConcatNode(leftPart, rightPart);
            }
#if DEBUG
            public override void Validate()
            {
                Debug.Assert(left != null);
                Debug.Assert(right != null);
                Debug.Assert(Depth > 0);
                Debug.Assert(Count > 0);
                Debug.Assert(Math.Max(left.Depth, right.Depth) + 1 == Depth);
                Debug.Assert(left.Count + right.Count == Count);
                left.Validate();
                right.Validate();
            }
            public override void Print(string prefixNode, string prefixChildren)
            {
                Console.WriteLine("{0}CONCAT {1} {2} count={3} depth={4}", prefixNode, shared ? "S" : " ",
                    IsBalanced() ? "B" : (IsAlmostBalanced() ? "A" : " "), count, depth);
                left.Print(prefixChildren + "|-L-", prefixChildren + "|  ");
                right.Print(prefixChildren + "|-R-", prefixChildren + "   ");
            }
#endif //DEBUG
        }
        [Serializable]
        private class BigListRange : ListBase<T>
        {
            private readonly BigList1<T> wrappedList;
            private readonly int start;
            private int count;
            public BigListRange(BigList1<T> wrappedList, int start, int count)
            {
                this.wrappedList = wrappedList;
                this.start = start;
                this.count = count;
            }
            public override int Count
            {
                get { return Math.Min(count, wrappedList.Count - start); }
            }
            public override void Clear()
            {
                if (wrappedList.Count - start < count)
                    count = wrappedList.Count - start;
                while (count > 0)
                {
                    wrappedList.RemoveAt(start + count - 1);
                    --count;
                }
            }
            public override void Insert(int index, T item)
            {
                if (index < 0 || index > count)
                    throw new ArgumentOutOfRangeException("index");
                wrappedList.Insert(start + index, item);
                ++count;
            }
            public override void RemoveAt(int index)
            {
                if (index < 0 || index >= count)
                    throw new ArgumentOutOfRangeException("index");
                wrappedList.RemoveAt(start + index);
                --count;
            }
            public override T this[int index]
            {
                get
                {
                    if (index < 0 || index >= count)
                        throw new ArgumentOutOfRangeException("index");
                    return wrappedList[start + index];
                }
                set
                {
                    if (index < 0 || index >= count)
                        throw new ArgumentOutOfRangeException("index");
                    wrappedList[start + index] = value;
                }
            }
            public override IEnumerator<T> GetEnumerator()
            {
                return wrappedList.GetEnumerator(start, count);
            }
        }
    }
}