namespace ME.BECS {
    
    public unsafe class EntProxy {

        private Ent ent;
        
        public EntProxy(Ent ent) {

            this.ent = ent;

        }

        public bool alive => this.ent.IsAlive();
        public override string ToString() => this.ent.ToString();
        
        public object[] components {
            get {

                if (this.alive == false) return null;
                
                var world = this.ent.World;
                var methodHas = typeof(Components).GetMethod(nameof(Components.HasDirect));
                var methodRead = typeof(Components).GetMethod(nameof(Components.ReadDirect));

                var result = new System.Collections.Generic.List<object>();
                foreach (var item in StaticTypesLoadedManaged.loadedTypes) {

                    var type = item.Value;
                    var gHasMethod = methodHas.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(world.state->components, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = methodRead.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(world.state->components, new object[] { this.ent });
                        result.Add(data);
                    }

                }
                
                return result.ToArray();
            }
        }

        public object[] sharedComponents {
            get {
                
                if (this.alive == false) return null;

                var world = this.ent.World;
                var result = new System.Collections.Generic.List<object>();
                var methodHas = typeof(Components).GetMethod(nameof(Components.HasSharedDirect));
                var methodRead = typeof(Components).GetMethod(nameof(Components.ReadSharedDirect));
                foreach (var item in StaticTypesLoadedManaged.loadedSharedTypes) {
                    
                    var type = item.Value;
                    var gHasMethod = methodHas.MakeGenericMethod(type);
                    var has = (bool)gHasMethod.Invoke(world.state->components, new object[] { this.ent });
                    if (has == true) {
                        var gReadMethod = methodRead.MakeGenericMethod(type);
                        var data = gReadMethod.Invoke(world.state->components, new object[] { this.ent });
                        result.Add(data);
                    }
                    
                }

                return result.ToArray();
            }
        }

    }

    public unsafe class MemArrayProxy<T> where T : unmanaged {

        private MemArray<T> arr;
        
        public MemArrayProxy(MemArray<T> arr) {

            this.arr = arr;

        }

        public T[] items {
            get {
                var world = Context.world;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[world.state->allocator, i];
                }

                return arr;
            }
        }

    }

    public unsafe class MemArrayAutoProxy<T> where T : unmanaged {

        private MemArrayAuto<T> arr;
        
        public MemArrayAutoProxy(MemArrayAuto<T> arr) {

            this.arr = arr;

        }

        public T[] items {
            get {
                var world = Context.world;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[world.state->allocator, i];
                }

                return arr;
            }
        }

    }

    public unsafe class MemArrayThreadCacheLineProxy<T> where T : unmanaged {

        private MemArrayThreadCacheLine<T> arr;
        
        public MemArrayThreadCacheLineProxy(MemArrayThreadCacheLine<T> arr) {

            this.arr = arr;

        }

        public T[] items {
            get {
                var world = Context.world;
                var arr = new T[this.arr.Length];
                for (int i = 0; i < this.arr.Length; ++i) {
                    arr[i] = this.arr[world.state->allocator, i];
                }

                return arr;
            }
        }

    }

    public unsafe class ListProxy<T> where T : unmanaged {

        private List<T> arr;
        
        public ListProxy(List<T> arr) {

            this.arr = arr;

        }

        public uint Capacity {
            get {
                return this.arr.Capacity;
            }
        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public T[] items {
            get {
                var world = Context.world;
                var arr = new T[this.arr.Count];
                for (uint i = 0; i < this.arr.Count; ++i) {
                    arr[i] = this.arr[world.state->allocator, i];
                }

                return arr;
            }
        }

    }

    public class QueueProxy<T> where T : unmanaged {

        private Queue<T> arr;
        
        public QueueProxy(Queue<T> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public T[] items {
            get {
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

    public class StackProxy<T> where T : unmanaged {

        private Stack<T> arr;
        
        public StackProxy(Stack<T> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public T[] items {
            get {
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

    public class EquatableDictionaryProxy<K, V> where K : unmanaged, System.IEquatable<K> where V : unmanaged {

        private EquatableDictionary<K, V> arr;
        
        public EquatableDictionaryProxy(EquatableDictionary<K, V> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public MemArray<uint> buckets => this.arr.buckets;
        public MemArray<EquatableDictionary<K, V>.Entry> entries => this.arr.entries;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint freeCount => this.arr.freeCount;

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

    public class UIntDictionaryProxy<V> where V : unmanaged {

        private UIntDictionary<V> arr;
        
        public UIntDictionaryProxy(UIntDictionary<V> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public MemArray<uint> buckets => this.arr.buckets;
        public MemArray<UIntDictionary<V>.Entry> entries => this.arr.entries;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint freeCount => this.arr.freeCount;

        public System.Collections.Generic.KeyValuePair<uint, V>[] items {
            get {
                var arr = new System.Collections.Generic.KeyValuePair<uint, V>[this.arr.Count];
                var i = 0;
                var e = this.arr.GetEnumerator(Context.world);
                while (e.MoveNext() == true) {
                    arr[i++] = new System.Collections.Generic.KeyValuePair<uint, V>(e.Current.key, e.Current.value);
                }
                
                return arr;
            }
        }

    }

    public class ULongDictionaryProxy<V> where V : unmanaged {

        private ULongDictionary<V> arr;
        
        public ULongDictionaryProxy(ULongDictionary<V> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public MemArray<uint> buckets => this.arr.buckets;
        public MemArray<ULongDictionary<V>.Entry> entries => this.arr.entries;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint freeCount => this.arr.freeCount;

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

    public class ULongDictionaryAutoProxy<V> where V : unmanaged {

        private ULongDictionaryAuto<V> arr;
        
        public ULongDictionaryAutoProxy(ULongDictionaryAuto<V> arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }

        public MemArrayAuto<uint> buckets => this.arr.buckets;
        public MemArrayAuto<ULongDictionaryAuto<V>.Entry> entries => this.arr.entries;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint freeCount => this.arr.freeCount;

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

    public class UIntHashSetProxy {

        private UIntHashSet arr;
        
        public UIntHashSetProxy(UIntHashSet arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }
        
        public MemArray<int> buckets => this.arr.buckets;
        public MemArray<UIntHashSet.Slot> slots => this.arr.slots;
        public uint hash => this.arr.hash;
        public uint count => (uint)this.arr.count;
        public uint version => (uint)this.arr.version;
        public int freeList => this.arr.freeList;
        public uint lastIndex => (uint)this.arr.lastIndex;

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

    public class UIntPairHashSetProxy {

        private UIntPairHashSet arr;
        
        public UIntPairHashSetProxy(UIntPairHashSet arr) {

            this.arr = arr;

        }

        public uint Count {
            get {
                return this.arr.Count;
            }
        }
        
        public MemArray<uint> buckets => this.arr.buckets;
        public MemArray<UIntPairHashSet.Slot> slots => this.arr.slots;
        public uint hash => this.arr.hash;
        public uint count => this.arr.count;
        public uint version => this.arr.version;
        public int freeList => this.arr.freeList;
        public uint lastIndex => this.arr.lastIndex;

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