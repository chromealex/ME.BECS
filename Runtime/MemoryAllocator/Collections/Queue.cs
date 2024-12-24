namespace ME.BECS {

    using MemPtr = System.Int64;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(QueueProxy<>))]
    public unsafe struct Queue<T> where T : unmanaged {

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

            public void Dispose() {
                this.index = -2;
                this.currentElement = default(T);
            }

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
        public readonly bool isCreated => this.array.IsCreated;

        public readonly uint Count => this.size;
        public readonly uint Capacity => this.array.Length;

        public Queue(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
        }

        public void Dispose(ref MemoryAllocator allocator) {
            
            this.array.Dispose(ref allocator);
            this = default;
            
        }

        public readonly Enumerator GetEnumerator(World world) {
            return new Enumerator(this, world.state);
        }

        public readonly Enumerator GetEnumerator(safe_ptr<State> state) {
            return new Enumerator(this, state);
        }

        public void Clear() {
            this.head = 0;
            this.tail = 0;
            this.size = 0;
            this.version++;
        }

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

        public T Dequeue(ref MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);
            
            var removed = this.array[in allocator, this.head];
            this.array[in allocator, this.head] = default(T);
            this.head = (this.head + 1) % this.array.Length;
            this.size--;
            this.version++;
            return removed;
        }

        public T Peek(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            return this.array[in allocator, this.head];
        }

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
            this.array.Resize(ref allocator, capacity, 2);
            this.head = 0;
            this.tail = this.size == capacity ? 0 : this.size;
            this.version++;
        }

    }

}