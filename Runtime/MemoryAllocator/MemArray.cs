using Unity.Jobs;

namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public readonly unsafe struct CachedPtr<T> where T : unmanaged {

        [NativeDisableUnsafePtrRestriction]
        private readonly T* cachedPtr;
        private readonly ushort version;

        [INLINE(256)]
        public CachedPtr(in MemoryAllocator allocator, T* ptr) {
            this.version = allocator.version;
            this.cachedPtr = ptr;
        }

        [INLINE(256)]
        public bool IsValid(in MemoryAllocator allocator) {
            return this.version == allocator.version;
        }
        
        [INLINE(256)]
        public T* ReadPtr(in MemoryAllocator allocator) {
            if (allocator.version == this.version) {
                return this.cachedPtr;
            }
            return null;
        }

        [INLINE(256)]
        public void* ReadPtr(in MemoryAllocator allocator, MemPtr arrPtr) {
            if (allocator.version == this.version) {
                return this.cachedPtr;
            }
            return MemoryAllocatorExt.GetUnsafePtr(in allocator, arrPtr);
        }

        [INLINE(256)]
        public ref T Read(in MemoryAllocator allocator, MemPtr arrPtr, int index) {
            if (allocator.version == this.version) {
                return ref *(this.cachedPtr + index);
            }
            return ref allocator.RefArray<T>(arrPtr, index);
        }

        [INLINE(256)]
        public ref T Read(in MemoryAllocator allocator, MemPtr arrPtr, uint index) {
            if (allocator.version == this.version) {
                return ref *(this.cachedPtr + index);
            }
            return ref allocator.RefArray<T>(arrPtr, index);
        }

    }
    
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayProxy<>))]
    public unsafe struct MemArray<T> where T : unmanaged {

        public static MemArray<T> Empty => new MemArray<T>() {
            arrPtr = MemPtr.Invalid,
            Length = 0,
            growFactor = 0,
        };
        
        public CachedPtr<T> cachedPtr;
        public ushort growFactor;
        public MemPtr arrPtr;
        public uint Length;

        public readonly bool isCreated {
            [INLINE(256)]
            get => this.arrPtr.IsValid();
        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory, ushort growFactor = 1) {

            if (length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            this.cachedPtr = default;
            var memPtr = allocator.AllocArray(length, out T* ptr);
            this.cachedPtr = new CachedPtr<T>(in allocator, ptr);
            this.Length = length;
            this.growFactor = growFactor;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.arrPtr = memPtr;

        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, uint elementSize, uint length, ClearOptions clearOptions, ushort growFactor = 1) {

            if (length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            this.cachedPtr = default;
            this.arrPtr = MemoryAllocatorExt.Alloc(ref allocator, elementSize * length, out var tPtr);
            this.cachedPtr = new CachedPtr<T>(in allocator, (T*)tPtr);
            this.Length = length;
            this.growFactor = growFactor;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                this.Clear(ref allocator);
            }
            
        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, in MemArray<T> arr) {

            if (arr.Length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this.cachedPtr = default;
            this.Length = arr.Length;
            this.growFactor = arr.growFactor;
            this.arrPtr = allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(ref allocator, in arr, 0u, ref this, 0u, arr.Length);

        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this.cachedPtr = default;
            this.Length = arr.Length;
            this.growFactor = 1;
            this.arrPtr = allocator.AllocArray<T>(arr.Length, out var ptr);
            var size = TSize<T>.size;
            UnsafeUtility.MemCpy(ptr, arr.ptr, this.Length * size);
            
        }

        [INLINE(256)]
        public MemArray(MemPtr ptr, uint length, ushort growFactor) {

            this.arrPtr = ptr;
            this.Length = length;
            this.growFactor = growFactor;
            this.cachedPtr = default;

        }

        [INLINE(256)]
        public readonly ref U As<U>(in MemoryAllocator allocator, uint index) where U : struct {
            E.RANGE(index, 0, this.Length);
            return ref allocator.RefArray<U>(this.arrPtr, index);
        }
        
        [INLINE(256)]
        public void ReplaceWith(ref MemoryAllocator allocator, in MemArray<T> other) {
            
            if (other.arrPtr == this.arrPtr) {
                return;
            }
            
            this.Dispose(ref allocator);
            this = other;
            
        }

        [INLINE(256)]
        public void CopyFrom(ref MemoryAllocator allocator, in MemArray<T> other) {

            if (other.arrPtr == this.arrPtr) return;
            if (this.arrPtr.IsValid() == false && other.arrPtr.IsValid() == false) return;
            if (this.arrPtr.IsValid() == true && other.arrPtr.IsValid() == false) {
                this.Dispose(ref allocator);
                return;
            }
            if (this.arrPtr.IsValid() == false) this = new MemArray<T>(ref allocator, other.Length);
            
            NativeArrayUtils.Copy(ref allocator, in other, ref this);
            
        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            if (this.arrPtr.IsValid() == true) {
                allocator.Free(this.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeJob() {
                ptr = this.arrPtr,
                worldId = worldId,
            }.Schedule(inputDeps);
            
            this = default;

            return jobHandle;

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            if (state == true && this.isCreated == true) {
                this.cachedPtr = new CachedPtr<T>(in allocator, (T*)this.GetUnsafePtr(in allocator));
            } else {
                this.cachedPtr = default;
            }
        }

        [INLINE(256)]
        public readonly void* GetUnsafePtr(in MemoryAllocator allocator) {

            return MemoryAllocatorExt.GetUnsafePtr(in allocator, this.arrPtr);

        }

        [INLINE(256)]
        public readonly void* GetUnsafePtrCached(in MemoryAllocator allocator) {

            return this.cachedPtr.ReadPtr(in allocator, this.arrPtr);

        }

        [INLINE(256)]
        public MemPtr GetAllocPtr(in MemoryAllocator allocator, uint index) {
            
            return allocator.RefArrayPtr<T>(this.arrPtr, index);
            
        }

        [INLINE(256)]
        public ref T Read(in MemoryAllocator allocator, uint index) {
            
            E.RANGE(index, 0, this.Length);
            return ref this.cachedPtr.Read(in allocator, this.arrPtr, index);
            
        }

        public readonly ref T this[State* state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref this.cachedPtr.Read(in state->allocator, this.arrPtr, index);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref this.cachedPtr.Read(in allocator, this.arrPtr, index);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref this.cachedPtr.Read(in allocator, this.arrPtr, index);
            }
        }

        public readonly ref T this[State* state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref this.cachedPtr.Read(in state->allocator, this.arrPtr, index);
            }
        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint newLength, ClearOptions options = ClearOptions.ClearMemory) {
            return this.Resize(ref allocator, newLength, this.growFactor, options);
        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint newLength, ushort growFactor, ClearOptions options = ClearOptions.ClearMemory) {

            if (this.isCreated == false) {

                this = new MemArray<T>(ref allocator, newLength, options, growFactor);
                return true;

            }
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var prevLength = this.Length;
            this.arrPtr = allocator.ReAllocArray(this.arrPtr, newLength, out T* ptr);
            this.cachedPtr = new CachedPtr<T>(in allocator, ptr);
            if (options == ClearOptions.ClearMemory) {
                this.Clear(ref allocator, prevLength, newLength - prevLength);
            }
            this.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint elementSize, uint newLength, ClearOptions options = ClearOptions.ClearMemory, ushort growFactor = 1) {

            if (this.isCreated == false) {

                this = new MemArray<T>(ref allocator, newLength, options, growFactor);
                return true;

            }
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= this.growFactor;

            var prevLength = this.Length;
            this.arrPtr = MemoryAllocatorExt.Alloc(ref allocator, elementSize * newLength, out var tPtr);
            this.cachedPtr = new CachedPtr<T>(in allocator, (T*)tPtr);
            if (options == ClearOptions.ClearMemory) {
                this.Clear(ref allocator, prevLength, newLength - prevLength);
            }
            this.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {

            this.Clear(ref allocator, 0u, this.Length);

        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator, uint index, uint length) {

            var size = TSize<T>.size;
            allocator.MemClear(this.arrPtr, index * size, length * size);
            
        }

        [INLINE(256)]
        public readonly bool Contains<U>(in MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            var ptr = (T*)this.GetUnsafePtrCached(in allocator);
            for (uint i = 0, cnt = this.Length; i < cnt; ++i) {

                if (obj.Equals(*(ptr + i)) == true) {

                    return true;

                }
                
            }

            return false;

        }

        public uint GetReservedSizeInBytes() {

            return this.Length * (uint)sizeof(T);

        }

    }

}