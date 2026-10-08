namespace ME.BECS {

    internal sealed unsafe class UnsafeBitArrayDebugView {

        private MemBitArray Data;

        public UnsafeBitArrayDebugView(MemBitArray data) {
            this.Data = data;
        }

        public bool[] Bits {
            get {
                var allocator = Context.world.state.ptr->allocator;
                var array = new bool[this.Data.Length];
                for (var i = 0; i < this.Data.Length; ++i) {
                    array[i] = this.Data.IsSet(in allocator, i);
                }

                return array;
            }
        }

    }

    /// <summary>
    /// Exposes mem array data for debugger inspection.
    /// </summary>
    public unsafe class MemArrayProxy<T> where T : unmanaged {

        private MemArray<T> arr;
        
        /// <summary>
        /// Initializes <c>MemArrayProxy</c> from the supplied arr.
        /// </summary>
        public MemArrayProxy(MemArray<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                var world = Context.world;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[world.state.ptr->allocator, i];
                }

                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes mem array auto data for debugger inspection.
    /// </summary>
    public unsafe class MemArrayAutoProxy<T> where T : unmanaged {

        private MemArrayAuto<T> arr;
        
        /// <summary>
        /// Initializes <c>MemArrayAutoProxy</c> from the supplied arr.
        /// </summary>
        public MemArrayAutoProxy(MemArrayAuto<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.IsCreated == false) return null;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[i];
                }

                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes mem array thread cache line data for debugger inspection.
    /// </summary>
    public unsafe class MemArrayThreadCacheLineProxy<T> where T : unmanaged {

        private MemArrayThreadCacheLine<T> arr;
        
        /// <summary>
        /// Initializes <c>MemArrayThreadCacheLineProxy</c> from the supplied arr.
        /// </summary>
        public MemArrayThreadCacheLineProxy(MemArrayThreadCacheLine<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.IsCreated == false) return null;
                var world = Context.world;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[world.state.ptr->allocator, i];
                }

                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes list data for debugger inspection.
    /// </summary>
    public unsafe class ListProxy<T> where T : unmanaged {

        private List<T> arr;
        
        /// <summary>
        /// Initializes <c>ListProxy</c> from the supplied arr.
        /// </summary>
        public ListProxy(List<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity {
            get {
                return this.arr.Capacity;
            }
        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.IsCreated == false) return null;
                var world = Context.world;
                var arr = new T[this.arr.Count];
                for (uint i = 0; i < this.arr.Count; ++i) {
                    arr[i] = this.arr[world.state.ptr->allocator, i];
                }

                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes list auto data for debugger inspection.
    /// </summary>
    public unsafe class ListAutoProxy<T> where T : unmanaged {

        private ListAuto<T> arr;
        
        /// <summary>
        /// Initializes <c>ListAutoProxy</c> from the supplied arr.
        /// </summary>
        public ListAutoProxy(ListAuto<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity {
            get {
                return this.arr.Capacity;
            }
        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.IsCreated == false) return null;
                var arr = new T[this.arr.Count];
                for (uint i = 0; i < this.arr.Count; ++i) {
                    arr[i] = this.arr[i];
                }

                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes queue data for debugger inspection.
    /// </summary>
    public class QueueProxy<T> where T : unmanaged {

        private Queue<T> arr;
        
        /// <summary>
        /// Initializes <c>QueueProxy</c> from the supplied arr.
        /// </summary>
        public QueueProxy(Queue<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.isCreated == false) return null;
                var arr = new T[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = e.Current;
                }
                e.Dispose();
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes stack data for debugger inspection.
    /// </summary>
    public class StackProxy<T> where T : unmanaged {

        private Stack<T> arr;
        
        /// <summary>
        /// Initializes <c>StackProxy</c> from the supplied arr.
        /// </summary>
        public StackProxy(Stack<T> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public T[] items {
            get {
                if (this.arr.isCreated == false) return null;
                var world = Context.world;
                var arr = new T[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(world);
                while (e.MoveNext() == true) {
                    arr[i++] = e.Current;
                }
                e.Dispose();
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes equatable dictionary data for debugger inspection.
    /// </summary>
    public class EquatableDictionaryProxy<K, V> where K : unmanaged, System.IEquatable<K> where V : unmanaged {

        private EquatableDictionary<K, V> arr;
        
        /// <summary>
        /// Initializes <c>EquatableDictionaryProxy</c> from the supplied arr.
        /// </summary>
        public EquatableDictionaryProxy(EquatableDictionary<K, V> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArray<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets entries; this implementation returns <c>this.arr.entries</c>.
        /// </summary>
        public MemArray<EquatableDictionary<K, V>.Entry> entries => this.arr.entries;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.arr.freeCount</c>.
        /// </summary>
        public uint freeCount => this.arr.freeCount;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public System.Collections.Generic.KeyValuePair<K, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<K, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<K, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes u int dictionary data for debugger inspection.
    /// </summary>
    public class UIntDictionaryProxy<V> where V : unmanaged {

        private UIntDictionary<V> arr;
        private World world;
        
        /// <summary>
        /// Initializes <c>UIntDictionaryProxy</c> from the supplied arr.
        /// </summary>
        public UIntDictionaryProxy(UIntDictionary<V> arr) {

            this.arr = arr;
            this.world = Context.world;

        }

        /// <summary>
        /// Initializes <c>UIntDictionaryProxy</c> from the supplied arr, world.
        /// </summary>
        public UIntDictionaryProxy(UIntDictionary<V> arr, World world) {

            this.arr = arr;
            this.world = world;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArray<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets entries; this implementation returns <c>this.arr.entries</c>.
        /// </summary>
        public MemArray<UIntDictionary<V>.Entry> entries => this.arr.entries;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.arr.freeCount</c>.
        /// </summary>
        public uint freeCount => this.arr.freeCount;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public System.Collections.Generic.KeyValuePair<uint, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<uint, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(this.world);
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<uint, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes u long dictionary data for debugger inspection.
    /// </summary>
    public class ULongDictionaryProxy<V> where V : unmanaged {

        private ULongDictionary<V> arr;
        
        /// <summary>
        /// Initializes <c>ULongDictionaryProxy</c> from the supplied arr.
        /// </summary>
        public ULongDictionaryProxy(ULongDictionary<V> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArray<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets entries; this implementation returns <c>this.arr.entries</c>.
        /// </summary>
        public MemArray<ULongDictionary<V>.Entry> entries => this.arr.entries;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.arr.freeCount</c>.
        /// </summary>
        public uint freeCount => this.arr.freeCount;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public System.Collections.Generic.KeyValuePair<ulong, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<ulong, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<ulong, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes u long dictionary auto data for debugger inspection.
    /// </summary>
    public class ULongDictionaryAutoProxy<V> where V : unmanaged {

        private ULongDictionaryAuto<V> arr;
        
        /// <summary>
        /// Initializes <c>ULongDictionaryAutoProxy</c> from the supplied arr.
        /// </summary>
        public ULongDictionaryAutoProxy(ULongDictionaryAuto<V> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArrayAuto<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets entries; this implementation returns <c>this.arr.entries</c>.
        /// </summary>
        public MemArrayAuto<ULongDictionaryAuto<V>.Entry> entries => this.arr.entries;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.arr.freeCount</c>.
        /// </summary>
        public uint freeCount => this.arr.freeCount;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public System.Collections.Generic.KeyValuePair<ulong, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<ulong, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator();
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<ulong, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }
    
    /// <summary>
    /// Exposes u int dictionary auto data for debugger inspection.
    /// </summary>
    public class UIntDictionaryAutoProxy<V> where V : unmanaged {

        private UIntDictionaryAuto<V> arr;
        
        /// <summary>
        /// Initializes <c>UIntDictionaryAutoProxy</c> from the supplied arr.
        /// </summary>
        public UIntDictionaryAutoProxy(UIntDictionaryAuto<V> arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArrayAuto<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets entries; this implementation returns <c>this.arr.entries</c>.
        /// </summary>
        public MemArrayAuto<UIntDictionaryAuto<V>.Entry> entries => this.arr.entries;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.arr.freeCount</c>.
        /// </summary>
        public uint freeCount => this.arr.freeCount;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public System.Collections.Generic.KeyValuePair<uint, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<uint, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator();
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<uint, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }

    /*
    public class HashSetProxy<T> where T : unmanaged {

        private HashSet<T> arr;
        
        public HashSetProxy(HashSet<T> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                if (StaticAllocatorProxy.allocator.isValid == false) return 0;
                return this.arr.Count;
            }
        }
        
        public MemArray<uint> buckets => this.arr.buckets;
        public MemArray<HashSet<T>.Slot> slots => this.arr.slots;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint lastIndex => this.arr.lastIndex;

        public T[] items {
            get {
                if (StaticAllocatorProxy.allocator.isValid == false) return null;
                var arr = new T[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator();
                while (e.MoveNext() == true) {
                    arr[i++] = e.Current;
                }
                e.Dispose();
                
                return arr;
            }
        }

    }
    */

    /// <summary>
    /// Exposes u int hash set data for debugger inspection.
    /// </summary>
    public class UIntHashSetProxy {

        private UIntHashSet arr;
        
        /// <summary>
        /// Initializes <c>UIntHashSetProxy</c> from the supplied arr.
        /// </summary>
        public UIntHashSetProxy(UIntHashSet arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }
        
        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArray<int> buckets => this.arr.buckets;
        /// <summary>
        /// Gets slots; this implementation returns <c>this.arr.slots</c>.
        /// </summary>
        public MemArray<UIntHashSet.Slot> slots => this.arr.slots;
        /// <summary>
        /// Gets hash; this implementation returns <c>this.arr.hash</c>.
        /// </summary>
        public uint hash => this.arr.hash;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => (uint)this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => (uint)this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Last index used to locate the associated entry.
        /// </summary>
        public uint lastIndex => (uint)this.arr.lastIndex;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public uint[] items {
            get {
                var arr = new uint[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = e.Current;
                }
                
                return arr;
            }
        }

    }

    /// <summary>
    /// Exposes u int pair hash set data for debugger inspection.
    /// </summary>
    public class UIntPairHashSetProxy {

        private UIntPairHashSet arr;
        
        /// <summary>
        /// Initializes <c>UIntPairHashSetProxy</c> from the supplied arr.
        /// </summary>
        public UIntPairHashSetProxy(UIntPairHashSet arr) {

            this.arr = arr;

        }

        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count {
            get {
                return this.arr.Count;
            }
        }
        
        /// <summary>
        /// Gets buckets; this implementation returns <c>this.arr.buckets</c>.
        /// </summary>
        public MemArray<uint> buckets => this.arr.buckets;
        /// <summary>
        /// Gets slots; this implementation returns <c>this.arr.slots</c>.
        /// </summary>
        public MemArray<UIntPairHashSet.Slot> slots => this.arr.slots;
        /// <summary>
        /// Gets hash; this implementation returns <c>this.arr.hash</c>.
        /// </summary>
        public uint hash => this.arr.hash;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count => this.arr.count;
        /// <summary>
        /// Change version used to detect stale state.
        /// </summary>
        public uint version => this.arr.version;
        /// <summary>
        /// Gets free list; this implementation returns <c>this.arr.freeList</c>.
        /// </summary>
        public int freeList => this.arr.freeList;
        /// <summary>
        /// Gets last index; this implementation returns <c>this.arr.lastIndex</c>.
        /// </summary>
        public uint lastIndex => this.arr.lastIndex;

        /// <summary>
        /// Entries stored by this container.
        /// </summary>
        public UIntPair[] items {
            get {
                var arr = new UIntPair[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = e.Current;
                }
                
                return arr;
            }
        }

    }

}