namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;

    public unsafe struct JobThreadStack<T> where T : unmanaged {

        private const uint DEFAULT_CAPACITY = 4u;

        private MemArray<T> array;
        //private BitArray bits;
        private volatile uint size;
        public bool isCreated => this.array.IsCreated;

        public readonly uint Count => this.size;

        [INLINE(256)]
        public JobThreadStack(ref MemoryAllocator allocator, uint capacity) {
            this = default;
            this.array = new MemArray<T>(ref allocator, capacity);
            //this.bits = new BitArray(ref allocator, capacity);
        }

        [INLINE(256)]
        public void Apply(ref MemoryAllocator allocator) {
            E.THREAD_CHECK("Apply");
            /*for (int i = (int)this.array.Length - 1; i >= 0; --i) {
                if (this.bits.IsSet(in allocator, i) == true) {
                    --this.size;
                    this.bits.Set(in allocator, i, false);
                }
            }*/
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
        public T Pop(in MemoryAllocator allocator, in JobInfo jobInfo) {
            /*var offset = jobInfo.Offset;
            while (true) {
                E.RANGE(offset, 0u, this.size);

                var idx = (int)offset;
                if (jobInfo.isCreated == true && this.bits.IsSet(in allocator, idx) == false) {
                    offset += jobInfo.itemsPerThread;
                    continue;
                }
                var item = this.array[in allocator, idx];
                this.array[in allocator, idx] = default;
                this.bits.Set(in allocator, idx, false);
                return item;
            }*/
            E.IS_EMPTY(this.size);
            var idx = --this.size;
            var item = this.array[in allocator, idx];
            this.array[in allocator, idx] = default;
            return item;
        }

        [INLINE(256)]
        public void Push(ref MemoryAllocator allocator, T item) {
            if (this.size == this.array.Length) {
                this.array.Resize(ref allocator, this.array.Length == 0 ? JobThreadStack<T>.DEFAULT_CAPACITY : 2 * this.array.Length, 2);
                //this.bits.Resize(ref allocator, this.bits.Length == 0 ? JobThreadStack<T>.DEFAULT_CAPACITY : 2 * this.bits.Length);
            }

            //this.bits.Set(in allocator, (int)this.size, true);
            this.array[in allocator, this.size++] = item;
        }

        [INLINE(256)]
        public void PushRange(ref MemoryAllocator allocator, List<T> list) {
            var freeItems = this.array.Length - this.size;
            if (list.Count >= freeItems) {
                var delta = list.Count - freeItems;
                this.array.Resize(ref allocator, this.array.Length + delta, growFactor: 1);
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