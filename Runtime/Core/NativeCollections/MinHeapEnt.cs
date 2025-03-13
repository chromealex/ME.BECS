#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.NativeCollections {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public struct NativeMinHeapEnt : IDisposable {

        public uint Count => (uint)this.mLength;

        private safe_ptr<MinHeapNodeEnt> mBuffer;
        private uint mCapacity;
        private Allocator mAllocatorLabel;

        private int mHead;
        private int mLength;
        //private int mMinIndex;
        //private int mMaxIndex;

        [INLINE(256)]
        public NativeMinHeapEnt(uint capacity, Allocator allocator /*, NativeArrayOptions options = NativeArrayOptions.ClearMemory*/) {
            Allocate(capacity, allocator, out this);
            /*if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return;
            UnsafeUtility.MemClear(m_Buffer, (long) m_capacity * UnsafeUtility.SizeOf<MinHeapNode>());*/
        }

        [INLINE(256)]
        private static void Allocate(uint capacity, Allocator allocator, out NativeMinHeapEnt nativeMinHeap) {
            var size = TSize<MinHeapNodeEnt>.size * capacity;
            if (allocator <= Allocator.None) {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }

            if (size > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(capacity),
                                                      $"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");
            }

            nativeMinHeap.mBuffer = _make(size, TAlign<MinHeapNodeEnt>.alignInt, allocator);
            nativeMinHeap.mCapacity = capacity;
            nativeMinHeap.mAllocatorLabel = allocator;
            //nativeMinHeap.mMinIndex = 0;
            //nativeMinHeap.mMaxIndex = capacity - 1;
            nativeMinHeap.mHead = -1;
            nativeMinHeap.mLength = 0;

        }

        [INLINE(256)]
        public bool HasNext() {
            return this.mHead >= 0;
        }

        [INLINE(256)]
        public void EnsureCapacity(uint capacity) {

            var free = this.mCapacity - (uint)this.mLength;
            if (free < capacity) {
                _resizeArray(this.mAllocatorLabel, ref this.mBuffer, ref this.mCapacity, capacity + (uint)this.mLength);
            }

        }

        [INLINE(256)]
        public void Push(MinHeapNodeEnt node) {

            if (this.mHead < 0) {
                this.mHead = this.mLength;
            } else if (node.expectedCost < this[this.mHead].expectedCost) {
                node.next = this.mHead;
                this.mHead = this.mLength;
            } else {
                var currentPtr = this.mHead;
                var current = this[currentPtr];

                while (current.next >= 0 && this[current.next].expectedCost <= node.expectedCost) {
                    currentPtr = current.next;
                    current = this[current.next];
                }

                node.next = current.next;
                current.next = this.mLength;

                this.mBuffer[currentPtr] = current;
            }

            this.mBuffer[this.mLength] = node;
            ++this.mLength;
        }

        [INLINE(256)]
        public int Pop() {
            var result = this.mHead;
            this.mHead = this[this.mHead].next;
            return result;
        }

        public MinHeapNodeEnt this[int index] => this.mBuffer[index];

        [INLINE(256)]
        public void Clear() {
            this.mHead = -1;
            this.mLength = 0;
        }

        [INLINE(256)]
        public void Dispose() {
            if (!UnsafeUtility.IsValidAllocator(this.mAllocatorLabel)) {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            _free(this.mBuffer, this.mAllocatorLabel);
            this.mBuffer = default;
            this.mCapacity = 0;
        }

    }

    public struct MinHeapNodeEnt {

        [INLINE(256)]
        public MinHeapNodeEnt(Ent data, tfloat expectedCost) {
            this.data = data;
            this.expectedCost = expectedCost;
            this.next = -1;
        }

        public readonly Ent data;
        public readonly tfloat expectedCost;
        public int next;

    }

}