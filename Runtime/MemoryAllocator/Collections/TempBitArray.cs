namespace ME.BECS {

    using static CutsPool;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;

    public class FullFillBits {

        public const uint FILL_BITS_COUNT = 2048u;
        
        public static readonly Unity.Burst.SharedStatic<Internal.Array<uint>> fullFillBits = Unity.Burst.SharedStatic<Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<FullFillBits>(TAlign<Internal.Array<uint>>.align, 101L);

        public static void Initialize() {
            if (fullFillBits.Data.IsCreated == true) fullFillBits.Data.Dispose();
            fullFillBits.Data.Resize(FILL_BITS_COUNT);
            for (uint i = 0; i < FILL_BITS_COUNT; ++i) {
                fullFillBits.Data.Get(i) = i;
            }
        }

        public static void Dispose() {
            if (fullFillBits.Data.IsCreated == true) fullFillBits.Data.Dispose();
        }

    }

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(TempBitArrayDebugView))]
    public unsafe struct TempBitArray : IIsCreated {

        internal const int BITS_IN_ULONG = sizeof(ulong) * 8;
        internal const int BITS_IN_ULONG_MASK = BITS_IN_ULONG - 1;

        public readonly safe_ptr<ulong> ptr;
        public uint Length;
        internal readonly Unity.Collections.Allocator allocator;

        public bool IsCreated => this.ptr.ptr != null;

        [INLINE(256)]
        public TempBitArray(uint length, ClearOptions clearOptions = ClearOptions.ClearMemory, Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMPJOB) {

            var sizeInBytes = Bitwise.AlignULongBits(length);
            this.allocator = allocator;
            this.ptr = _make(sizeInBytes, TAlign<ulong>.alignInt, this.allocator);
            this.Length = length;

            if (clearOptions == ClearOptions.ClearMemory) {
                _memclear(this.ptr, sizeInBytes);
            }
        }

        [INLINE(256)]
        public TempBitArray(uint length, ClearOptions clearOptions, Unity.Collections.AllocatorManager.AllocatorHandle allocator) {

            var sizeInBytes = Bitwise.AlignULongBits(length);
            this.allocator = allocator.ToAllocator;
            this.ptr = _make((int)sizeInBytes, TAlign<ulong>.alignInt, this.allocator);
            this.Length = length;

            if (clearOptions == ClearOptions.ClearMemory) {
                _memclear(this.ptr, sizeInBytes);
            }
        }

        [INLINE(256)]
        public TempBitArray(in MemoryAllocator allocator, in BitArray bitmap, Unity.Collections.Allocator unityAllocator) {

            var newArr = new TempBitArray(bitmap.Length, ClearOptions.UninitializedMemory, unityAllocator);
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            _memcpy(ptr, newArr.ptr, Bitwise.AlignULongBits(bitmap.Length));
            this = newArr;
            
        }

        [INLINE(256)]
        public TempBitArray(in MemoryAllocator allocator, in BitArray bitmap, Unity.Collections.AllocatorManager.AllocatorHandle unityAllocator) {

            var newArr = new TempBitArray(bitmap.Length, ClearOptions.UninitializedMemory, unityAllocator);
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            _memcpy(ptr, newArr.ptr, Bitwise.AlignULongBits(bitmap.Length));
            this = newArr;
            
        }

        [INLINE(256)]
        public void Resize(uint newLength, Unity.Collections.Allocator allocator) {
            E.IS_CREATED(this);

            if (newLength > this.Length) {
                var newArr = new TempBitArray(newLength, ClearOptions.ClearMemory, allocator);
                _memcpy(this.ptr, newArr.ptr, Bitwise.AlignULongBits(this.Length));
                _free(this.ptr, this.allocator);
                this = newArr;
            }
            
        }

        /// <summary>
        /// Sets all the bits in the bitmap to the specified value.
        /// </summary>
        /// <param name="value">The value to set each bit to.</param>
        /// <returns>The instance of the modified bitmap.</returns>
        [INLINE(256)]
        public void SetAllBits(bool value) {
            E.IS_CREATED(this);
            var len = Bitwise.GetLength(this.Length);
            var setValue = value ? ulong.MaxValue : ulong.MinValue;
            for (var index = 0; index < len; index++) {
                this.ptr[index] = setValue;
            }
        }

        /// <summary>
        /// Gets the value of the bit at the specified index.
        /// </summary>
        /// <param name="index">The index of the bit.</param>
        /// <returns>The value of the bit at the specified index.</returns>
        [INLINE(256)]
        public readonly bool IsSet(int index) {
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Length);
            return (this.ptr[index / TempBitArray.BITS_IN_ULONG] & (1UL << (index & TempBitArray.BITS_IN_ULONG_MASK))) > 0;
        }

        /// <summary>
        /// Sets the value of the bit at the specified index to the specified value.
        /// </summary>
        /// <param name="index">The index of the bit to set.</param>
        /// <param name="value">The value to set the bit to.</param>
        /// <returns>The instance of the modified bitmap.</returns>
        [INLINE(256)]
        public void Set(int index, bool value) {
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Length);
            if (value == true) {
                this.ptr[index / TempBitArray.BITS_IN_ULONG] |= 1UL << (index & TempBitArray.BITS_IN_ULONG_MASK);
            } else {
                this.ptr[index / TempBitArray.BITS_IN_ULONG] &= ~(1UL << (index & TempBitArray.BITS_IN_ULONG_MASK));
            }
        }

        /// <summary>
        /// Takes the union of this bitmap and the specified bitmap and stores the result in this
        /// instance.
        /// </summary>
        /// <param name="bitmap">The bitmap to union with this instance.</param>
        /// <returns>A reference to this instance.</returns>
        [INLINE(256)]
        public void Union(in TempBitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) return;
            this.Resize(bitmap.Length > this.Length ? bitmap.Length : this.Length, this.allocator);
            E.RANGE(bitmap.Length - 1u, 0u, this.Length);
            var len = Bitwise.GetMinLength(bitmap.Length, this.Length);
            for (var index = 0; index < len; ++index) {
                this.ptr[index] |= bitmap.ptr[index];
            }
        }

        [INLINE(256)]
        public void Union(in MemoryAllocator allocator, in BitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) return;
            this.Resize(bitmap.Length > this.Length ? bitmap.Length : this.Length, this.allocator);
            E.RANGE(bitmap.Length - 1u, 0u, this.Length);
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            var len = Bitwise.GetMinLength(bitmap.Length, this.Length);
            for (var index = 0; index < len; ++index) {
                this.ptr[index] |= ptr[index];
            }
        }

        /// <summary>
        /// Takes the intersection of this bitmap and the specified bitmap and stores the result in
        /// this instance.
        /// </summary>
        /// <param name="bitmap">The bitmap to intersect with this instance.</param>
        /// <returns>A reference to this instance.</returns>
        [INLINE(256)]
        public void Intersect(in TempBitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) {
                this.SetAllBits(false);
                return;
            }
            E.RANGE(bitmap.Length - 1u, 0u, this.Length);
            var ptr = bitmap.ptr;
            var len = Bitwise.GetLength(this.Length);
            var bLen = Bitwise.GetLength(bitmap.Length);
            for (var index = 0; index < len; ++index) {
                var v = 0UL;
                if (index < bLen) v = ptr[index];
                this.ptr[index] &= v;
            }
        }

        [INLINE(256)]
        public void Intersect(in TempBitArray bitmap, uint maxLength) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) {
                this.SetAllBits(false);
                return;
            }
            var targetLength = math.min(maxLength, bitmap.Length);
            E.RANGE(targetLength - 1u, 0u, this.Length);
            var ptr = bitmap.ptr;
            var len = Bitwise.GetLength(this.Length);
            var bLen = Bitwise.GetLength(targetLength);
            for (var index = 0; index < len; ++index) {
                var v = 0UL;
                if (index < bLen) v = ptr[index];
                this.ptr[index] &= v;
            }
        }

        [INLINE(256)]
        public void Intersect(in MemoryAllocator allocator, in BitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) {
                this.SetAllBits(false);
                return;
            }
            E.RANGE(bitmap.Length - 1u, 0u, this.Length);
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            var len = Bitwise.GetLength(this.Length);
            var bLen = Bitwise.GetLength(bitmap.Length);
            for (var index = 0; index < len; ++index) {
                var v = 0UL;
                if (index < bLen) v = ptr[index];
                this.ptr[index] &= v;
            }
        }

        [INLINE(256)]
        public void Intersect(in MemoryAllocator allocator, in BitArray bitmap, uint maxLength) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0u) {
                this.SetAllBits(false);
                return;
            }

            var targetLength = math.min(maxLength, bitmap.Length);
            E.RANGE(targetLength - 1u, 0u, this.Length);
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            var len = Bitwise.GetLength(this.Length);
            var bLen = Bitwise.GetLength(targetLength);
            for (var index = 0; index < len; ++index) {
                var v = 0UL;
                if (index < bLen) v = ptr[index];
                this.ptr[index] &= v;
            }
        }

        [INLINE(256)]
        public void Remove(in TempBitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0) return;
            this.Resize(bitmap.Length > this.Length ? bitmap.Length : this.Length, this.allocator);
            E.RANGE(bitmap.Length - 1u, 0u, this.Length);
            var len = Bitwise.GetMinLength(bitmap.Length, this.Length);
            for (var index = 0; index < len; ++index) {
                this.ptr[index] &= ~bitmap.ptr[index];
            }
        }

        [INLINE(256)]
        public void Remove(in MemoryAllocator allocator, in BitArray bitmap) {
            E.IS_CREATED(this);
            if (bitmap.Length == 0) return;
            var ptr = (safe_ptr<ulong>)allocator.GetUnsafePtr(bitmap.ptr);
            var len = Bitwise.GetMinLength(bitmap.Length, this.Length);
            for (var index = 0; index < len; ++index) {
                this.ptr[index] &= ~ptr[index];
            }
        }

        /// <summary>
        /// Inverts all the bits in this bitmap.
        /// </summary>
        /// <returns>A reference to this instance.</returns>
        [INLINE(256)]
        public void Invert() {
            E.IS_CREATED(this);
            var len = Bitwise.GetLength(this.Length);
            for (var index = 0; index < len; ++index) {
                this.ptr[index] = ~this.ptr[index];
            }
        }

        /// <summary>
        /// Sets a range of bits to the specified value.
        /// </summary>
        /// <param name="start">The index of the bit at the start of the range (inclusive).</param>
        /// <param name="end">The index of the bit at the end of the range (inclusive).</param>
        /// <param name="value">The value to set the bits to.</param>
        /// <returns>A reference to this instance.</returns>
        [INLINE(256)]
        public void SetRange(int start, int end, bool value) {
            E.IS_CREATED(this);
            if (start == end) {
                this.Set(start, value);
                return;
            }

            var startBucket = start / TempBitArray.BITS_IN_ULONG;
            var startOffset = start & TempBitArray.BITS_IN_ULONG_MASK;
            var endBucket = end / TempBitArray.BITS_IN_ULONG;
            var endOffset = end & TempBitArray.BITS_IN_ULONG_MASK;

            if (value) {
                this.ptr[startBucket] |= ulong.MaxValue << startOffset;
            } else {
                this.ptr[startBucket] &= ~(ulong.MaxValue << startOffset);
            }

            for (var bucketIndex = startBucket + 1; bucketIndex < endBucket; bucketIndex++) {
                this.ptr[bucketIndex] = value ? ulong.MaxValue : ulong.MinValue;
            }

            if (value) {
                this.ptr[endBucket] |= ulong.MaxValue >> (TempBitArray.BITS_IN_ULONG - endOffset - 1);
            } else {
                this.ptr[endBucket] &= ~(ulong.MaxValue >> (TempBitArray.BITS_IN_ULONG - endOffset - 1));
            }
        }

        [INLINE(256)]
        public void Clear() {

            E.IS_CREATED(this);
            this.SetAllBits(false);

        }

        [INLINE(256)]
        public void Dispose() {

            E.IS_CREATED(this);
            _free(this.ptr, this.allocator);
            this = default;

        }

        [INLINE(256)]
        public readonly void DisposeReadonly() {

            _free(this.ptr, this.allocator);
            
        }

        [INLINE(256)]
        public UnsafeList<uint> GetTrueBitsTemp() {
            return ME.BECS.Collections.BitScanner.GetTrueBitsTempFast(in this);
        }

        public uint GetReservedSizeInBytes() {
            return Bitwise.AlignULongBits(this.Length);
        }

    }

    internal sealed class TempBitArrayDebugView {

        private TempBitArray data;

        public TempBitArrayDebugView(TempBitArray data) {
            this.data = data;
        }

        public bool[] Bits {
            get {
                var array = new bool[this.data.Length];
                for (var i = 0; i < this.data.Length; ++i) {
                    array[i] = this.data.IsSet(i);
                }

                return array;
            }
        }

        public int[] BitIndexes {
            get {
                var array = new System.Collections.Generic.List<int>((int)this.data.Length);
                for (var i = 0; i < this.data.Length; ++i) {
                    if (this.data.IsSet(i) == true) array.Add(i);
                }

                return array.ToArray();
            }
        }

    }

}