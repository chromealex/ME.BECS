namespace ME.BECS {

    using MemPtr = System.Int64;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides stack storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(StackProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct Stack<T> where T : unmanaged {

        /// <summary>
        /// Traverses the entries exposed by <c>Stack</c>.
        /// </summary>
        public struct Enumerator : System.Collections.Generic.IEnumerator<T> {

            private readonly Stack<T> stack;
            private readonly safe_ptr<State> state;
            private int index;
            private T currentElement;

            internal Enumerator(Stack<T> stack, safe_ptr<State> state) {
                this.stack = stack;
                this.state = state;
                this.index = -2;
                this.currentElement = default(T);
            }

            /// <summary>
            /// Releases the resources owned by this enumerator instance.
            /// </summary>
            public void Dispose() {
                this.index = -1;
            }

            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            public bool MoveNext() {
                bool retval;
                if (this.index == -2) { // First call to enumerator.
                    this.index = (int)this.stack.size - 1;
                    retval = this.index >= 0;
                    if (retval) {
                        this.currentElement = this.stack.array[in this.state.ptr->allocator, this.index];
                    }

                    return retval;
                }

                if (this.index == -1) { // End of enumeration.
                    return false;
                }

                retval = --this.index >= 0;
                if (retval) {
                    this.currentElement = this.stack.array[in this.state.ptr->allocator, this.index];
                } else {
                    this.currentElement = default(T);
                }

                return retval;
            }

            /// <summary>
            /// Element at the enumerator's current position.
            /// </summary>
            public T Current {
                get {
                    return this.currentElement;
                }
            }

            object System.Collections.IEnumerator.Current {
                get {
                    return this.currentElement;
                }
            }

            void System.Collections.IEnumerator.Reset() {
                this.index = -2;
                this.currentElement = default;
            }

        }
        
        private const uint DEFAULT_CAPACITY = 4u;

        private MemArray<T> array;
        private uint size;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool isCreated => this.array.IsCreated;

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public readonly uint Count => this.size;

        /// <summary>
        /// Initializes <c>Stack</c> with storage for the requested number of elements.
        /// </summary>
        [INLINE(256)]
        public Stack(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
        }

        /// <summary>
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        public Enumerator GetEnumerator(World world) {
            return new Enumerator(this, world.state);
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
        /// Releases the resources owned by this stack instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            
            this.array.Dispose(ref allocator);
            this = default;
            
        }

        /// <summary>
        /// Removes stored entries while retaining the backing allocation for reuse.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            this.size = 0;
        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        [INLINE(256)]
        public bool Contains<U>(in MemoryAllocator allocator, U item) where U : System.IEquatable<T> {

            var count = this.size;
            while (count-- > 0) {
                if (item.Equals(this.array[in allocator, count])) {
                    return true;
                }
            }

            return false;

        }

        /// <summary>
        /// Returns the next entry without removing it.
        /// </summary>
        [INLINE(256)]
        public readonly T Peek(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            return this.array[in allocator, this.size - 1];
        }

        /// <summary>
        /// Removes and returns the next entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public T Pop(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            var item = this.array[in allocator, --this.size];
            this.array[in allocator, this.size] = default;
            return item;
        }

        /// <summary>
        /// Adds an entry according to this container's ordering.
        /// </summary>
        [INLINE(256)]
        public void Push(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                this.array.Resize(ref allocator, this.array.Length == 0 ? Stack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
            }

            this.array[in allocator, this.size++] = item;
        }

        /// <summary>
        /// Pushes an entry while holding the container's synchronization lock.
        /// </summary>
        [INLINE(256)]
        public void PushLock(ref LockSpinner spinner, ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                spinner.Lock();
                if (this.size == this.array.Length) {
                    this.array.Resize(ref allocator, this.array.Length == 0 ? Stack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
                }
                spinner.Unlock();
            }

            var idx = JobUtils.Increment(ref this.size);
            this.array[in allocator, idx - 1u] = item;
        }

        /// <summary>
        /// Appends the supplied range to the container.
        /// </summary>
        [INLINE(256)]
        public void PushRange(ref MemoryAllocator allocator, List<uint> list) {
            var freeItems = this.array.Length - this.size;
            if (list.Count >= freeItems) {
                var delta = list.Count - freeItems;
                this.array.Resize(ref allocator, this.array.Length + delta, growFactor: 1);
            }

            _memcpy(list.GetUnsafePtr(in allocator), (safe_ptr<byte>)this.array.GetUnsafePtr(in allocator) + TSize<uint>.size * this.size, TSize<uint>.size * list.Count);
            this.size += list.Count;

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