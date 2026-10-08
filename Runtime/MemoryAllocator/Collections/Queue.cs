namespace ME.BECS {

    using MemPtr = System.Int64;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides queue storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(QueueProxy<>))]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct Queue<T> where T : unmanaged {

        /// <summary>
        /// Traverses the entries exposed by <c>Queue</c>.
        /// </summary>
        public struct Enumerator : System.Collections.Generic.IEnumerator<T> {

            private safe_ptr<State> state;
            private Queue<T> q;
            private int index; // -1 = not started, -2 = ended/disposed
            private T currentElement;

            internal Enumerator(Queue<T> q, safe_ptr<State> state) {
                this.q = q;
                this.index = -1;
                this.currentElement = default(T);
                this.state = state;
            }

            /// <summary>
            /// Resets the enumerator position and current value without releasing the queue storage.
            /// </summary>
            public void Dispose() {
                this.index = -2;
                this.currentElement = default(T);
            }

            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            public bool MoveNext() {
                if (this.index == -2) {
                    return false;
                }

                this.index++;

                if (this.index == this.q.size) {
                    this.index = -2;
                    this.currentElement = default(T);
                    return false;
                }

                this.currentElement = this.q.GetElement(in this.state.ptr->allocator, (uint)this.index);
                return true;
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
                this.index = -1;
                this.currentElement = default(T);
            }

        }
        
        private const uint MINIMUM_GROW = 4;
        private const uint GROW_FACTOR = 200;

        private MemArray<T> array;
        private uint head;
        private uint tail;
        private uint size;
        private uint version;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool isCreated => this.array.IsCreated;

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public readonly uint Count => this.size;
        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public readonly uint Capacity => this.array.Length;

        /// <summary>
        /// Initializes <c>Queue</c> with storage for the requested number of elements.
        /// </summary>
        public Queue(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
        }

        /// <summary>
        /// Releases the resources owned by this queue instance.
        /// </summary>
        public void Dispose(ref MemoryAllocator allocator) {
            
            this.array.Dispose(ref allocator);
            this = default;
            
        }

        /// <summary>
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        public readonly Enumerator GetEnumerator(World world) {
            return new Enumerator(this, world.state);
        }

        /// <summary>
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        public readonly Enumerator GetEnumerator(safe_ptr<State> state) {
            return new Enumerator(this, state);
        }

        /// <summary>
        /// Removes stored entries while retaining the backing allocation for reuse.
        /// </summary>
        public void Clear() {
            this.head = 0;
            this.tail = 0;
            this.size = 0;
            this.version++;
        }

        /// <summary>
        /// Adds an entry at the tail of the queue.
        /// </summary>
        public void Enqueue(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                var newCapacity = (uint)((long)this.array.Length * (long)Queue<T>.GROW_FACTOR / 100);
                if (newCapacity < this.array.Length + Queue<T>.MINIMUM_GROW) {
                    newCapacity = this.array.Length + Queue<T>.MINIMUM_GROW;
                }

                this.SetCapacity(ref allocator, newCapacity);
            }

            this.array[in allocator, this.tail] = item;
            this.tail = (this.tail + 1) % this.array.Length;
            this.size++;
            this.version++;
        }

        /// <summary>
        /// Removes and returns the entry at the head of the queue.
        /// </summary>
        public T Dequeue(ref MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);
            
            var removed = this.array[in allocator, this.head];
            this.array[in allocator, this.head] = default(T);
            this.head = (this.head + 1) % this.array.Length;
            this.size--;
            this.version++;
            return removed;
        }

        /// <summary>
        /// Returns the next entry without removing it.
        /// </summary>
        public T Peek(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            return this.array[in allocator, this.head];
        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        public bool Contains<U>(in MemoryAllocator allocator, U item) where U : System.IEquatable<T> {
            var index = this.head;
            var count = this.size;

            while (count-- > 0) {
                if (item.Equals(this.array[in allocator, index])) {
                    return true;
                }

                index = (index + 1) % this.array.Length;
            }

            return false;
        }

        private T GetElement(in MemoryAllocator allocator, uint i) {
            return this.array[in allocator, (this.head + i) % this.array.Length];
        }

        private void SetCapacity(ref MemoryAllocator allocator, uint capacity) {
            // Enqueue grows a full buffer by at least its current capacity.
            var oldCapacity = this.array.Length;
            this.array.Resize(ref allocator, capacity, growFactor: 1);
            if (this.head > 0u) {
                var ptr = this.array.GetUnsafePtr(in allocator);
                Cuts._memcpy(ptr, ptr + oldCapacity * TSize<T>.size, this.head * TSize<T>.size);
            }
            this.tail = oldCapacity + this.head;
            this.version++;
        }

    }

}