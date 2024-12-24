namespace ME.BECS {

    using MemPtr = System.Int64;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(StackProxy<>))]
    public unsafe struct Stack<T> where T : unmanaged {

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

            public void Dispose() {
                this.index = -1;
            }

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
        public bool isCreated => this.array.IsCreated;

        public readonly uint Count => this.size;

        [INLINE(256)]
        public Stack(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
        }

        public Enumerator GetEnumerator(World world) {
            return new Enumerator(this, world.state);
        }

        [INLINE(256)]
        public safe_ptr GetUnsafePtr(in MemoryAllocator allocator) {
            return this.array.GetUnsafePtr(in allocator);
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.array.BurstMode(in allocator, state);
        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            
            this.array.Dispose(ref allocator);
            this = default;
            
        }

        [INLINE(256)]
        public void Clear() {
            this.size = 0;
        }

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

        [INLINE(256)]
        public readonly T Peek(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            return this.array[in allocator, this.size - 1];
        }

        [INLINE(256)]
        public T Pop(in MemoryAllocator allocator) {
            E.IS_EMPTY(this.size);

            var item = this.array[in allocator, --this.size];
            this.array[in allocator, this.size] = default;
            return item;
        }

        [INLINE(256)]
        public void Push(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                this.array.Resize(ref allocator, this.array.Length == 0 ? Stack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
            }

            this.array[in allocator, this.size++] = item;
        }

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

        [INLINE(256)]
        public void PushNoChecks(T item, T* ptr) {
            *ptr = item;
            ++this.size;
        }

        public uint GetReservedSizeInBytes() {
            return this.array.GetReservedSizeInBytes();
        }

    }

}