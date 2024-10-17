namespace ME.BECS.NativeCollections {

    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public unsafe struct NativeMinHeapEnt : IDisposable {

        public uint Count => (uint)this.mLength;

        [NativeDisableUnsafePtrRestriction]
        private void* mBuffer;
        private int mCapacity;
        private Allocator mAllocatorLabel;

        private int mHead;
        private int mLength;
        //private int mMinIndex;
        //private int mMaxIndex;

        public NativeMinHeapEnt(int capacity, Allocator allocator /*, NativeArrayOptions options = NativeArrayOptions.ClearMemory*/) {
            Allocate(capacity, allocator, out this);
            /*if ((options & NativeArrayOptions.ClearMemory) != NativeArrayOptions.ClearMemory)
                return;
            UnsafeUtility.MemClear(m_Buffer, (long) m_capacity * UnsafeUtility.SizeOf<MinHeapNode>());*/
        }

        private static void Allocate(int capacity, Allocator allocator, out NativeMinHeapEnt nativeMinHeap) {
            var size = (long)UnsafeUtility.SizeOf<MinHeapNodeEnt>() * capacity;
            if (allocator <= Allocator.None) {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }

            if (capacity < 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Length must be >= 0");
            }

            if (size > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(capacity),
                                                      $"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");
            }

            nativeMinHeap.mBuffer = UnsafeUtility.Malloc(size, UnsafeUtility.AlignOf<MinHeapNodeEnt>(), allocator);
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

        public void Push(MinHeapNodeEnt node) {

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

                UnsafeUtility.WriteArrayElement(this.mBuffer, currentPtr, current);
            }

            UnsafeUtility.WriteArrayElement(this.mBuffer, this.mLength, node);
            this.mLength += 1;
        }

        public int Pop() {
            var result = this.mHead;
            this.mHead = this[this.mHead].Next;
            return result;
        }

        public MinHeapNodeEnt this[int index] => UnsafeUtility.ReadArrayElement<MinHeapNodeEnt>(this.mBuffer, index);

        public void Clear() {
            this.mHead = -1;
            this.mLength = 0;
        }

        public void Dispose() {
            if (!UnsafeUtility.IsValidAllocator(this.mAllocatorLabel)) {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            UnsafeUtility.Free(this.mBuffer, this.mAllocatorLabel);
            this.mBuffer = null;
            this.mCapacity = 0;
        }

    }

    public struct MinHeapNodeEnt {

        public MinHeapNodeEnt(Ent position, float expectedCost) {
            this.Position = position;
            this.ExpectedCost = expectedCost;
            this.Next = -1;
        }

        public Ent Position { get; }
        public float ExpectedCost { get; }
        public int Next { get; set; }

    }

}