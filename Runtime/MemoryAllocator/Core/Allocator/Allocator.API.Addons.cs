namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Allocates relocatable native blocks addressed by zone and offset.
    /// </summary>
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(AllocatorDebugProxy))]
    public unsafe partial struct MemoryAllocator {

        /// <summary>
        /// Allocates an aligned native block and returns its relocatable zone-and-offset address.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr Alloc<T>() where T : unmanaged {
            return this.Alloc(TSize<T>.size);
        }

        /// <summary>
        /// Allocates an aligned native block and returns its relocatable zone-and-offset address.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr Alloc(int size) {
            if (size <= 0) return MemPtr.Invalid;
            return this.Alloc((uint)size, out _);
        }

        /// <summary>
        /// Allocates array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr AllocArray<T>(uint length) where T : unmanaged {
            return this.Alloc(length * TSize<T>.size);
        }

        /// <summary>
        /// Allocates array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr AllocArray<T>(uint length, out safe_ptr<T> ptr) where T : unmanaged {
            var res = this.Alloc(length * TSize<T>.size, out var memPtr);
            ptr = memPtr;
            return res;
        }

        /// <summary>
        /// Allocates array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr AllocArray(uint length, uint elementSize) {
            return this.Alloc(length * elementSize);
        }
        
        /// <summary>
        /// Resizes native storage to hold the requested array element count.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public MemPtr ReAllocArray<T>(in MemPtr memPtr, uint newLength, out safe_ptr<T> ptr) where T : unmanaged {
            var res = this.ReAlloc(memPtr, newLength * TSize<T>.size, out var newMemPtr);
            ptr = newMemPtr;
            return res;
        }

        /// <summary>
        /// Returns a reference to the addressed native value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T Ref<T>(in MemPtr ptr) where T : unmanaged {
            return ref *(T*)this.GetPtr(ptr);
        }

        /// <summary>
        /// Returns a reference to the addressed native value.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T Ref<T>(MemPtr ptr) where T : unmanaged {
            return ref *(T*)this.GetPtr(ptr);
        }

        /// <summary>
        /// Returns reference access to an element in the addressed native array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T RefArray<T>(in MemPtr ptr, uint index) where T : unmanaged {
            return ref *(T*)((byte*)this.GetPtr(in ptr) + TSize<T>.size * index);
        }

        /// <summary>
        /// Returns reference access to an element in the addressed native array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T RefArray<T>(MemPtr ptr, uint index) where T : unmanaged {
            return ref *(T*)((byte*)this.GetPtr(in ptr) + TSize<T>.size * index);
        }

        /// <summary>
        /// Returns reference access to an element in the addressed native array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T RefArray<T>(in MemPtr ptr, int index) where T : unmanaged {
            return ref *(T*)((byte*)this.GetPtr(ptr) + TSize<T>.size * index);
        }

        /// <summary>
        /// Returns reference access to an element in the addressed native array.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly ref T RefArray<T>(MemPtr ptr, int index) where T : unmanaged {
            return ref *(T*)((byte*)this.GetPtr(ptr) + TSize<T>.size * index);
        }

        /// <summary>
        /// Returns a pointer to the requested element of native array storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly MemPtr RefArrayPtr<T>(in MemPtr ptr, uint index) where T : unmanaged {
            return this.GetSafePtr((byte*)((T*)this.GetPtr(ptr) + index), ptr.zoneId);
        }

        /// <summary>
        /// Returns a pointer to the requested element of native array storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly MemPtr RefArrayPtr<T>(MemPtr ptr, uint index) where T : unmanaged {
            return this.GetSafePtr((byte*)((T*)this.GetPtr(ptr) + index), ptr.zoneId);
        }

        /// <summary>
        /// Returns a raw pointer to the underlying storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly safe_ptr<T> GetUnsafePtr<T>(in MemPtr ptr, uint offset = 0u) where T : unmanaged {
            return (safe_ptr)this.GetPtr(new MemPtr(ptr.zoneId, ptr.offset + offset));
        }

        /// <summary>
        /// Returns a raw pointer to the underlying storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly safe_ptr GetUnsafePtr(in MemPtr ptr, uint offset = 0u) {
            return (safe_ptr)this.GetPtr(new MemPtr(ptr.zoneId, ptr.offset + offset));
        }

        /// <summary>
        /// Returns a raw pointer to the underlying storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly safe_ptr<T> GetUnsafePtr<T>(MemPtr ptr, uint offset = 0u) where T : unmanaged {
            return (safe_ptr)this.GetPtr(new MemPtr(ptr.zoneId, ptr.offset + offset));
        }

        /// <summary>
        /// Returns a raw pointer to the underlying storage.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public readonly safe_ptr GetUnsafePtr(MemPtr ptr, uint offset = 0u) {
            return (safe_ptr)this.GetPtr(new MemPtr(ptr.zoneId, ptr.offset + offset));
        }

    }

}