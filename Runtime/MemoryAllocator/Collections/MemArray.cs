//#define USE_CACHE_PTR
namespace ME.BECS {
    
    using System.Runtime.CompilerServices;
    #if NO_INLINE
    using INLINE = NoInlineAttribute;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    using System.Runtime.InteropServices;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    
    [IgnoreProfiler]
    [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
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

    [StructLayout(LayoutKind.Explicit)]
    public struct MemArrayData {

        #if USE_CACHE_PTR
        public const int SIZE = 24;
        #else
        public const int SIZE = 12;
        #endif

        [FieldOffset(0)]
        public MemPtr arrPtr;
        [FieldOffset(8)]
        public volatile uint Length;
        #if USE_CACHE_PTR
        [FieldOffset(12)]
        public CachedPtr cachedPtr;
        #endif

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.arrPtr);
            writer.Write(this.Length);
            #if USE_CACHE_PTR
            writer.Write(this.cachedPtr);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.arrPtr);
            var length = 0u;
            reader.Read(ref length);
            this.Length = length;
            #if USE_CACHE_PTR
            reader.Read(ref this.cachedPtr);
            #endif
        }

    }

    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayProxy<>))]
    public unsafe struct MemArray<T> : IIsCreated where T : unmanaged {

        public const int SIZE = MemArrayData.SIZE;
        
        public static readonly MemArray<T> Empty = new MemArray<T>() {
            data = new MemArrayData() {
                arrPtr = MemPtr.Invalid,
                Length = 0u,
            },
        };

        private MemArrayData data;
        public readonly uint Length => this.data.Length;
        public readonly MemPtr arrPtr => this.data.arrPtr;

        public readonly bool IsCreated {
            [INLINE(256)]
            get => this.data.arrPtr.IsValid() == true || (this.IsInlined == true && this.Length > 0u);
        }

        private readonly bool IsInlined => TSize<T>.size * this.Length <= MemPtr.SIZE;

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            this.data.SerializeHeaders(ref writer);
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            this.data.DeserializeHeaders(ref reader);
        }

        public MemArray(ref MemoryAllocator allocator, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            if (length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this.data = default;
            this.data.Length = length;
            if (this.IsInlined == true) {
                // we can inline the data into array without allocating any memory
                this.data.arrPtr = default;
                return;
            }
            
            var memPtr = allocator.AllocArray(length, out safe_ptr<T> ptr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in allocator, ptr);
            #endif
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.data.arrPtr = memPtr;

        }

        public MemArray(ref MemoryAllocator allocator, in MemArray<T> arr) {

            if (arr.Length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            this.data.Length = arr.Length;
            if (this.IsInlined == true) {
                NativeArrayUtils.CopyNoChecks(ref allocator, in arr, 0u, ref this, 0u, arr.Length);
                return;
            }
            
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.arrPtr = allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(ref allocator, in arr, 0u, ref this, 0u, arr.Length);

        }

        public MemArray(ref MemoryAllocator allocator, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArray<T>.Empty;
                return;
            }

            this = default;
            var size = TSize<T>.size;
            this.data.Length = arr.Length;
            if (this.IsInlined == true) {
                _memcpy(arr.ptr, this.GetUnsafePtr(in allocator), this.Length * size);
                return;
            }
            
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.Length = arr.Length;
            this.data.arrPtr = allocator.AllocArray<T>(arr.Length, out var ptr);
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
            if (this.IsCreated == false && other.IsCreated == false) return;
            if (this.IsCreated == true && other.IsCreated == false) {
                this.Dispose(ref allocator);
                return;
            }
            if (this.IsCreated == false) this = new MemArray<T>(ref allocator, other.Length);
            
            NativeArrayUtils.Copy(ref allocator, in other, ref this);
            
        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            if (this.IsInlined == true) {
                this = default;
                return;
            }
            
            if (this.data.arrPtr.IsValid() == true) {
                allocator.Free(this.data.arrPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(ushort worldId, Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);

            if (this.IsInlined == true) {
                this = default;
                return inputDeps;
            }
            
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
            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr;
            }

            return allocator.GetUnsafePtr(this.data.arrPtr);

        }

        [INLINE(256)]
        public readonly safe_ptr<T> GetUnsafePtr(in MemoryAllocator allocator, uint index) {

            E.IS_CREATED(this);
            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr + index;
            }
            
            return (safe_ptr<T>)allocator.GetUnsafePtr(this.data.arrPtr) + index;

        }

        [INLINE(256)]
        public readonly safe_ptr<T> GetUnsafePtr(safe_ptr<State> state, uint index) {

            E.IS_CREATED(this);
            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr + index;
            }
            
            return (safe_ptr<T>)state.ptr->allocator.GetUnsafePtr(this.data.arrPtr) + index;

        }

        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtrCached(in MemoryAllocator allocator) {

            E.IS_CREATED(this);
            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr;
            }
            
            #if USE_CACHE_PTR
            return CachedPtr.ReadPtr(in this.data.cachedPtr, in allocator, this.data.arrPtr);
            #else
            return this.GetUnsafePtr(in allocator);
            #endif

        }

        [INLINE(256)]
        public readonly MemPtr GetAllocPtr(in MemoryAllocator allocator, uint index) {
            
            E.IS_CREATED(this);
            if (this.IsInlined == true) {
                return default;
            }
            
            return allocator.RefArrayPtr<T>(this.data.arrPtr, index);
            
        }

        [INLINE(256)]
        public readonly ref T Read(in MemoryAllocator allocator, uint index) {
            
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

            if (this.IsInlined == true) {
                if (TSize<T>.size * newLength > MemPtr.SIZE) {
                    var arrPtr = allocator.AllocArray(newLength, out safe_ptr<T> ptr);
                    #if USE_CACHE_PTR
                    this.data.cachedPtr = new CachedPtr(in state.ptr->allocator, ptr);
                    #endif
                    _memcpy(this.GetUnsafePtr(in allocator), allocator.GetUnsafePtr(arrPtr), this.Length * TSize<T>.size);
                    var oldLength = this.Length;
                    this.data.arrPtr = arrPtr;
                    this.data.Length = newLength;
                    if (options == ClearOptions.ClearMemory) {
                        var size = TSize<T>.size;
                        _memclear(this.GetUnsafePtr(in allocator) + oldLength * size, (newLength - oldLength) * size);
                    }
                } else {
                    var size = TSize<T>.size;
                    _memclear(this.GetUnsafePtr(in allocator) + this.Length * size, (newLength - this.Length) * size);
                }
            } else {
                var prevLength = this.Length;
                this.data.arrPtr = allocator.ReAllocArray(this.data.arrPtr, newLength, out safe_ptr<T> ptr);
                #if USE_CACHE_PTR
                this.data.cachedPtr = new CachedPtr(in allocator, ptr);
                #endif
                if (options == ClearOptions.ClearMemory) {
                    var size = TSize<T>.size;
                    _memclear(this.GetUnsafePtr(in allocator) + prevLength * size, (newLength - prevLength) * size);
                }
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

            if (length == 0u) return;
            
            E.IS_CREATED(this);
            E.RANGE(index, 0u, this.Length);
            E.RANGE(length - 1u, 0u, this.Length);

            var size = TSize<T>.size;
            if (this.IsInlined == true) {
                _memclear(this.GetUnsafePtr(in allocator) + index * size, length * size);
            } else {
                allocator.MemClear(this.data.arrPtr, index * size, length * size);
            }

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