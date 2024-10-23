
namespace ME.BECS {
    
    #if NO_INLINE
    using INLINE = NoInlineAttribute;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Jobs;
    using static Cuts;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Size = MemArrayAutoData.SIZE)]
    [System.Serializable]
    public struct MemArrayAutoData {

        #if USE_CACHE_PTR
        public const int SIZE = 36;
        #else
        public const int SIZE = 24;
        #endif

        public MemPtr arrPtr;
        public Ent ent;
        public uint Length;
        #if USE_CACHE_PTR
        public CachedPtr cachedPtr;
        #endif

    }

    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayAutoProxy<>))]
    public unsafe struct MemArrayAuto<T> : IMemArray, IUnmanagedList where T : unmanaged {

        public static readonly MemArrayAuto<T> Empty = new MemArrayAuto<T>() {
            data = new MemArrayAutoData() {
                arrPtr = MemPtr.Invalid,
                Length = 0,
            },
        };

        public MemArrayAutoData data;
        public readonly uint Length => this.data.Length;
        public readonly Ent ent => this.data.ent;
        public readonly MemPtr arrPtr => this.data.arrPtr;
        
        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.data.arrPtr.IsValid();
        }

        public Ent Ent => this.ent;

        public uint GetConfigId() => this.data.Length;
        
        object[] IUnmanagedList.ToManagedArray() {
            var arr = new object[this.data.Length];
            for (uint i = 0u; i < this.data.Length; ++i) {
                arr[i] = this[i];
            }
            return arr;
        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, void* data, uint length) : this(in ent, length, ClearOptions.UninitializedMemory) {

            if (this.IsCreated == true) {

                var elemSize = TSize<T>.size;
                _memcpy(data, this.GetUnsafePtr(), length * elemSize);
                
            }

        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            if (length == 0u) {
                this = MemArrayAuto<T>.Empty;
                this.data.ent = ent;
                return;
            }

            var state = ent.World.state;
            this = default;
            this.data.ent = ent;
            var memPtr = state->allocator.AllocArray(length, out T* ptr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in state->allocator, ptr);
            #endif
            this.data.Length = length;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                state->allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.data.arrPtr = memPtr;
            state->collectionsRegistry.Add(state, in ent, in this.data.arrPtr);

        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, uint elementSize, uint length, ClearOptions clearOptions) {

            if (length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this = default;
            this.data.ent = ent;
            this.data.arrPtr = state->allocator.Alloc(elementSize * length, out var tPtr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in state->allocator, (T*)tPtr);
            #endif
            this.data.Length = length;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                this.Clear();
            }
            state->collectionsRegistry.Add(state, in ent, in this.data.arrPtr);
            
        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, in MemArrayAuto<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this.data.ent = ent;
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.Length = arr.Length;
            this.data.arrPtr = state->allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(in arr, 0u, ref this, 0u, arr.Length);
            state->collectionsRegistry.Add(state, in ent, in this.data.arrPtr);

        }

        [INLINE(256)]
        public MemArrayAuto(in Ent ent, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            var state = ent.World.state;
            this.data.ent = ent;
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.Length = arr.Length;
            this.data.arrPtr = state->allocator.AllocArray<T>(arr.Length, out var ptr);
            var size = TSize<T>.size;
            _memcpy(arr.ptr, ptr, this.Length * size);
            state->collectionsRegistry.Add(state, in ent, in this.data.arrPtr);
            
        }

        [INLINE(256)]
        public readonly ref U As<U>(uint index) where U : unmanaged {
            E.RANGE(index, 0, this.Length);
            return ref this.data.ent.World.state->allocator.RefArray<U>(this.data.arrPtr, index);
        }
        
        [INLINE(256)]
        public void ReplaceWith(in MemArrayAuto<T> other) {
            
            if (other.data.arrPtr == this.data.arrPtr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        [INLINE(256)]
        public void Dispose() {

            var state = this.data.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.data.ent, in this.data.arrPtr);
            if (this.data.arrPtr.IsValid() == true) {
                state->allocator.Free(this.data.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.data.arrPtr,
                ent = this.data.ent,
                worldId = this.data.ent.World.id,
            }.Schedule(inputDeps);
            
            this = default;

            return jobHandle;

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            #if USE_CACHE_PTR
            if (state == true && this.IsCreated == true) {
                this.data.cachedPtr = new CachedPtr(in allocator, (T*)this.GetUnsafePtr(in allocator));
            } else {
                this.data.cachedPtr = default;
            }
            #endif
        }

        [INLINE(256)]
        public readonly void* GetUnsafePtr() {

            return this.data.ent.World.state->allocator.GetUnsafePtr(this.data.arrPtr);

        }

        [INLINE(256)]
        public readonly void* GetUnsafePtr(in MemoryAllocator allocator) {

            return allocator.GetUnsafePtr(this.data.arrPtr);

        }

        [INLINE(256)]
        public readonly void* GetUnsafePtrCached(in MemoryAllocator allocator) {

            #if USE_CACHE_PTR
            return CachedPtr.ReadPtr(in this.data.cachedPtr, in allocator, this.data.arrPtr);
            #else
            return this.GetUnsafePtr(in allocator);
            #endif

        }

        [INLINE(256)]
        public readonly void* GetUnsafePtrCached() {

            #if USE_CACHE_PTR
            return CachedPtr.ReadPtr(in this.data.cachedPtr, in this.data.ent.World.state->allocator, this.data.arrPtr);
            #else
            return this.GetUnsafePtr();
            #endif

        }

        [INLINE(256)]
        public MemPtr GetAllocPtr(uint index) {
            
            return this.data.ent.World.state->allocator.RefArrayPtr<T>(this.data.arrPtr, index);
            
        }

        [INLINE(256)]
        public ref T Read(in MemoryAllocator allocator, uint index) {
            
            E.RANGE(index, 0, this.Length);
            #if USE_CACHE_PTR
            return ref CachedPtr.Read<T>(this.data.cachedPtr, in allocator, this.data.arrPtr, index);
            #else
            return ref *((T*)this.GetUnsafePtrCached(in this.data.ent.World.state->allocator) + index);
            #endif
            
        }
        
        public readonly ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in this.data.ent.World.state->allocator) + index);
            }
        }

        public readonly ref T this[int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((T*)this.GetUnsafePtrCached(in this.data.ent.World.state->allocator) + index);
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
        public bool Resize(uint newLength, ushort growFactor, ClearOptions options = ClearOptions.ClearMemory) {

            E.IS_CREATED(this);
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var state = this.data.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.data.ent, in this.data.arrPtr);
            var prevLength = this.Length;
            this.data.arrPtr = state->allocator.ReAllocArray(this.data.arrPtr, newLength, out T* ptr);
            state->collectionsRegistry.Add(state, in this.data.ent, in this.data.arrPtr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in state->allocator, ptr);
            #endif
            if (options == ClearOptions.ClearMemory) {
                this.Clear(prevLength, newLength - prevLength);
            }
            this.data.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public bool Resize(uint elementSize, uint newLength, ClearOptions options = ClearOptions.ClearMemory, ushort growFactor = 1) {

            E.IS_CREATED(this);

            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var state = this.data.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.data.ent, in this.data.arrPtr);
            var prevLength = this.Length;
            this.data.arrPtr = state->allocator.Alloc(elementSize * newLength, out var tPtr);
            state->collectionsRegistry.Add(state, in this.data.ent, in this.data.arrPtr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in state->allocator, (T*)tPtr);
            #endif
            if (options == ClearOptions.ClearMemory) {
                this.Clear(prevLength, newLength - prevLength);
            }
            this.data.Length = newLength;
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
            this.data.ent.World.state->allocator.MemClear(this.data.arrPtr, index * size, length * size);
            
        }

        [INLINE(256)]
        public readonly bool Contains<U>(U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            var ptr = (T*)this.GetUnsafePtrCached(in this.data.ent.World.state->allocator);
            for (uint i = 0, cnt = this.Length; i < cnt; ++i) {

                if (obj.Equals(*(ptr + i)) == true) {

                    return true;

                }
                
            }

            return false;

        }

        [INLINE(256)]
        public void CopyFrom(in MemArrayAuto<T> other) {

            if (other.data.arrPtr == this.data.arrPtr) return;
            if (this.data.arrPtr.IsValid() == false && other.data.arrPtr.IsValid() == false) return;
            if (this.data.arrPtr.IsValid() == true && other.data.arrPtr.IsValid() == false) {
                this.Dispose();
                return;
            }
            if (this.data.arrPtr.IsValid() == false) this = new MemArrayAuto<T>(in other.data.ent, other.Length);
            
            NativeArrayUtils.Copy(in other, ref this);
            
        }
        
        public uint GetReservedSizeInBytes() {

            return this.Length * (uint)sizeof(T);

        }

    }

}