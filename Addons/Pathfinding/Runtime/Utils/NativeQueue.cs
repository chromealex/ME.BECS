namespace ME.BECS.Pathfinding {

    public struct NativeQueue<T> where T : unmanaged {

        public int Count;
        public Unity.Collections.NativeList<T> arr;
        public int head;
        public int last;
            
        public NativeQueue(int size, Unity.Collections.Allocator allocator) {
                
            this.arr = new Unity.Collections.NativeList<T>(size, allocator);
            this.Count = 0;
            this.head = -1;
            this.last = -1;

        }

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

        public void Enqueue(T data) {

            ++this.last;
            if (this.last >= this.arr.Length) this.arr.Add(default);
            this.arr[this.last] = data;
            if (this.head == -1) this.head = this.last;
            ++this.Count;

        }

        public void Dispose() {

            this.arr.Dispose();
            this.Count = default;
            this.last = default;
            this.head = default;

        }

    }

}