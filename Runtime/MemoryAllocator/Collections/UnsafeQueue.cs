namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    using Unity.Collections;
    using static Cuts;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides unsafe queue storage backed by native memory; value copies share the underlying allocation.
    /// </summary>
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public unsafe struct UnsafeQueue<T> : IIsCreated where T : unmanaged {

        /// <summary>
        /// Traverses the entries exposed by <c>UnsafeQueue</c>.
        /// </summary>
        public struct Enumerator : System.Collections.Generic.IEnumerator<T> {

            private UnsafeQueue<T> q;
            private int index; // -1 = not started, -2 = ended/disposed
            private T currentElement;

            [INLINE(256)]
            internal Enumerator(UnsafeQueue<T> q) {
                this.q = q;
                this.index = -1;
                this.currentElement = default(T);
            }

            /// <summary>
            /// Releases the resources owned by this enumerator instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                this.index = -2;
                this.currentElement = default(T);
            }

            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            [INLINE(256)]
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

                this.currentElement = this.q.GetElement((uint)this.index);
                return true;
            }

            /// <summary>
            /// Element at the enumerator's current position.
            /// </summary>
            public T Current {
                [INLINE(256)]
                get {
                    return this.currentElement;
                }
            }

            object System.Collections.IEnumerator.Current {
                [INLINE(256)]
                get {
                    return this.currentElement;
                }
            }

            [INLINE(256)]
            void System.Collections.IEnumerator.Reset() {
                this.index = -1;
                this.currentElement = default(T);
            }

        }
        
        private const uint MINIMUM_GROW = 4u;
        private const uint GROW_FACTOR = 200u;

        private safe_ptr<T> array;
        private uint head;
        private uint tail;
        private uint size;
        private uint capacity;
        private uint version;
        private readonly Unity.Collections.Allocator allocator;
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public readonly bool IsCreated => this.array.ptr != null;

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public readonly uint Count => this.size;
        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public readonly uint Capacity => (uint)this.capacity;

        /// <summary>
        /// Initializes <c>UnsafeQueue</c> from the supplied allocator.
        /// </summary>
        public UnsafeQueue(Allocator allocator) : this(4u, allocator) { }

        /// <summary>
        /// Initializes <c>UnsafeQueue</c> with storage for the requested number of elements.
        /// </summary>
        public UnsafeQueue(uint capacity, Allocator allocator) {
            this = default;
            this.allocator = allocator;
            this.capacity = capacity > 0u ? capacity : 4u;
            this.array = _makeArray<T>(capacity, allocator);
        }

        /// <summary>
        /// Releases the resources owned by this unsafe queue instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            E.IS_CREATED(this);
            _free(this.array, this.allocator);
            this = default;
        }

        /// <summary>
        /// Returns an enumerator over the current collection contents.
        /// </summary>
        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {
            E.IS_CREATED(this);
            return new Enumerator(this);
        }

        /// <summary>
        /// Removes stored entries while retaining the backing allocation for reuse.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            E.IS_CREATED(this);
            this.head = 0;
            this.tail = 0;
            this.size = 0;
            this.version++;
        }

        /// <summary>
        /// Adds an entry at the tail of the queue.
        /// </summary>
        [INLINE(256)]
        public void Enqueue(T item) {
            E.IS_CREATED(this);
            if (this.size == this.capacity) {
                var newCapacity = this.capacity * GROW_FACTOR / 100;
                if (newCapacity < this.capacity + MINIMUM_GROW) {
                    newCapacity = this.capacity + MINIMUM_GROW;
                }

                this.SetCapacity(newCapacity);
            }

            this.array[this.tail] = item;
            this.tail = (this.tail + 1) % this.capacity;
            this.size++;
            this.version++;
        }

        /// <summary>
        /// Removes and returns the entry at the head of the queue.
        /// </summary>
        [INLINE(256)]
        public T Dequeue() {
            E.IS_CREATED(this);
            E.IS_EMPTY(this.size);
            var removed = this.array[this.head];
            this.array[this.head] = default(T);
            this.head = (this.head + 1) % this.capacity;
            this.size--;
            this.version++;
            return removed;
        }

        /// <summary>
        /// Returns the next entry without removing it.
        /// </summary>
        [INLINE(256)]
        public T Peek() {
            E.IS_CREATED(this);
            E.IS_EMPTY(this.size);
            return this.array[this.head];
        }

        /// <summary>
        /// Tests whether the specified value is present.
        /// </summary>
        [INLINE(256)]
        public bool Contains<U>(U item) where U : System.IEquatable<T> {
            E.IS_CREATED(this);
            var index = this.head;
            var count = this.size;
            while (count-- > 0) {
                if (item.Equals(this.array[index])) {
                    return true;
                }
                index = (index + 1) % this.capacity;
            }
            return false;
        }

        [INLINE(256)]
        private T GetElement(uint i) {
            E.IS_CREATED(this);
            return this.array[(this.head + i) % this.capacity];
        }

        [INLINE(256)]
        private void SetCapacity(uint capacity) {
            E.IS_CREATED(this);
            // Enqueue grows a full buffer by at least its current capacity.
            var oldCapacity = this.capacity;
            _resizeArray(this.allocator, ref this.array, ref this.capacity, capacity);
            if (this.head > 0u) {
                _memcpy(this.array, this.array + oldCapacity, this.head * TSize<T>.size);
            }
            this.tail = oldCapacity + this.head;
            this.version++;
        }

    }

}