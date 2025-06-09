namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public unsafe struct JobThreadStack<T> : IIsCreated where T : unmanaged {

        private const uint DEFAULT_CAPACITY = 4u;

        private MemArray<T> array;
        private List<uint> toRemove;
        //private BitArray bits;
        private uint size;
        public bool IsCreated => this.array.IsCreated;

        public readonly uint Count => this.size;

        [INLINE(256)]
        public JobThreadStack(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
            this.toRemove = new List<uint>(ref allocator, capacity);
            //this.bits = new BitArray(ref allocator, capacity);
        }

        [INLINE(256)]
        public void Apply(in MemoryAllocator allocator) {
            E.THREAD_CHECK("Apply");
            for (uint i = 0u; i < this.toRemove.Count; ++i) {
                var idx = this.toRemove[in allocator, i];
                var last = this.array[in allocator, --this.size];
                this.array[in allocator, idx] = last;
            }

            this.toRemove.Clear();
            //this.bits.Clear(in allocator);
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
        public T Pop(ref MemoryAllocator allocator, in JobInfo jobInfo) {
            E.IS_EMPTY(this.size);
            if (jobInfo.IsCreated == false) {
                var idx = --this.size;
                var item = this.array[in allocator, idx];
                this.array[in allocator, idx] = default;
                return item;
            }

            {
                var idx = this.size - 1u - jobInfo.Offset;
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
                jobInfo.IncrementLocalCounter();
                //this.bits.Set(in allocator, (int)idx, true);
                return item;
            }
        }

        [INLINE(256)]
        public void Push(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                this.array.Resize(ref allocator, this.array.Length == 0 ? JobThreadStack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
                //this.bits.Resize(ref allocator, this.array.Length);
            }

            this.array[in allocator, this.size++] = item;
        }

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