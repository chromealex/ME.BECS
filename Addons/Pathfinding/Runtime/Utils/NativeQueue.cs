namespace ME.BECS.Pathfinding {

    /// <summary>
    /// Stores queue entries in native memory.
    /// </summary>
    public struct NativeQueue<T> where T : unmanaged {

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public int Count;
        /// <summary>
        /// Backing array used by this value.
        /// </summary>
        public Unity.Collections.NativeList<T> arr;
        /// <summary>
        /// Head used by <c>NativeQueue</c>.
        /// </summary>
        public int head;
        /// <summary>
        /// Last used by <c>NativeQueue</c>.
        /// </summary>
        public int last;
            
        /// <summary>
        /// Initializes <c>NativeQueue</c> from the supplied size, allocator.
        /// </summary>
        public NativeQueue(int size, Unity.Collections.Allocator allocator) {
                
            this.arr = new Unity.Collections.NativeList<T>(size, allocator);
            this.Count = 0;
            this.head = -1;
            this.last = -1;

        }

        /// <summary>
        /// Clears the current native queue contents.
        /// </summary>
        public void Clear() {
            
            this.Count = 0;
            this.head = -1;
            this.last = -1;
            
        }

        /// <summary>
        /// Removes and returns the entry at the head of the queue.
        /// </summary>
        public T Dequeue() {
                
            var data = this.arr[this.head];
            --this.Count;
            ++this.head;
            if (this.head > this.last) {
                    
                this.head = -1;
                this.last = -1;

            }
            return data;
                
        }

        /// <summary>
        /// Adds an entry at the tail of the queue.
        /// </summary>
        public void Enqueue(T data) {

            ++this.last;
            if (this.last >= this.arr.Length) this.arr.Add(default);
            this.arr[this.last] = data;
            if (this.head == -1) this.head = this.last;
            ++this.Count;

        }

        /// <summary>
        /// Releases the resources owned by this native queue instance.
        /// </summary>
        public void Dispose() {

            this.arr.Dispose();
            this.Count = default;
            this.last = default;
            this.head = default;

        }

    }

}