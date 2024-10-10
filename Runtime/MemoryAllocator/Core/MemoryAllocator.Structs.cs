namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;
    using static Cuts;

    public enum ClearOptions {

        ClearMemory,
        UninitializedMemory,

    }

    public struct TSize<T> where T : struct {

        public static readonly uint size = (uint)_sizeOf<T>();
        public static readonly int sizeInt = _sizeOf<T>();

    }

    public struct TAlign<T> where T : struct {

        public static readonly uint align = (uint)_alignOf<T>();
        public static readonly int alignInt = _alignOf<T>();

    }

    [StructLayout(LayoutKind.Explicit, Size = MemPtr.SIZE)]
    public readonly struct MemPtr : System.IEquatable<MemPtr> {

        public const int SIZE = 8;

        public static readonly MemPtr Invalid = new MemPtr(0u, 0u);
        
        [FieldOffset(0)]
        public readonly uint zoneId;
        [FieldOffset(4)]
        public readonly uint offset;

        [INLINE(256)]
        public MemPtr(uint zoneId, uint offset) {
            this.zoneId = zoneId;
            this.offset = offset;
        }

        [INLINE(256)]
        public bool IsValid() => this.zoneId >= 0 && this.offset > 0;

        [INLINE(256)]
        public static bool operator ==(in MemPtr m1, in MemPtr m2) {
            return m1.zoneId == m2.zoneId && m1.offset == m2.offset;
        }

        [INLINE(256)]
        public static bool operator !=(in MemPtr m1, in MemPtr m2) {
            return !(m1 == m2);
        }

        [INLINE(256)]
        public bool Equals(MemPtr other) {
            return this.zoneId == other.zoneId && this.offset == other.offset;
        }

        [INLINE(256)]
        public override bool Equals(object obj) {
            return obj is MemPtr other && this.Equals(other);
        }

        [INLINE(256)]
        public override int GetHashCode() {
            return System.HashCode.Combine(this.zoneId, this.offset);
        }

        [INLINE(256)]
        public long AsLong() {
            var index = (long)this.zoneId << 32;
            var offset = (long)this.offset;
            return index | offset;
        }

        public override string ToString() => $"zoneId: {this.zoneId}, offset: {this.offset}";

        public unsafe uint GetSizeInBytes(State* state) {
            if (this.IsValid() == false) return TSize<MemPtr>.size;
            return state->allocator.GetSize(in this);
        }

    }
    
    public unsafe struct MemAllocatorPtr {

        internal MemPtr ptr;

        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        [INLINE(256)]
        public readonly ref T As<T>(in MemoryAllocator allocator) where T : unmanaged {

            return ref allocator.Ref<T>(this.ptr);

        }

        [INLINE(256)]
        public readonly T* AsPtr<T>(in MemoryAllocator allocator, uint offset = 0u) where T : unmanaged {

            return (T*)allocator.GetUnsafePtr(this.ptr, offset);

        }

        [INLINE(256)]
        public void Set<T>(ref MemoryAllocator allocator, in T data) where T : unmanaged {

            this.ptr = allocator.Alloc<T>();
            allocator.Ref<T>(this.ptr) = data;

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, void* data, uint dataSize) {

            this.ptr = allocator.Alloc(dataSize, out var ptr);
            if (data != null) {
                _memcpy(data, ptr, dataSize);
            } else {
                _memclear(ptr, dataSize);
            }

        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            allocator.Free(this.ptr);
            this = default;

        }

    }

    public unsafe struct MemAllocatorPtr<T> where T : unmanaged {

        internal MemPtr ptr;

        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        [INLINE(256)]
        public readonly ref T As(in MemoryAllocator allocator) {

            return ref allocator.Ref<T>(this.ptr);

        }

        [INLINE(256)]
        public readonly T* AsPtr(in MemoryAllocator allocator, uint offset = 0u) {

            return (T*)allocator.GetUnsafePtr(this.ptr, offset);

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, in T data) {

            this.ptr = allocator.Alloc<T>();
            allocator.Ref<T>(this.ptr) = data;

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, void* data, uint dataSize) {

            this.ptr = allocator.Alloc(dataSize, out var ptr);
            if (data != null) {
                _memcpy(data, ptr, dataSize);
            } else {
                _memclear(ptr, dataSize);
            }

        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            allocator.Free(this.ptr);
            this = default;

        }

    }

    public unsafe partial struct MemoryAllocator {
        
        [StructLayout(LayoutKind.Explicit, Size = MemZone.SIZE)]
        public struct MemZone {
            
            public const int SIZE = 4 + MemBlock.SIZE + MemBlockOffset.SIZE;

            [FieldOffset(0)]
            public int size;           // total bytes malloced, including header
            [FieldOffset(4)]
            public MemBlock blocklist; // start / end cap for linked list
            [FieldOffset(4 + MemBlock.SIZE)]
            public MemBlockOffset rover;

        }

        [StructLayout(LayoutKind.Explicit, Size = MemBlock.SIZE)]
        public struct MemBlock {
                        
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            public const int SIZE = 4 + 4 + MemBlockOffset.SIZE + MemBlockOffset.SIZE + 4;
            #else
            public const int SIZE = 4 + 4 + MemBlockOffset.SIZE + MemBlockOffset.SIZE;
            #endif

            [FieldOffset(0)]
            public int size;    // including the header and possibly tiny fragments
            [FieldOffset(4)]
            public byte state;
            [FieldOffset(4 + 4)]
            public MemBlockOffset next;
            [FieldOffset(4 + 4 + MemBlockOffset.SIZE)]
            public MemBlockOffset prev;
            #if MEMORY_ALLOCATOR_BOUNDS_CHECK
            [FieldOffset(4 + 4 + MemBlockOffset.SIZE + MemBlockOffset.SIZE)]
            public int id;      // should be ZONE_ID
            #endif

        };

        public readonly struct MemBlockOffset {

            public const int SIZE = 8;

            public readonly long value;

            [INLINE(256)]
            public MemBlockOffset(void* block, MemZone* zone) {
                this.value = (byte*)block - (byte*)zone;
            }

            [INLINE(256)]
            public MemBlock* Ptr(void* zone) {
                return (MemBlock*)((byte*)zone + this.value);
            }

            [INLINE(256)]
            public static bool operator ==(MemBlockOffset a, MemBlockOffset b) => a.value == b.value;

            [INLINE(256)]
            public static bool operator !=(MemBlockOffset a, MemBlockOffset b) => a.value != b.value;

            [INLINE(256)]
            public bool Equals(MemBlockOffset other) {
                return this.value == other.value;
            }

            [INLINE(256)]
            public override bool Equals(object obj) {
                return obj is MemBlockOffset other && this.Equals(other);
            }

            [INLINE(256)]
            public override int GetHashCode() {
                return this.value.GetHashCode();
            }

        }

    }

}
