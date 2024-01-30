using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayAutoProxy<>))]
    public unsafe struct MemArrayAuto<T> : IIsCreated where T : unmanaged {

        public static readonly MemArrayAuto<T> Empty = new MemArrayAuto<T>() {
            arrPtr = MemPtr.Invalid,
            Length = 0,
            growFactor = 0,
        };
        
        public CachedPtr<T> cachedPtr;
        public ushort growFactor;
        public MemPtr arrPtr;
        public uint Length;
        public Ent ent;

        public readonly bool isCreated {
            [INLINE(256)]
            get => this.arrPtr.IsValid();
        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory, ushort growFactor = 1) {

            if (length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this = default;
            this.ent = ent;
            this.cachedPtr = default;
            var memPtr = state->allocator.AllocArray(length, out T* ptr);
            this.cachedPtr = new CachedPtr<T>(in state->allocator, ptr);
            this.Length = length;
            this.growFactor = growFactor;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                state->allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.arrPtr = memPtr;
            state->collectionsRegistry.Add(state, in ent, in this.arrPtr);

        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, uint elementSize, uint length, ClearOptions clearOptions, ushort growFactor = 1) {

            if (length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this = default;
            this.ent = ent;
            this.cachedPtr = default;
            this.arrPtr = MemoryAllocatorExt.Alloc(ref state->allocator, elementSize * length, out var tPtr);
            this.cachedPtr = new CachedPtr<T>(in state->allocator, (T*)tPtr);
            this.Length = length;
            this.growFactor = growFactor;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                this.Clear();
            }
            state->collectionsRegistry.Add(state, in ent, in this.arrPtr);
            
        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, in MemArrayAuto<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this.ent = ent;
            this.cachedPtr = default;
            this.Length = arr.Length;
            this.growFactor = arr.growFactor;
            this.arrPtr = state->allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(ref state->allocator, in arr, 0u, ref this, 0u, arr.Length);
            state->collectionsRegistry.Add(state, in ent, in this.arrPtr);

        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this.ent = ent;
            this.cachedPtr = default;
            this.Length = arr.Length;
            this.growFactor = 1;
            this.arrPtr = state->allocator.AllocArray<T>(arr.Length, out var ptr);
            var size = TSize<T>.size;
            _memcpy(arr.ptr, ptr, this.Length * size);
            state->collectionsRegistry.Add(state, in ent, in this.arrPtr);
            
        }

        [INLINE(256)]
        public readonly ref U As<U>(uint index) where U : unmanaged {
            E.RANGE(index, 0, this.Length);
            return ref this.ent.World.state->allocator.RefArray<U>(this.arrPtr, index);
        }
        
        [INLINE(256)]
        public void ReplaceWith(in MemArrayAuto<T> other) {
            
            if (other.arrPtr == this.arrPtr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        [INLINE(256)]
        public void Dispose() {

            var state = this.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.arrPtr);
            if (this.arrPtr.IsValid() == true) {
                state->allocator.Free(this.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.arrPtr,
                ent = this.ent,
                worldId = this.ent.World.id,
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
        public readonly void* GetUnsafePtr() {

            return MemoryAllocatorExt.GetUnsafePtr(in this.ent.World.state->allocator, this.arrPtr);

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
        
        public readonly ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in this.ent.World.state->allocator) + index);
            }
        }

        public readonly ref T this[int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in this.ent.World.state->allocator) + index);
            }
        }

        public readonly ref T this[State* state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in state->allocator) + index);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in allocator) + index);
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in allocator) + index);
            }
        }

        public readonly ref T this[State* state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in state->allocator) + index);
            }
        }

        [INLINE(256)]
        public bool Resize(uint newLength, ClearOptions options = ClearOptions.ClearMemory) {
            return this.Resize(newLength, this.growFactor, options);
        }

        [INLINE(256)]
        public bool Resize(uint newLength, ushort growFactor, ClearOptions options = ClearOptions.ClearMemory) {

            E.IS_CREATED(this);
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var state = this.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.arrPtr);
            var prevLength = this.Length;
            this.arrPtr = state->allocator.ReAllocArray(this.arrPtr, newLength, out T* ptr);
            state->collectionsRegistry.Add(state, in this.ent, in this.arrPtr);
            this.cachedPtr = new CachedPtr<T>(in state->allocator, ptr);
            if (options == ClearOptions.ClearMemory) {
                this.Clear(prevLength, newLength - prevLength);
            }
            this.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public bool Resize(uint elementSize, uint newLength, ClearOptions options = ClearOptions.ClearMemory, ushort growFactor = 1) {

            E.IS_CREATED(this);

            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= this.growFactor;

            var state = this.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.arrPtr);
            var prevLength = this.Length;
            this.arrPtr = MemoryAllocatorExt.Alloc(ref state->allocator, elementSize * newLength, out var tPtr);
            state->collectionsRegistry.Add(state, in this.ent, in this.arrPtr);
            this.cachedPtr = new CachedPtr<T>(in state->allocator, (T*)tPtr);
            if (options == ClearOptions.ClearMemory) {
                this.Clear(prevLength, newLength - prevLength);
            }
            this.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public void Clear() {

            this.Clear(0u, this.Length);

        }

        [INLINE(256)]
        public void Clear(uint index, uint length) {

            E.IS_CREATED(this);

            var size = TSize<T>.size;
            this.ent.World.state->allocator.MemClear(this.arrPtr, index * size, length * size);
            
        }

        [INLINE(256)]
        public readonly bool Contains<U>(U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            var ptr = (T*)this.GetUnsafePtrCached(in this.ent.World.state->allocator);
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