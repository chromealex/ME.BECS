namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    using Unity.Collections;
    using static Cuts;

    public unsafe struct UnsafeQueue<T> : IIsCreated where T : unmanaged {

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

            [INLINE(256)]
            public void Dispose() {
                this.index = -2;
                this.currentElement = default(T);
            }

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
        public readonly bool IsCreated => this.array.ptr != null;

        public readonly uint Count => this.size;
        public readonly uint Capacity => (uint)this.capacity;

        public UnsafeQueue(Allocator allocator) : this(4u, allocator) { }

        public UnsafeQueue(uint capacity, Allocator allocator) {
            this = default;
            this.allocator = allocator;
            this.capacity = capacity > 0u ? capacity : 4u;
            this.array = _makeArray<T>(capacity, allocator);
        }

        [INLINE(256)]
        public void Dispose() {
            E.IS_CREATED(this);
            _free(this.array, this.allocator);
            this = default;
        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {
            E.IS_CREATED(this);
            return new Enumerator(this);
        }

        [INLINE(256)]
        public void Clear() {
            E.IS_CREATED(this);
            this.head = 0;
            this.tail = 0;
            this.size = 0;
            this.version++;
        }

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

        [INLINE(256)]
        public T Peek() {
            E.IS_CREATED(this);
            E.IS_EMPTY(this.size);
            return this.array[this.head];
        }

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
            _resizeArray(this.allocator, ref this.array, ref this.capacity, capacity);
            this.head = 0;
            this.tail = this.size == capacity ? 0 : this.size;
            this.version++;
        }

    }

}