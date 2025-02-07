//MIT License
//
//Copyright(c) 2018 Vili Volčini / viliwonka
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//
// Modifed 2019 Arthur Brussee
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using static ME.BECS.Cuts;

namespace ME.BECS.NativeCollections {

    public static unsafe class UnsafeUtilityEx {

        public static T* AllocArray<T>(int length, Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(length * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), allocator);
        }

    }

}

namespace ME.BECS.NativeCollections {

    public static class HeapUtils {

        public static uint Parent(uint index) {
            return index / 2;
        }

        public static uint Left(uint index) {
            return index * 2;
        }

        public static uint Right(uint index) {
            return index * 2 + 1;
        }

    }

    // Sorted heap with a self balancing tree
    // Can act as either a min or max heap
    public unsafe struct MinMaxHeap<T> : IDisposable where T : unmanaged {

        [NativeDisableContainerSafetyRestriction]
        private safe_ptr<T> keys; //objects

        [NativeDisableContainerSafetyRestriction]
        private safe_ptr<tfloat> values;

        public uint Count;
        private uint m_capacity;

        public tfloat HeadValue => this.values[1];
        private T HeadKey => this.keys[1];

        public bool IsFull => this.Count == this.m_capacity;

        private Allocator m_allocator;

        public MinMaxHeap(uint startCapacity, Allocator allocator) {
            this.Count = 0;
            this.m_allocator = allocator;

            // Now alloc starting arrays
            this.m_capacity = startCapacity;
            this.values = _makeArray<tfloat>(startCapacity + 1u, this.m_allocator);
            this.keys = _makeArray<T>(startCapacity + 1, this.m_allocator);
        }

        private void Swap(uint indexA, uint indexB) {
            var tempVal = this.values[indexA];
            this.values[indexA] = this.values[indexB];
            this.values[indexB] = tempVal;

            var tempKey = this.keys[indexA];
            this.keys[indexA] = this.keys[indexB];
            this.keys[indexB] = tempKey;
        }

        public void Dispose() {
            _free(this.values, this.m_allocator);
            _free(this.keys, this.m_allocator);
            this.values = default;
            this.keys = default;
        }

        public void Resize(uint newSize) {
            // Allocate more spaces
            var newValues = _makeArray<tfloat>(newSize + 1, this.m_allocator);
            var newKeys = _makeArray<T>(newSize + 1, this.m_allocator);

            // Copy over old arrays
            UnsafeUtility.MemCpy(newValues.ptr, this.values.ptr, (this.m_capacity + 1) * sizeof(int));
            UnsafeUtility.MemCpy(newKeys.ptr, this.keys.ptr, (this.m_capacity + 1) * sizeof(int));

            // Get rid of old arrays
            this.Dispose();

            // And now use old arrays
            this.values = newValues;
            this.keys = newKeys;
            this.m_capacity = newSize;
        }

        // bubble down, MaxHeap version
        private void BubbleDownMax(uint index) {
            var l = HeapUtils.Left(index);
            var r = HeapUtils.Right(index);

            // bubbling down, 2 kids
            while (r <= this.Count) {
                // if heap property is violated between index and Left child
                if (this.values[index] < this.values[l]) {
                    if (this.values[l] < this.values[r]) {
                        this.Swap(index, r); // left has bigger priority
                        index = r;
                    } else {
                        this.Swap(index, l); // right has bigger priority
                        index = l;
                    }
                } else {
                    // if heap property is violated between index and R
                    if (this.values[index] < this.values[r]) {
                        this.Swap(index, r);
                        index = r;
                    } else {
                        index = l;
                        l = HeapUtils.Left(index);
                        break;
                    }
                }

                l = HeapUtils.Left(index);
                r = HeapUtils.Right(index);
            }

            // only left & last children available to test and swap
            if (l <= this.Count && this.values[index] < this.values[l]) {
                this.Swap(index, l);
            }
        }

        private void BubbleDownMin(uint index) {
            var l = HeapUtils.Left(index);
            var r = HeapUtils.Right(index);

            // bubbling down, 2 kids
            while (r <= this.Count) {
                // if heap property is violated between index and Left child
                if (this.values[index] > this.values[l]) {
                    if (this.values[l] > this.values[r]) {
                        this.Swap(index, r); // right has smaller priority
                        index = r;
                    } else {
                        this.Swap(index, l); // left has smaller priority
                        index = l;
                    }
                } else {
                    // if heap property is violated between index and R
                    if (this.values[index] > this.values[r]) {
                        this.Swap(index, r);
                        index = r;
                    } else {
                        index = l;
                        l = HeapUtils.Left(index);
                        break;
                    }
                }

                l = HeapUtils.Left(index);
                r = HeapUtils.Right(index);
            }

            // only left & last children available to test and swap
            if (l <= this.Count && this.values[index] > this.values[l]) {
                this.Swap(index, l);
            }
        }

        private void BubbleUpMax(uint index) {
            var p = HeapUtils.Parent(index);

            //swap, until Heap property isn't violated anymore
            while (p > 0 && this.values[p] < this.values[index]) {
                this.Swap(p, index);
                index = p;
                p = HeapUtils.Parent(index);
            }
        }

        private void BubbleUpMin(uint index) {
            var p = HeapUtils.Parent(index);

            //swap, until Heap property isn't violated anymore
            while (p > 0 && this.values[p] > this.values[index]) {
                this.Swap(p, index);
                index = p;
                p = HeapUtils.Parent(index);
            }
        }

        public void PushObjMax(T key, tfloat val) {
            // if heap full
            if (this.Count == this.m_capacity) {
                // if Heads priority is smaller than input priority, then ignore that item
                if (this.HeadValue > val) {
                    this.values[1] = val; // remove top element
                    this.keys[1] = key;
                    this.BubbleDownMax(1); // bubble it down
                }
            } else {
                this.Count++;
                this.values[this.Count] = val;
                this.keys[this.Count] = key;
                this.BubbleUpMax(this.Count);
            }
        }

        public void PushObjMin(T key, tfloat val) {
            // if heap full
            if (this.Count == this.m_capacity) {
                // if Heads priority is smaller than input priority, then ignore that item
                if (this.HeadValue < val) {
                    this.values[1] = val; // remove top element
                    this.keys[1] = key;
                    this.BubbleDownMin(1); // bubble it down
                }
            } else {
                this.Count++;
                this.values[this.Count] = val;
                this.keys[this.Count] = key;
                this.BubbleUpMin(this.Count);
            }
        }

        private T PopHeadObj() {
            var result = this.HeadKey;

            this.values[1] = this.values[this.Count];
            this.keys[1] = this.keys[this.Count];
            this.Count--;

            return result;
        }

        public T PopObjMax() {
            var result = this.PopHeadObj();
            this.BubbleDownMax(1);
            return result;
        }

        public T PopObjMin() {
            var result = this.PopHeadObj();
            this.BubbleDownMin(1);
            return result;
        }

    }

}