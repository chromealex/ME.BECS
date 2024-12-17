//#define USE_CACHE_PTR
namespace ME.BECS {
    
    #if NO_INLINE
    using INLINE = NoInlineAttribute;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    using System.Runtime.InteropServices;
    
    [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public readonly unsafe struct CachedPtr {
        
        internal readonly safe_ptr cachedPtr;
        internal readonly ushort version;
        
        [INLINE(256)]
        public CachedPtr(in MemoryAllocator allocator, safe_ptr ptr) {
            this.version = allocator.version;
            this.cachedPtr = ptr;
        }

        [INLINE(256)]
        public static bool IsValid(in CachedPtr cache, in MemoryAllocator allocator) {
            return cache.version == allocator.version;
        }
        
        [INLINE(256)]
        public static safe_ptr<T> ReadPtr<T>(in CachedPtr cache, in MemoryAllocator allocator) where T : unmanaged {
            if (allocator.version == cache.version) {
                return cache.cachedPtr;
            }
            return default;
        }

        [INLINE(256)]
        public static safe_ptr ReadPtr(in CachedPtr cache, in MemoryAllocator allocator, in MemPtr arrPtr) {
            if (allocator.version == cache.version) {
                return cache.cachedPtr;
            }
            return allocator.GetUnsafePtr(in arrPtr);
        }

        [INLINE(256)]
        public static ref T Read<T>(CachedPtr cache, in MemoryAllocator allocator, MemPtr arrPtr, int index) where T : unmanaged {
            if (allocator.version == cache.version) {
                return ref *((safe_ptr<T>)cache.cachedPtr + index).ptr;
            }
            return ref allocator.RefArray<T>(arrPtr, index);
        }

        [INLINE(256)]
        public static ref T Read<T>(CachedPtr cache, in MemoryAllocator allocator, MemPtr arrPtr, uint index) where T : unmanaged {
            if (allocator.version == cache.version) {
                return ref *((safe_ptr<T>)cache.cachedPtr + index).ptr;
            }
            return ref allocator.RefArray<T>(arrPtr, index);
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemArrayData {

        #if USE_CACHE_PTR
        public const int SIZE = 24;
        #else
        public const int SIZE = 12;
        #endif

        public MemPtr arrPtr;
        public volatile uint Length;
        #if USE_CACHE_PTR
        public CachedPtr cachedPtr;
        #endif

    }

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayProxy<>))]
    public unsafe struct MemArray<T> : IIsCreated where T : unmanaged {

        public const int SIZE = MemArrayData.SIZE;
        
        public static readonly MemArray<T> Empty = new MemArray<T>() {
            data = new MemArrayData() {
                arrPtr = MemPtr.Invalid,
                Length = 0,
            },
        };

        private MemArrayData data;
        public readonly uint Length => this.data.Length;
        public readonly MemPtr arrPtr => this.data.arrPtr;

        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.data.arrPtr.IsValid();
        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            if (length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            var memPtr = allocator.AllocArray(length, out safe_ptr<T> ptr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in allocator, ptr);
            #endif
            this.data.Length = length;
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.data.arrPtr = memPtr;

        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, uint elementSize, uint length, ClearOptions clearOptions) {

            if (length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            this.data.arrPtr = allocator.Alloc(elementSize * length, out var tPtr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in allocator, (T*)tPtr);
            #endif
            this.data.Length = length;
            
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
            
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.Length = arr.Length;
            this.data.arrPtr = allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(ref allocator, in arr, 0u, ref this, 0u, arr.Length);

        }

        [INLINE(256)]
        public MemArray(ref MemoryAllocator allocator, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.Length = arr.Length;
            this.data.arrPtr = allocator.AllocArray<T>(arr.Length, out var ptr);
            var size = TSize<T>.size;
            _memcpy(arr.ptr, ptr, this.Length * size);
            
        }

        [INLINE(256)]
        public MemArray(MemPtr ptr, uint length) {

            this.data.arrPtr = ptr;
            this.data.Length = length;
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif

        }

        [INLINE(256)]
        public readonly ref U As<U>(in MemoryAllocator allocator, uint index) where U : unmanaged {
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Length);
            return ref allocator.RefArray<U>(this.data.arrPtr, index);
        }
        
        [INLINE(256)]
        public void ReplaceWith(ref MemoryAllocator allocator, in MemArray<T> other) {
            
            E.IS_CREATED(this);
            if (other.data.arrPtr == this.data.arrPtr) {
                return;
            }
            
            this.Dispose(ref allocator);
            this = other;
            
        }

        [INLINE(256)]
        public void CopyFrom(ref MemoryAllocator allocator, in MemArray<T> other) {

            if (other.data.arrPtr == this.data.arrPtr) return;
            if (this.data.arrPtr.IsValid() == false && other.data.arrPtr.IsValid() == false) return;
            if (this.data.arrPtr.IsValid() == true && other.data.arrPtr.IsValid() == false) {
                this.Dispose(ref allocator);
                return;
            }
            if (this.data.arrPtr.IsValid() == false) this = new MemArray<T>(ref allocator, other.Length);
            
            NativeArrayUtils.Copy(ref allocator, in other, ref this);
            
        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            if (this.data.arrPtr.IsValid() == true) {
                allocator.Free(this.data.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            var jobHandle = new DisposeJob() {
                ptr = this.data.arrPtr,
                worldId = worldId,
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
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            E.IS_CREATED(this);
            return allocator.GetUnsafePtr(this.data.arrPtr);

        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtrCached(in MemoryAllocator allocator) {

            E.IS_CREATED(this);
            #if USE_CACHE_PTR
            return CachedPtr.ReadPtr(in this.data.cachedPtr, in allocator, this.data.arrPtr);
            #else
            return this.GetUnsafePtr(in allocator);
            #endif

        }

        [INLINE(256)]
        public MemPtr GetAllocPtr(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            return allocator.RefArrayPtr<T>(this.data.arrPtr, index);
            
        }

        [INLINE(256)]
        public ref T Read(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            E.RANGE(index, 0, this.Length);
            #if USE_CACHE_PTR
            return ref CachedPtr.Read<T>(this.data.cachedPtr, in allocator, this.data.arrPtr, index);
            #else
            return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            #endif

        }

        public readonly ref T this[safe_ptr<State> state, int index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in state.ptr->allocator) + index).ptr;
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            }
        }

        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            }
        }

        public readonly ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in state.ptr->allocator) + index).ptr;
            }
        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint newLength, ushort growFactor, ClearOptions options = ClearOptions.ClearMemory) {

            if (this.IsCreated == false) {

                this = new MemArray<T>(ref allocator, newLength, options);
                return true;

            }
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var prevLength = this.Length;
            this.data.arrPtr = allocator.ReAllocArray(this.data.arrPtr, newLength, out safe_ptr<T> ptr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in allocator, ptr);
            #endif
            if (options == ClearOptions.ClearMemory) {
                this.Clear(ref allocator, prevLength, newLength - prevLength);
            }
            this.data.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public bool Resize(ref MemoryAllocator allocator, uint elementSize, uint newLength, ClearOptions options = ClearOptions.ClearMemory, ushort growFactor = 1) {

            if (this.IsCreated == false) {

                this = new MemArray<T>(ref allocator, newLength, options);
                return true;

            }
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var prevLength = this.Length;
            this.data.arrPtr = allocator.Alloc(elementSize * newLength, out var tPtr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in allocator, (T*)tPtr);
            #endif
            if (options == ClearOptions.ClearMemory) {
                this.Clear(ref allocator, prevLength, newLength - prevLength);
            }
            this.data.Length = newLength;
            return true;

        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {

            this.Clear(ref allocator, 0u, this.Length);

        }

        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator, uint index, uint length) {

            E.IS_CREATED(this);

            var size = TSize<T>.size;
            allocator.MemClear(this.data.arrPtr, index * size, length * size);
            
        }

        [INLINE(256)]
        public readonly bool Contains<U>(in MemoryAllocator allocator, U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            var ptr = (safe_ptr<T>)this.GetUnsafePtrCached(in allocator);
            for (uint i = 0, cnt = this.Length; i < cnt; ++i) {

                if (obj.Equals(*(ptr + i).ptr) == true) {

                    return true;

                }
                
            }

            return false;

        }

        public uint GetReservedSizeInBytes() {

            return this.Length * TSize<T>.size;

        }

    }

}