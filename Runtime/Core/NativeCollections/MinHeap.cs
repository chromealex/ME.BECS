#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.NativeCollections {

    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    public unsafe struct NativeMinHeap : IDisposable {

        private safe_ptr<MinHeapNode> mBuffer;
        private uint mCapacity;
        private Allocator mAllocatorLabel;

        private int mHead;
        private int mLength;
        //private int mMinIndex;
        //private int mMaxIndex;

        public NativeMinHeap(uint capacity, Allocator allocator /*, NativeArrayOptions options = NativeArrayOptions.ClearMemory*/) {
            Allocate(capacity, allocator, out this);
            /*if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return;
            UnsafeUtility.MemClear(m_Buffer, (long) m_capacity * UnsafeUtility.SizeOf<MinHeapNode>());*/
        }

        private static void Allocate(uint capacity, Allocator allocator, out NativeMinHeap nativeMinHeap) {
            var size = (uint)TSize<MinHeapNode>.size * capacity;
            if (allocator <= Allocator.None) {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }

            if (size > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(capacity),
                                                      $"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");
            }

            nativeMinHeap.mBuffer = _make(size, TAlign<MinHeapNode>.alignInt, allocator);
            nativeMinHeap.mCapacity = capacity;
            nativeMinHeap.mAllocatorLabel = allocator;
            //nativeMinHeap.mMinIndex = 0;
            //nativeMinHeap.mMaxIndex = capacity - 1;
            nativeMinHeap.mHead = -1;
            nativeMinHeap.mLength = 0;

        }

        public bool HasNext() {
            return this.mHead >= 0;
        }

        public void Push(MinHeapNode node) {

            if (this.mHead < 0) {
                this.mHead = this.mLength;
            } else if (node.ExpectedCost < this[this.mHead].ExpectedCost) {
                node.Next = this.mHead;
                this.mHead = this.mLength;
            } else {
                var currentPtr = this.mHead;
                var current = this[currentPtr];

                while (current.Next >= 0 && this[current.Next].ExpectedCost <= node.ExpectedCost) {
                    currentPtr = current.Next;
                    current = this[current.Next];
                }

                node.Next = current.Next;
                current.Next = this.mLength;

                this.mBuffer[currentPtr] = current;
            }

            this.mBuffer[this.mLength] = node;
            ++this.mLength;
        }

        public int Pop() {
            var result = this.mHead;
            this.mHead = this[this.mHead].Next;
            return result;
        }

        public MinHeapNode this[int index] => this.mBuffer[index];

        public void Clear() {
            this.mHead = -1;
            this.mLength = 0;
        }

        public void Dispose() {
            if (!UnsafeUtility.IsValidAllocator(this.mAllocatorLabel)) {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            _free(this.mBuffer, this.mAllocatorLabel);
            this.mBuffer = default;
            this.mCapacity = 0;
        }

    }

    public struct MinHeapNode {

        public MinHeapNode(uint position, tfloat expectedCost) {
            this.Position = position;
            this.ExpectedCost = expectedCost;
            this.Next = -1;
        }

        public uint Position { get; }
        public tfloat ExpectedCost { get; }
        public int Next { get; set; }

    }

}