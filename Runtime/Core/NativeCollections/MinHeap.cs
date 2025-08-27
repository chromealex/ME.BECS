
using Unity.Jobs;
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

    public unsafe struct NativeMinHeap<T> : IDisposable where T : unmanaged, IMinHeapNode {

        private safe_ptr<T> mBuffer;
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

        [INLINE(256)]
        private static void Allocate(uint capacity, Allocator allocator, out NativeMinHeap<T> nativeMinHeap) {
            var size = (uint)TSize<T>.size * capacity;
            if (allocator <= Allocator.None) {
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            }

            if (size > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(capacity),
                                                      $"Length * sizeof(T) cannot exceed {(object)int.MaxValue} bytes");
            }

            nativeMinHeap.mBuffer = _make(size, TAlign<T>.alignInt, allocator);
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
        public void Push(T node) {

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

                this.Set(currentPtr, in current);
            }

            this.Set(this.mLength, in node);
            ++this.mLength;
        }

        [INLINE(256)]
        public void Set(int index, in T data) {
            if (index >= this.mCapacity) {
                this.mCapacity *= 2u;
                var size = TSize<T>.size * this.mCapacity;
                var newPtr = _make(size, TAlign<T>.alignInt, this.mAllocatorLabel);
                _memmove(this.mBuffer, newPtr, TSize<T>.size * this.mLength);
                this.mBuffer = newPtr;
            }
            this.mBuffer[index] = data;
        }

        [INLINE(256)]
        public int Pop() {
            var result = this.mHead;
            this.mHead = this[this.mHead].Next;
            return result;
        }

        [INLINE(256)]
        public bool TryPop(out T node) {
            if (this.mHead == -1) {
                node = default;
                return false;
            }
            var idx = this.Pop();
            node = this[idx];
            return true;
        }

        public T this[int index] => this.mBuffer[index];

        [INLINE(256)]
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

        public JobHandle Dispose(JobHandle dependsOn) {
            dependsOn = new DisposeWithAllocatorPtrJob() {
                ptr = this.mBuffer,
                allocator = this.mAllocatorLabel,
            }.Schedule(dependsOn);
            this.mBuffer = default;
            this.mCapacity = 0;
            return dependsOn;
        }

    }

    public interface IMinHeapNode {

        public tfloat ExpectedCost { get; }
        public int Next { get; set; }

    }
    
    public struct MinHeapNode : IMinHeapNode {

        [INLINE(256)]
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