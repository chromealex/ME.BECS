namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides job thread stack storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct JobThreadStack<T> : IIsCreated where T : unmanaged {

        private const uint DEFAULT_CAPACITY = 4u;

        private MemArray<T> array;
        private List<uint> toRemove;
        //private BitArray bits;
        private uint size;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.array.IsCreated;

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public readonly uint Count => this.size;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.array);
            writer.Write(this.toRemove);
            writer.Write(this.size);
        }

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.array);
            reader.Read(ref this.toRemove);
            reader.Read(ref this.size);
        }

        /// <summary>
        /// Initializes <c>JobThreadStack</c> with storage for the requested number of elements.
        /// </summary>
        [INLINE(256)]
        public JobThreadStack(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
            this.toRemove = new List<uint>(ref allocator, capacity);
            //this.bits = new BitArray(ref allocator, capacity);
        }

        /// <summary>
        /// Applies the supplied data or pending changes to the target state.
        /// </summary>
        [INLINE(256)]
        public void Apply(in MemoryAllocator allocator) {
            E.THREAD_CHECK("Apply");
            if (this.toRemove.Count == 0u) return;
            for (uint i = 0u; i < this.toRemove.Count; ++i) {
                var idx = this.toRemove[in allocator, i];
                var last = this.array[in allocator, --this.size];
                this.array[in allocator, idx] = last;
            }

            this.toRemove.Clear();
            //this.bits.Clear(in allocator);
        }

        /// <summary>
        /// Returns a borrowed pointer to collection storage; mutation that reallocates storage or disposal invalidates it.
        /// </summary>
        [INLINE(256)]
        public safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {
            return this.array.GetUnsafePtr(in allocator);
        }

        /// <summary>
        /// Updates cached native access for the requested Burst execution mode.
        /// </summary>
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.array.BurstMode(in allocator, state);
        }

        /// <summary>
        /// Releases the resources owned by this job thread stack instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            
            this.array.Dispose(ref allocator);
            this = default;
            
        }

        /// <summary>
        /// Removes and returns the next entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public T Pop(ref MemoryAllocator allocator, in JobInfo jobInfo) {
            E.IS_EMPTY(this.size);
            if (jobInfo.IsCreated == false) {
                var idx = --this.size;
                var item = this.array[in allocator, idx];
                this.array[in allocator, idx] = default;
                return item;
            }

            {
                var idx = this.size - 1u - jobInfo.GetOffset(0u);
                E.RANGE(idx, 0u, this.size);
                /*while (true) {
                    if (this.toRemove.Contains(in allocator, idx) == true) {
                        --idx;
                        continue;
                    }
                    break;
                }*/

                var item = this.array[in allocator, idx];
                this.array[in allocator, idx] = default;
                this.toRemove.Add(ref allocator, idx);
                jobInfo.IncrementLocalCounter(0u);
                //this.bits.Set(in allocator, (int)idx, true);
                return item;
            }
        }

        /// <summary>
        /// Adds an entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public void Push(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                this.array.Resize(ref allocator, this.array.Length == 0 ? JobThreadStack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
                //this.bits.Resize(ref allocator, this.array.Length);
            }

            this.array[in allocator, this.size++] = item;
        }

        /// <summary>
        /// Appends the supplied range to the container.
        /// </summary>
        [INLINE(256)]
        public void PushRange(ref MemoryAllocator allocator, List<T> list) {
            var freeItems = this.array.Length - this.size;
            if (list.Count >= freeItems) {
                var delta = list.Count - freeItems;
                this.array.Resize(ref allocator, this.array.Length + delta, growFactor: 1);
                //this.bits.Resize(ref allocator, this.array.Length);
            }

            _memcpy(list.GetUnsafePtr(in allocator), (safe_ptr<byte>)this.array.GetUnsafePtr(in allocator) + TSize<T>.size * this.size, TSize<T>.size * list.Count);
            this.size += list.Count;
            /*for (uint i = 0; i < list.Count; ++i) {
                this.Push(ref allocator, list[allocator, i]);
            }*/

        }

        /// <summary>
        /// Pushes an entry without performing the usual validation checks.
        /// </summary>
        [INLINE(256)]
        public void PushNoChecks(T item, T* ptr) {
            *ptr = item;
            ++this.size;
        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
        public uint GetReservedSizeInBytes() {
            return this.array.GetReservedSizeInBytes();
        }

    }

}