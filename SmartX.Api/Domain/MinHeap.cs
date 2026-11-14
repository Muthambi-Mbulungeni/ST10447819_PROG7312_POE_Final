using System;
using System.Collections.Generic;

namespace SmartX.Api.Domain
{
    /// <summary>
    /// Custom binary Min-Heap data structure used for Prim's Minimum Spanning Tree algorithm,
    /// Dijkstra shortest path computation, and priority alert processing.
    /// Provides O(log N) insertion, O(log N) extraction of minimum priority element, and O(1) peek.
    /// </summary>
    /// <typeparam name="TElement">Element data type.</typeparam>
    /// <typeparam name="TPriority">Comparable priority/weight type.</typeparam>
    public class MinHeap<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _heap = new();

        public int Count => _heap.Count;
        public bool IsEmpty => _heap.Count == 0;

        public void Enqueue(TElement element, TPriority priority)
        {
            _heap.Add((element, priority));
            HeapifyUp(_heap.Count - 1);
        }

        public TElement Dequeue()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Heap is empty.");

            var root = _heap[0].Element;
            var lastIndex = _heap.Count - 1;

            _heap[0] = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);

            if (_heap.Count > 0)
            {
                HeapifyDown(0);
            }

            return root;
        }

        public (TElement Element, TPriority Priority) DequeueWithPriority()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Heap is empty.");

            var root = _heap[0];
            var lastIndex = _heap.Count - 1;

            _heap[0] = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);

            if (_heap.Count > 0)
            {
                HeapifyDown(0);
            }

            return root;
        }

        public TElement Peek()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("Heap is empty.");

            return _heap[0].Element;
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_heap[index].Priority.CompareTo(_heap[parentIndex].Priority) >= 0)
                    break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            int lastIndex = _heap.Count - 1;
            while (true)
            {
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;
                int smallest = index;

                if (leftChild <= lastIndex && _heap[leftChild].Priority.CompareTo(_heap[smallest].Priority) < 0)
                {
                    smallest = leftChild;
                }

                if (rightChild <= lastIndex && _heap[rightChild].Priority.CompareTo(_heap[smallest].Priority) < 0)
                {
                    smallest = rightChild;
                }

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int i, int j)
        {
            var temp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = temp;
        }

        public void Clear()
        {
            _heap.Clear();
        }
    }
}
