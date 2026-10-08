namespace ME.BECS {

    using System.Runtime.CompilerServices;
    #if NO_INLINE
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = NoInlineAttribute;
    #endif
    #else
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    #endif
    using Unity.Jobs;
    using static Cuts;
    using System.Runtime.InteropServices;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides mem array auto data storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [StructLayout(LayoutKind.Explicit, Size = MemArrayAutoData.SIZE)]
    [System.Serializable]
    public struct MemArrayAutoData {

        #if USE_CACHE_PTR
        /// <summary>
        /// Storage size or fixed element count used by this representation.
        /// </summary>
        public const int SIZE = 36;
        #else
        /// <summary>
        /// Storage size or fixed element count used by this representation.
        /// </summary>
        public const int SIZE = 24;
        #endif

        /// <summary>
        /// Native pointer or typed storage accessor for arr.
        /// </summary>
        [FieldOffset(0)]
        public MemPtr arrPtr;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        [FieldOffset(8)]
        public Ent ent;
        /// <summary>
        /// Number of elements exposed by this value.
        /// </summary>
        [FieldOffset(16)]
        public uint Length;
        #if USE_CACHE_PTR
        /// <summary>
        /// Cached native address; its lifetime is tied to the backing allocation.
        /// </summary>
        [FieldOffset(20)]
        public CachedPtr cachedPtr;
        #endif

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.arrPtr);
            writer.Write(this.ent);
            writer.Write(this.Length);
            #if USE_CACHE_PTR
            writer.Write(this.cachedPtr);
            #endif
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.arrPtr);
            reader.Read(ref this.ent);
            reader.Read(ref this.Length);
            #if USE_CACHE_PTR
            reader.Read(ref this.cachedPtr);
            #endif
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() => this.arrPtr.GetHashCode();
        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(MemArrayAutoData obj) => this.arrPtr.Equals(obj.arrPtr);

    }

    /// <summary>
    /// Provides mem array auto storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Serializable]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(MemArrayAutoProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct MemArrayAuto<T> : IMemArray, IUnmanagedList, System.IEquatable<MemArrayAuto<T>> where T : unmanaged {

        /// <summary>
        /// Empty used by <c>MemArrayAuto</c>.
        /// </summary>
        public static readonly MemArrayAuto<T> Empty = new MemArrayAuto<T>() {
            data = new MemArrayAutoData() {
                arrPtr = MemPtr.Invalid,
                Length = 0,
            },
        };

        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public MemArrayAutoData data;
        /// <summary>
        /// Number of elements exposed by this value.
        /// </summary>
        public readonly uint Length => this.data.Length;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public readonly Ent ent => this.data.ent;
        /// <summary>
        /// Gets arr ptr; this implementation returns <c>this.data.arrPtr</c>.
        /// </summary>
        public readonly MemPtr arrPtr => this.data.arrPtr;
        /// <summary>
        /// Number of stored elements exposed by the collection interface.
        /// </summary>
        public uint ElementsCount => this.Length;
        
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool IsCreated {
            [INLINE(256)]
            get => (this.data.arrPtr.IsValid() == true || this.IsInlined == true) && this.data.ent.IsAlive() == true;
        }

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent Ent => this.ent;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            this.data.SerializeHeaders(ref writer);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            this.data.DeserializeHeaders(ref reader);
        }

        /// <summary>
        /// Returns config ID.
        /// </summary>
        public uint GetConfigId() => this.data.Length;

        private readonly bool IsInlined => false; // TSize<T>.size * this.Length <= MemPtr.SIZE;
        
        object[] IUnmanagedList.ToManagedArray() {
            var arr = new object[this.data.Length];
            for (uint i = 0u; i < this.data.Length; ++i) {
                arr[i] = this[i];
            }
            return arr;
        }

        /// <summary>
        /// Initializes <c>MemArrayAuto</c> with storage for the requested number of elements.
        /// </summary>
        public MemArrayAuto(in Ent ent, safe_ptr data, uint length) : this(in ent, length, ClearOptions.UninitializedMemory) {

            if (this.IsCreated == true) {

                var elemSize = TSize<T>.size;
                _memcpy(data, this.GetUnsafePtr(), length * elemSize);
                
            }

        }

        /// <summary>
        /// Initializes <c>MemArrayAuto</c> with storage for the requested number of elements.
        /// </summary>
        public MemArrayAuto(in Ent ent, uint length, ClearOptions clearOptions = ClearOptions.ClearMemory) {

            if (length == 0u) {
                this = MemArrayAuto<T>.Empty;
                this.data.ent = ent;
                return;
            }

            this = default;
            this.data.Length = length;
            if (this.IsInlined == true) {
                // we can inline the data into array without allocating any memory
                this.data.ent = ent;
                this.data.arrPtr = default;
                return;
            }
            
            var state = ent.World.state;
            this.data.ent = ent;
            var memPtr = state.ptr->allocator.AllocArray(length, out safe_ptr<T> ptr);
            #if USE_CACHE_PTR
            this.data.cachedPtr = new CachedPtr(in state.ptr->allocator, ptr);
            #endif
            
            if (clearOptions == ClearOptions.ClearMemory) {
                var size = TSize<T>.size;
                state.ptr->allocator.MemClear(memPtr, 0u, length * size);
            }
            
            this.data.arrPtr = memPtr;
            CollectionsRegistry.Add(state, in ent, in this.data.arrPtr);

        }

        /// <summary>
        /// Initializes <c>MemArrayAuto</c> from the supplied ent, arr.
        /// </summary>
        public MemArrayAuto(in Ent ent, in MemArrayAuto<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            this = default;
            this.data.Length = arr.Length;
            if (this.IsInlined == true) {
                // we can inline the data into array without allocating any memory
                this.data.ent = ent;
                this.data.arrPtr = default;
                NativeArrayUtils.CopyNoChecks(in arr, 0u, ref this, 0u, arr.Length);
                return;
            }
            
            var state = ent.World.state;
            this.data.ent = ent;
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.arrPtr = state.ptr->allocator.AllocArray<T>(arr.Length);
            NativeArrayUtils.CopyNoChecks(in arr, 0u, ref this, 0u, arr.Length);
            CollectionsRegistry.Add(state, in ent, in this.data.arrPtr);

        }

        /// <summary>
        /// Initializes <c>MemArrayAuto</c> from the supplied ent, arr.
        /// </summary>
        public MemArrayAuto(in Ent ent, in ME.BECS.Internal.Array<T> arr) {

            if (arr.Length == 0u) {
                this = MemArrayAuto<T>.Empty;
                return;
            }

            this = default;
            var size = TSize<T>.size;
            this.data.Length = arr.Length;
            if (this.IsInlined == true) {
                // we can inline the data into array without allocating any memory
                this.data.ent = ent;
                this.data.arrPtr = default;
                _memcpy(arr.ptr, this.GetUnsafePtr(), this.Length * size);
                return;
            }
            
            var state = ent.World.state;
            this.data.ent = ent;
            #if USE_CACHE_PTR
            this.data.cachedPtr = default;
            #endif
            this.data.arrPtr = state.ptr->allocator.AllocArray<T>(arr.Length, out var ptr);
            _memcpy(arr.ptr, ptr, this.Length * size);
            CollectionsRegistry.Add(state, in ent, in this.data.arrPtr);
            
        }

        /// <summary>
        /// Exposes the value through the requested typed view.
        /// </summary>
        [INLINE(256)]
        public readonly ref U As<U>(uint index) where U : unmanaged {
            E.RANGE(index, 0, this.Length);
            return ref this.data.ent.World.state.ptr->allocator.RefArray<U>(this.data.arrPtr, index);
        }
        
        /// <summary>
        /// Disposes the current storage and copies the other collection handle; the two values then refer to the same allocation.
        /// </summary>
        [INLINE(256)]
        public void ReplaceWith(in MemArrayAuto<T> other) {
            
            if (other.data.arrPtr == this.data.arrPtr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        /// <summary>
        /// Releases the resources owned by this mem array auto instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {

            E.IS_ALIVE(this.data.ent);
            if (this.IsInlined == true) {
                this = default;
                return;
            }
            var state = this.data.ent.World.state;
            CollectionsRegistry.Remove(state, in this.data.ent, in this.data.arrPtr);
            if (this.data.arrPtr.IsValid() == true) {
                state.ptr->allocator.Free(this.data.arrPtr);
            }
            this = default;

        }

        /// <summary>
        /// Schedules release of the owned storage after the supplied dependency and returns the disposal handle.
        /// </summary>
        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

            E.IS_CREATED(this);
            
            if (this.IsInlined == true) {
                this = default;
                return inputDeps;
            }
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.data.arrPtr,
                ent = this.data.ent,
                worldId = this.data.ent.World.id,
            }.Schedule(inputDeps);
            
            this = default;

            return jobHandle;

        }

        /// <summary>
        /// Updates cached native access for the requested Burst execution mode.
        /// </summary>
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

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr() {

            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr;
            }

            return this.data.ent.World.state.ptr->allocator.GetUnsafePtr(this.data.arrPtr);

        }

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {

            if (this.IsInlined == true) {
                ref var r = ref Unsafe.AsRef(in this);
                var ptr = (byte*)Unsafe.AsPointer(ref r.data);
                return (safe_ptr)ptr;
            }

            return allocator.GetUnsafePtr(this.data.arrPtr);

        }

        /// <summary>
        /// Returns unsafe ptr cached.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtrCached(in MemoryAllocator allocator) {

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

        /// <summary>
        /// Returns unsafe ptr cached.
        /// </summary>
        [INLINE(256)]
        public readonly safe_ptr GetUnsafePtrCached() {

            #if USE_CACHE_PTR
            return CachedPtr.ReadPtr(in this.data.cachedPtr, in this.data.ent.World.state.ptr->allocator, this.data.arrPtr);
            #else
            return this.GetUnsafePtr();
            #endif

        }

        /// <summary>
        /// Returns alloc ptr.
        /// </summary>
        [INLINE(256)]
        public readonly MemPtr GetAllocPtr(uint index) {

            if (this.IsInlined == true) return default;
            return this.data.ent.World.state.ptr->allocator.RefArrayPtr<T>(this.data.arrPtr, index);
            
        }

        /// <summary>
        /// Reads the requested value from mem array auto.
        /// </summary>
        [INLINE(256)]
        public readonly ref T Read(in MemoryAllocator allocator, uint index) {
            
            E.RANGE(index, 0, this.Length);
            #if USE_CACHE_PTR
            return ref CachedPtr.Read<T>(this.data.cachedPtr, in allocator, this.data.arrPtr, index);
            #else
            return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            #endif
            
        }
        
        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in this.data.ent.World.state.ptr->allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in this.data.ent.World.state.ptr->allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[safe_ptr<State> state, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in state.ptr->allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[in MemoryAllocator allocator, int index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[in MemoryAllocator allocator, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Provides writable reference access to the requested entry.
        /// </summary>
        public readonly ref T this[safe_ptr<State> state, uint index] {
            [INLINE(256)]
            get {
                E.RANGE(index, 0, this.Length);
                return ref *((safe_ptr<T>)this.GetUnsafePtrCached(in state.ptr->allocator) + index).ptr;
            }
        }

        /// <summary>
        /// Grows storage using the requested length and growth factor; returns false when the requested length does not exceed the current length.
        /// </summary>
        [INLINE(256)]
        public bool Resize(uint newLength, ushort growFactor, ClearOptions options = ClearOptions.ClearMemory) {

            E.IS_CREATED(this);
            
            if (newLength <= this.Length) {

                return false;
                
            }

            newLength *= growFactor;

            var state = this.data.ent.World.state;
            if (this.IsInlined == true) {
                if (TSize<T>.size * newLength > MemPtr.SIZE) {
                    var arrPtr = state.ptr->allocator.AllocArray(newLength, out safe_ptr<T> ptr);
                    #if USE_CACHE_PTR
                    this.data.cachedPtr = new CachedPtr(in state.ptr->allocator, ptr);
                    #endif
                    _memcpy(this.GetUnsafePtr(), state.ptr->allocator.GetUnsafePtr(arrPtr), this.Length * TSize<T>.size);
                    var oldLength = this.Length;
                    this.data.arrPtr = arrPtr;
                    this.data.Length = newLength;
                    if (options == ClearOptions.ClearMemory) {
                        var size = TSize<T>.size;
                        _memclear(this.GetUnsafePtr() + oldLength * size, (newLength - oldLength) * size);
                    }
                    CollectionsRegistry.Add(state, in this.data.ent, in this.data.arrPtr);
                } else {
                    var size = TSize<T>.size;
                    _memclear(this.GetUnsafePtr() + this.Length * size, (newLength - this.Length) * size);
                }
            } else {
                CollectionsRegistry.Remove(state, in this.data.ent, in this.data.arrPtr);
                this.data.arrPtr = state.ptr->allocator.ReAllocArray(this.data.arrPtr, newLength, out safe_ptr<T> ptr);
                #if USE_CACHE_PTR
                this.data.cachedPtr = new CachedPtr(in state.ptr->allocator, ptr);
                #endif
                if (options == ClearOptions.ClearMemory) {
                    var size = TSize<T>.size;
                    _memclear(this.GetUnsafePtr() + this.Length * size, (newLength - this.Length) * size);
                }
                CollectionsRegistry.Add(state, in this.data.ent, in this.data.arrPtr);
            }
            this.data.Length = newLength;

            return true;

        }

        /// <summary>
        /// Zeroes the addressed elements while preserving the array length and backing allocation.
        /// </summary>
        [INLINE(256)]
        public readonly void Clear() {

            this.Clear(0u, this.Length);

        }

        /// <summary>
        /// Zeroes the addressed elements while preserving the array length and backing allocation.
        /// </summary>
        [INLINE(256)]
        public readonly void Clear(uint index, uint length) {

            if (length == 0u) return;
            
            E.IS_CREATED(this);
            E.RANGE(index, 0u, this.Length);
            E.RANGE(length - 1u, 0u, this.Length);

            var size = TSize<T>.size;
            if (this.IsInlined == true) {
                _memclear(this.GetUnsafePtr() + index * size, length * size);
            } else {
                this.data.ent.World.state.ptr->allocator.MemClear(this.data.arrPtr, index * size, length * size);
            }

        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        [INLINE(256)]
        public readonly bool Contains<U>(U obj) where U : unmanaged, System.IEquatable<T> {
            
            E.IS_CREATED(this);
            var ptr = (safe_ptr<T>)this.GetUnsafePtrCached(in this.data.ent.World.state.ptr->allocator);
            for (uint i = 0, cnt = this.Length; i < cnt; ++i) {

                if (obj.Equals(*(ptr + i).ptr) == true) {

                    return true;

                }
                
            }

            return false;

        }

        /// <summary>
        /// Copies the supplied source state into this mem array auto instance.
        /// </summary>
        [INLINE(256)]
        public void CopyFrom(in MemArrayAuto<T> other) {

            if (other.data.arrPtr == this.data.arrPtr) return;
            if (this.IsCreated == false && other.IsCreated == false) return;
            if (this.IsCreated == true && other.IsCreated == false) {
                this.Dispose();
                return;
            }
            if (this.IsCreated == false) this = new MemArrayAuto<T>(in other.data.ent, other.Length);
            
            NativeArrayUtils.Copy(in other, ref this);
            
        }
        
        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {

            return this.Length * TSize<T>.size;

        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(MemArrayAuto<T> other) {
            return this.data.Equals(other.data);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is MemArrayAuto<T> other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return this.data.GetHashCode();
        }

    }

}