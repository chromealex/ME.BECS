using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;

namespace NativeTrees {

    /// <summary>
    /// A simple binary heap useful for nearest neighbour queries
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TComp"></typeparam>
    public struct NativeMinHeap<T, TComp> : INativeDisposable
        where T : unmanaged
        where TComp : struct, IComparer<T> {

        private NativeList<T> values;
        private TComp comparer;

        public NativeMinHeap(TComp comparer, Allocator allocator) {
            this.comparer = comparer;
            this.values = new NativeList<T>(allocator);
            this.values.Add(default);
        }

        public void Clear() {
            this.values.Clear();
            this.values.Add(default);
        }

        public int Count => this.values.Length - 1;
        public T Min => this.values[1];

        public bool TryPop(out T value) {
            if (this.Count > 0) {
                value = this.Pop();
                return true;
            }

            value = default;
            return false;
        }

        public T Pop() {
            var count = this.Count;
            if (count == 0) {
                throw new InvalidOperationException("Heap is empty.");
            }

            var min = this.Min;
            this.values[1] = this.values[count];
            this.values.RemoveAt(count);

            if (this.values.Length > 1) {
                this.BubbleDown(1);
            }

            return min;
        }

        public void Push(T item) {
            this.values.Add(item);
            this.BubbleUp(this.Count);
        }

        private void BubbleUp(int index) {
            var parent = index / 2;
            while (index > 1 && this.CompareResult(parent, index) > 0) {
                this.Exchange(index, parent);
                index = parent;
                parent /= 2;
            }
        }

        private void BubbleDown(int index) {
            int min;

            while (true) {
                var left = index * 2;
                var right = left + 1;

                if (left < this.values.Length && this.CompareResult(left, index) < 0) {
                    min = left;
                } else {
                    min = index;
                }

                if (right < this.values.Length && this.CompareResult(right, min) < 0) {
                    min = right;
                }

                if (min != index) {
                    this.Exchange(index, min);
                    index = min;
                } else {
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int CompareResult(int index1, int index2) {
            return this.comparer.Compare(this.values[index1], this.values[index2]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Exchange(int index, int max) {
            var tmp = this.values[index];
            this.values[index] = this.values[max];
            this.values[max] = tmp;
        }

        public void Dispose() {
            this.values.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps) {
            return this.values.Dispose(inputDeps);
        }

    }

}