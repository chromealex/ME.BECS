
using Unity.Jobs;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.NativeCollections {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;

    /// <summary>
    /// Stores native entries ordered by minimum priority.
    /// </summary>
    public unsafe struct NativeMinHeap<T> : IDisposable where T : unmanaged, IMinHeapNode {

        private safe_ptr<T> mBuffer;
        private uint mCapacity;
        private Allocator mAllocatorLabel;

        private int mHead;
        private int mBufferLength;
        //private int mMinIndex;
        //private int mMaxIndex;

        /// <summary>
        /// Initializes <c>NativeMinHeap</c> from the supplied capacity, allocator.
        /// </summary>
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
            nativeMinHeap.mBufferLength = 0;

        }

        /// <summary>
        /// Tests whether the context has next.
        /// </summary>
        [INLINE(256)]
        public bool HasNext() {
            return this.mHead >= 0;
        }

        /// <summary>
        /// Adds an entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public void Push(T node) {

            if (this.mHead < 0) {
                this.mHead = this.mBufferLength;
                node.Next = -1;
            } else if (node.ExpectedCost < this[this.mHead].ExpectedCost) {
                node.Next = this.mHead;
                this.mHead = this.mBufferLength;
            } else {
                var currentPtr = this.mHead;
                var current = this[currentPtr];

                while (current.Next >= 0 && this[current.Next].ExpectedCost <= node.ExpectedCost) {
                    currentPtr = current.Next;
                    current = this[current.Next];
                }

                node.Next = current.Next;
                current.Next = this.mBufferLength;

                this.mBuffer[currentPtr] = current;
            }

            this.Set(this.mBufferLength, in node);
            ++this.mBufferLength;
        }

        /// <summary>
        /// Stores the supplied value in native min heap.
        /// </summary>
        [INLINE(256)]
        public void Set(int index, in T data) {
            if (index >= this.mCapacity) {
                _resizeArray(this.mAllocatorLabel, ref this.mBuffer, ref this.mCapacity, this.mCapacity * 2u);
            }
            this.mBuffer[index] = data;
        }

        /// <summary>
        /// Removes and returns the next entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public int Pop() {
            var result = this.mHead;
            this.mHead = this[this.mHead].Next;
            return result;
        }

        /// <summary>
        /// Attempts to pop and reports whether the operation succeeded.
        /// </summary>
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

        /// <summary>
        /// Provides indexed access to the requested entry.
        /// </summary>
        public T this[int index] => this.mBuffer[index];

        /// <summary>
        /// Clears the current native min heap contents.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            this.mHead = -1;
            this.mBufferLength = 0;
        }

        /// <summary>
        /// Releases the resources owned by this native min heap instance.
        /// </summary>
        public void Dispose() {
            if (!UnsafeUtility.IsValidAllocator(this.mAllocatorLabel)) {
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            }

            _free(this.mBuffer, this.mAllocatorLabel);
            this.mBuffer = default;
            this.mCapacity = 0;
        }

        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
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

    /// <summary>
    /// Defines the operations required by min heap node.
    /// </summary>
    public interface IMinHeapNode {

        /// <summary>
        /// Expected cost used by <c>IMinHeapNode</c>.
        /// </summary>
        public tfloat ExpectedCost { get; }
        /// <summary>
        /// Next entry in the represented sequence.
        /// </summary>
        public int Next { get; set; }

    }
    
    /// <summary>
    /// Defines a min heap node entry in the associated graph.
    /// </summary>
    public struct MinHeapNode : IMinHeapNode {

        /// <summary>
        /// Initializes <c>MinHeapNode</c> from the supplied position, expected cost.
        /// </summary>
        [INLINE(256)]
        public MinHeapNode(uint position, tfloat expectedCost) {
            this.Position = position;
            this.ExpectedCost = expectedCost;
            this.Next = -1;
        }

        /// <summary>
        /// Position in the coordinate space used by the containing API.
        /// </summary>
        public uint Position { get; }
        /// <summary>
        /// Expected cost used by <c>MinHeapNode</c>.
        /// </summary>
        public tfloat ExpectedCost { get; }
        /// <summary>
        /// Next entry in the represented sequence.
        /// </summary>
        public int Next { get; set; }

    }

}