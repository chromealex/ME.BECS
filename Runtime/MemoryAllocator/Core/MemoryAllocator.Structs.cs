namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using System.Runtime.InteropServices;
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Defines the supported clear options values.
    /// </summary>
    public enum ClearOptions {

        /// <summary>
        /// Clear memory option for <c>ClearOptions</c>.
        /// </summary>
        ClearMemory,
        /// <summary>
        /// Uninitialized memory option for <c>ClearOptions</c>.
        /// </summary>
        UninitializedMemory,

    }

    /// <summary>
    /// Caches the native storage size of <c>byte</c>.
    /// </summary>
    public struct TSize_byte {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(byte); }
    /// <summary>
    /// Caches the native storage size of <c>sbyte</c>.
    /// </summary>
    public struct TSize_sbyte {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(sbyte); }
    /// <summary>
    /// Caches the native storage size of <c>ushort</c>.
    /// </summary>
    public struct TSize_ushort {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(ushort); }
    /// <summary>
    /// Caches the native storage size of <c>short</c>.
    /// </summary>
    public struct TSize_short {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(short); }
    /// <summary>
    /// Caches the native storage size of <c>int</c>.
    /// </summary>
    public struct TSize_int {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(int); }
    /// <summary>
    /// Caches the native storage size of <c>uint</c>.
    /// </summary>
    public struct TSize_uint {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(uint); }
    /// <summary>
    /// Caches the native storage size of <c>long</c>.
    /// </summary>
    public struct TSize_long {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(long); }
    /// <summary>
    /// Caches the native storage size of <c>ulong</c>.
    /// </summary>
    public struct TSize_ulong {
        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public const uint size = sizeof(ulong); }

    /// <summary>
    /// Caches the native storage size of a type.
    /// </summary>
    public struct TSize<T> where T : struct {

        /// <summary>
        /// Native storage size in bytes.
        /// </summary>
        public static readonly uint size = (uint)_sizeOf<T>();
        /// <summary>
        /// Size int used by <c>TSize</c>.
        /// </summary>
        public static readonly int sizeInt = _sizeOf<T>();
        
    }

    /// <summary>
    /// Caches the native alignment of a type.
    /// </summary>
    public struct TAlign<T> where T : struct {

        /// <summary>
        /// Align used by <c>TAlign</c>.
        /// </summary>
        public static readonly uint align = (uint)_alignOf<T>();
        /// <summary>
        /// Align int used by <c>TAlign</c>.
        /// </summary>
        public static readonly int alignInt = _alignOf<T>();

    }

    /// <summary>
    /// Addresses a block by allocator zone and byte offset; it is not an independently owned allocation.
    /// </summary>
    [IgnoreProfiler]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MemPtr : System.IEquatable<MemPtr> {

        /// <summary>
        /// Storage size or fixed element count used by this representation.
        /// </summary>
        public const int SIZE = 8;

        /// <summary>
        /// Sentinel value representing an invalid entry.
        /// </summary>
        public static readonly MemPtr Invalid = new MemPtr(0u, 0u);
        
        /// <summary>
        /// Zone id used to locate the associated entry.
        /// </summary>
        public readonly uint zoneId;
        /// <summary>
        /// Offset into the associated storage or coordinate space.
        /// </summary>
        public readonly uint offset;

        /// <summary>
        /// Initializes <c>MemPtr</c> from the supplied zone ID, offset.
        /// </summary>
        [INLINE(256)]
        public MemPtr(uint zoneId, uint offset) {
            this.zoneId = zoneId;
            this.offset = offset;
        }

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid() => this.offset > 0u;

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        [INLINE(256)]
        public static bool operator ==(in MemPtr m1, in MemPtr m2) {
            return m1.zoneId == m2.zoneId && m1.offset == m2.offset;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        [INLINE(256)]
        public static bool operator !=(in MemPtr m1, in MemPtr m2) {
            return !(m1 == m2);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public bool Equals(MemPtr other) {
            return this.zoneId == other.zoneId && this.offset == other.offset;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is MemPtr other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        [INLINE(256)]
        public override int GetHashCode() {
            return System.HashCode.Combine(this.zoneId, this.offset);
        }

        /// <summary>
        /// Returns the value encoded as a signed 64-bit integer.
        /// </summary>
        [INLINE(256)]
        public long AsLong() {
            var index = (long)this.zoneId << 32;
            var offset = (long)this.offset;
            return index | offset;
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() => $"zoneId: {this.zoneId}, offset: {this.offset}";

        /// <summary>
        /// Returns size in bytes.
        /// </summary>
        public unsafe uint GetSizeInBytes(safe_ptr<State> state) {
            if (this.IsValid() == false) return TSize<MemPtr>.size;
            return state.ptr->allocator.GetSize(in this);
        }

    }
    
    /// <summary>
    /// Associates a memory address with the allocator needed to resolve it.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct MemAllocatorPtr {

        internal MemPtr ptr;

        /// <summary>
        /// Returns the value encoded as a signed 64-bit integer.
        /// </summary>
        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        /// <summary>
        /// Exposes the value through the requested typed view.
        /// </summary>
        [INLINE(256)]
        public readonly ref T As<T>(in MemoryAllocator allocator) where T : unmanaged {

            return ref allocator.Ref<T>(this.ptr);

        }

        /// <summary>
        /// Returns a native pointer view of this value.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr<T> AsPtr<T>(in MemoryAllocator allocator, uint offset = 0u) where T : unmanaged {

            return allocator.GetUnsafePtr(this.ptr, offset);

        }

        /// <summary>
        /// Stores the supplied value in mem allocator ptr.
        /// </summary>
        [INLINE(256)]
        public void Set<T>(ref MemoryAllocator allocator, in T data) where T : unmanaged {

            this.ptr = allocator.Alloc<T>();
            allocator.Ref<T>(this.ptr) = data;

        }

        /// <summary>
        /// Stores the supplied value in mem allocator ptr.
        /// </summary>
        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, safe_ptr data, uint dataSize) {

            this.ptr = allocator.Alloc(dataSize, out var ptr);
            if (data.ptr != null) {
                _memcpy(data, ptr, dataSize);
            } else {
                _memclear(ptr, dataSize);
            }

        }

        /// <summary>
        /// Releases the resources owned by this mem allocator ptr instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            allocator.Free(this.ptr);
            this = default;

        }

    }

    /// <summary>
    /// Associates a memory address with the allocator needed to resolve it.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct MemAllocatorPtr<T> where T : unmanaged {

        internal MemPtr ptr;

        /// <summary>
        /// Returns the value encoded as a signed 64-bit integer.
        /// </summary>
        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        /// <summary>
        /// Exposes the value through the requested typed view.
        /// </summary>
        [INLINE(256)]
        public readonly ref T As(in MemoryAllocator allocator) {

            return ref allocator.Ref<T>(this.ptr);

        }

        /// <summary>
        /// Returns a native pointer view of this value.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr<T> AsPtr(in MemoryAllocator allocator, uint offset = 0u) {

            return allocator.GetUnsafePtr(this.ptr, offset);

        }

        /// <summary>
        /// Stores the supplied value in mem allocator ptr.
        /// </summary>
        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, in T data) {

            this.ptr = allocator.Alloc<T>();
            allocator.Ref<T>(this.ptr) = data;

        }

        /// <summary>
        /// Stores the supplied value in mem allocator ptr.
        /// </summary>
        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, safe_ptr data, uint dataSize) {

            this.ptr = allocator.Alloc(dataSize, out var ptr);
            if (data.ptr != null) {
                _memcpy(data, ptr, dataSize);
            } else {
                _memclear(ptr, dataSize);
            }

        }

        /// <summary>
        /// Releases the resources owned by this mem allocator ptr instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            allocator.Free(this.ptr);
            this = default;

        }

    }

}
