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
    /// Stores entity entries ordered by minimum priority.
    /// </summary>
    public struct NativeMinHeapEnt : IDisposable {

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => (uint)this.mHeapLength;

        private safe_ptr<MinHeapNodeEnt> mBuffer;
        private uint mCapacity;
        private Allocator mAllocatorLabel;

        private int mHeapLength;
        //private int mMinIndex;
        //private int mMaxIndex;

        /// <summary>
        /// Initializes <c>NativeMinHeapEnt</c> from the supplied capacity, allocator.
        /// </summary>
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
            nativeMinHeap.mHeapLength = 0;

        }

        /// <summary>
        /// Tests whether the context has next.
        /// </summary>
        [INLINE(256)]
        public bool HasNext() {
            return this.mHeapLength > 0;
        }

        /// <summary>
        /// Grows backing storage when needed to satisfy the requested capacity.
        /// </summary>
        [INLINE(256)]
        public void EnsureCapacity(uint capacity) {

            var free = this.mCapacity - (uint)this.mHeapLength;
            if (free < capacity) {
                _resizeArray(this.mAllocatorLabel, ref this.mBuffer, ref this.mCapacity, capacity + (uint)this.mHeapLength);
            }

        }

        /// <summary>
        /// Adds an entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public void Push(MinHeapNodeEnt node) {
            if ((uint)this.mHeapLength == this.mCapacity) {
                _resizeArray(this.mAllocatorLabel, ref this.mBuffer, ref this.mCapacity, math.max(1u, this.mCapacity * 2u));
            }

            var index = this.mHeapLength;
            while (index > 0) {
                var parent = (index - 1) >> 1;
                var parentNode = this.mBuffer[parent];
                if (Less(in node, in parentNode) == false) break;
                this.mBuffer[index] = parentNode;
                index = parent;
            }

            this.mBuffer[index] = node;
            ++this.mHeapLength;
        }

        /// <summary>
        /// Removes and returns the next entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public int Pop() {
            var result = this.mBuffer[0];
            --this.mHeapLength;
            if (this.mHeapLength > 0) {
                var tail = this.mBuffer[this.mHeapLength];
                var index = 0;
                while (true) {
                    var left = index * 2 + 1;
                    if (left >= this.mHeapLength) break;
                    var right = left + 1;
                    var child = right < this.mHeapLength && Less(in this.mBuffer[right], in this.mBuffer[left]) == true ? right : left;
                    if (Less(in tail, in this.mBuffer[child]) == true) break;
                    this.mBuffer[index] = this.mBuffer[child];
                    index = child;
                }
                this.mBuffer[index] = tail;
            }

            this.mBuffer[this.mHeapLength] = result;
            return this.mHeapLength;
        }

        /// <summary>
        /// Provides indexed access to the requested entry.
        /// </summary>
        public MinHeapNodeEnt this[int index] => this.mBuffer[index];

        /// <summary>
        /// Clears the current native min heap ent contents.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            this.mHeapLength = 0;
        }

        [INLINE(256)]
        private static bool Less(in MinHeapNodeEnt a, in MinHeapNodeEnt b) {
            if (a.expectedCost < b.expectedCost) return true;
            if (a.expectedCost > b.expectedCost) return false;
            return a.data.CompareTo(b.data) < 0;
        }

        /// <summary>
        /// Releases the resources owned by this native min heap ent instance.
        /// </summary>
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

    /// <summary>
    /// Defines min heap node ent state and operations.
    /// </summary>
    public struct MinHeapNodeEnt {

        /// <summary>
        /// Initializes <c>MinHeapNodeEnt</c> from the supplied data, expected cost.
        /// </summary>
        [INLINE(256)]
        public MinHeapNodeEnt(Ent data, tfloat expectedCost) {
            this.data = data;
            this.expectedCost = expectedCost;
            this.next = -1;
        }

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public readonly Ent data;
        /// <summary>
        /// Expected cost used by <c>MinHeapNodeEnt</c>.
        /// </summary>
        public readonly tfloat expectedCost;
        /// <summary>
        /// Link or index of the next entry in the sequence.
        /// </summary>
        public int next;

    }

}
