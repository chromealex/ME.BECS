namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(EquatableDictionaryProxy<,>))]
    public unsafe struct EquatableDictionary<TKey, TValue> where TKey : unmanaged, System.IEquatable<TKey> where TValue : unmanaged {

        public struct Enumerator {

            private uint count;
            private readonly safe_ptr<Entry> entries;
            private uint index;

            internal Enumerator(in EquatableDictionary<TKey, TValue> dictionary, safe_ptr<State> state) {
                this.entries = dictionary.entries.GetUnsafePtrCached(in state.ptr->allocator);
                this.count = dictionary.count;
                this.index = 0u;
            }

            public bool MoveNext() {

                while (this.index < this.count) {
                    ref var local = ref this.entries[this.index++];
                    if (local.hashCode >= 0) {
                        return true;
                    }
                }

                this.index = this.count + 1u;
                return false;
            }

            public ref Entry Current => ref *(this.entries + this.index - 1u).ptr;

        }

        public struct Entry {

            public int hashCode; // Lower 31 bits of hash code, -1 if unused
            public int next; // Index of next entry, -1 if last
            public TKey key; // Key of entry
            public TValue value; // Value of entry

        }

        internal MemArray<uint> buckets;
        internal MemArray<Entry> entries;
        internal uint count;
        internal uint version;
        internal int freeList;
        internal uint freeCount;

        public bool isCreated {
            [INLINE(256)]
            get => this.buckets.IsCreated;
        }

        public readonly uint Count {
            [INLINE(256)]
            get => this.count - this.freeCount;
        }

        [INLINE(256)]
        public EquatableDictionary(ref MemoryAllocator allocator, uint capacity) {

            this = default;
            this.Initialize(ref allocator, capacity);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.buckets.BurstMode(in allocator, state);
            this.entries.BurstMode(in allocator, state);
        }

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {

            this.buckets.Dispose(ref allocator);
            this.entries.Dispose(ref allocator);
            this = default;

        }

        [INLINE(256)]
        public readonly MemPtr GetMemPtr() {

            return this.buckets.arrPtr;

        }

        [INLINE(256)]
        public void ReplaceWith(ref MemoryAllocator allocator, in EquatableDictionary<TKey, TValue> other) {
            
            if (this.GetMemPtr() == other.GetMemPtr()) return;
            
            this.Dispose(ref allocator);
            this = other;

        }

        [INLINE(256)]
        public void CopyFrom(ref MemoryAllocator allocator, in EquatableDictionary<TKey, TValue> other) {

            if (this.GetMemPtr() == other.GetMemPtr()) return;
            if (this.GetMemPtr().IsValid() == false && other.GetMemPtr().IsValid() == false) return;
            if (this.GetMemPtr().IsValid() == true && other.GetMemPtr().IsValid() == false) {
                this.Dispose(ref allocator);
                return;
            }
            if (this.GetMemPtr().IsValid() == false) this = new EquatableDictionary<TKey, TValue>(ref allocator, other.Count);
            
            NativeArrayUtils.CopyExact(ref allocator, other.buckets, ref this.buckets);
            NativeArrayUtils.CopyExact(ref allocator, other.entries, ref this.entries);
            this.count = other.count;
            this.version = other.version;
            this.freeCount = other.freeCount;
            this.freeList = other.freeList;

        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator(World world) {

            return new Enumerator(in this, world.state);

        }

        /// <summary><para>Gets or sets the value associated with the specified key.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="key">The key whose value is to be gotten or set.</param>
        public readonly ref TValue this[in MemoryAllocator allocator, TKey key] {
            [INLINE(256)]
            get {
                var entry = this.FindEntry(in allocator, key);
                if (entry >= 0) {
                    return ref this.entries[in allocator, entry].value;
                }

                throw new System.Collections.Generic.KeyNotFoundException();
            }
        }

        [INLINE(256)]
        public ref TValue GetValue(ref MemoryAllocator allocator, TKey key) {

            var entry = this.FindEntry(in allocator, key);
            if (entry >= 0) {
                return ref this.entries[in allocator, entry].value;
            }

            this.TryInsert(ref allocator, key, default, InsertionBehavior.OverwriteExisting);
            return ref this.entries[in allocator, this.FindEntry(in allocator, key)].value;

        }

        [INLINE(256)]
        public ref TValue GetValue(ref MemoryAllocator allocator, TKey key, out bool exist) {
            
            var entry = this.FindEntry(in allocator, key);
            if (entry >= 0) {
                exist = true;
                return ref this.entries[in allocator, entry].value;
            }

            exist = false;
            this.TryInsert(ref allocator, key, default, InsertionBehavior.OverwriteExisting);
            return ref this.entries[in allocator, this.FindEntry(in allocator, key)].value;

        }

        [INLINE(256)]
        public TValue GetValueAndRemove(ref MemoryAllocator allocator, TKey key) {

            this.Remove(ref allocator, key, out var value);
            return value;

        }

        /// <summary><para>Adds an element with the specified key and value to the dictionary.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="key">The key of the element to add to the dictionary.</param>
        /// <param name="value"></param>
        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, TKey key, TValue value) {
            this.TryInsert(ref allocator, key, value, InsertionBehavior.ThrowOnExisting);
        }

        /// <summary><para>Removes all elements from the dictionary.</para></summary>
        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {
            var count = this.count;
            if (count > 0) {
                this.buckets.Clear(ref allocator);
                this.count = 0;
                this.freeList = -1;
                this.freeCount = 0;
                this.entries.Clear(ref allocator, 0, count);
            }

            ++this.version;
        }

        /// <summary><para>Determines whether the dictionary contains an element with a specific key.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="key">The key to locate in the dictionary.</param>
        [INLINE(256)]
        public readonly bool ContainsKey(in MemoryAllocator allocator, TKey key) {
            return this.FindEntry(in allocator, key) >= 0;
        }

        /// <summary><para>Determines whether the dictionary contains an element with a specific value.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="value">The value to locate in the dictionary.</param>
        [INLINE(256)]
        public readonly bool ContainsValue(in MemoryAllocator allocator, TValue value) { 
            for (var index = 0; index < this.count; ++index) {
                if (this.entries[in allocator, index].hashCode >= 0 && System.Collections.Generic.EqualityComparer<TValue>.Default.Equals(this.entries[in allocator, index].value, value)) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        private readonly int FindEntry(in MemoryAllocator allocator, TKey key) {
            var index = -1;
            var num1 = 0;
            if (this.buckets.IsCreated == true) {
                var num2 = GetHashCode(key) & int.MaxValue;
                index = (int)this.buckets[in allocator, (uint)(num2 % this.buckets.Length)] - 1;
                while ((uint)index < (uint)this.entries.Length &&
                       (this.entries[in allocator, index].hashCode != num2 || !this.entries[in allocator, index].key.Equals(key))) {
                    index = this.entries[in allocator, index].next;
                    if (num1 >= this.entries.Length) {
                        E.OUT_OF_RANGE();
                    }

                    ++num1;
                }
            }

            return index;
        }

        [INLINE(256)]
        private uint Initialize(ref MemoryAllocator allocator, uint capacity) {
            var prime = HashHelpers.GetPrime(capacity);
            this.freeList = -1;
            this.buckets = new MemArray<uint>(ref allocator, prime);
            this.entries = new MemArray<Entry>(ref allocator, prime);
            return prime;
        }

        [INLINE(256)]
        private bool TryInsert(ref MemoryAllocator allocator, TKey key, TValue value, InsertionBehavior behavior) {
            ++this.version;
            if (this.buckets.IsCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            ref var entries = ref this.entries;
            var num1 = GetHashCode(key) & int.MaxValue;
            var num2 = 0u;
            ref var local1 = ref this.buckets[in allocator, (uint)(num1 % this.buckets.Length)];
            var index1 = (int)local1 - 1;
            {
                while ((uint)index1 < (uint)entries.Length) {
                    if (entries[in allocator, index1].hashCode == num1 &&
                        entries[in allocator, index1].key.Equals(key)) {
                        switch (behavior) {
                            case InsertionBehavior.OverwriteExisting:
                                entries[in allocator, index1].value = value;
                                return true;

                            case InsertionBehavior.ThrowOnExisting:
                                E.ADDING_DUPLICATE();
                                break;
                        }

                        return false;
                    }

                    index1 = entries[in allocator, index1].next;
                    if (num2 >= entries.Length) {
                        E.OUT_OF_RANGE();
                    }

                    ++num2;
                }
            }
            var flag1 = false;
            var flag2 = false;
            uint index2;
            if (this.freeCount > 0) {
                index2 = (uint)this.freeList;
                flag2 = true;
                --this.freeCount;
            } else {
                var count = this.count;
                if (count == entries.Length) {
                    this.Resize(ref allocator);
                    flag1 = true;
                }

                index2 = count;
                this.count = count + 1;
                entries = ref this.entries;
            }

            ref var local2 = ref (flag1 ? ref this.buckets[in allocator, (uint)(num1 % this.buckets.Length)] : ref local1);
            ref var local3 = ref entries[in allocator, index2];
            if (flag2) {
                this.freeList = local3.next;
            }

            local3.hashCode = num1;
            local3.next = (int)local2 - 1;
            local3.key = key;
            local3.value = value;
            local2 = index2 + 1;
            return true;
        }

        [INLINE(256)]
        private void Resize(ref MemoryAllocator allocator) {
            this.Resize(ref allocator, HashHelpers.ExpandPrime(this.count));
        }

        [INLINE(256)]
        private void Resize(ref MemoryAllocator allocator, uint newSize) {
            var numArray = new MemArray<uint>(ref allocator, newSize);
            var entryArray = new MemArray<Entry>(ref allocator, newSize);
            var count = this.count;
            NativeArrayUtils.CopyNoChecks(ref allocator, this.entries, 0, ref entryArray, 0, count);
            for (uint index1 = 0u; index1 < count;  ++index1) {
                if (entryArray[in allocator, index1].hashCode >= 0) {
                    uint index2 = (uint)(entryArray[in allocator, index1].hashCode % newSize);
                    entryArray[in allocator, index1].next = (int)numArray[in allocator, index2] - 1;
                    numArray[in allocator, index2] = index1 + 1u;
                }
            }

            if (this.buckets.IsCreated == true) {
                this.buckets.Dispose(ref allocator);
            }

            if (this.entries.IsCreated == true) {
                this.entries.Dispose(ref allocator);
            }

            this.buckets = numArray;
            this.entries = entryArray;
        }

        /// <summary><para>Removes the element with the specified key from the dictionary.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="key">The key of the element to be removed from the dictionary.</param>
        [INLINE(256)]
        public bool Remove(ref MemoryAllocator allocator, TKey key) {
            if (this.buckets.IsCreated == true) {
                var num = GetHashCode(key) & int.MaxValue;
                var index1 = (int)(num % this.buckets.Length);
                var index2 = -1;
                // ISSUE: variable of a reference type
                var next = 0;
                for (var index3 = (int)this.buckets[in allocator, index1] - 1; index3 >= 0; index3 = next) {
                    ref var local = ref this.entries[in allocator, index3];
                    next = local.next;
                    if (local.hashCode == num) {
                        if ((local.key.Equals(key) ? 1 : 0) != 0) {
                            if (index2 < 0) {
                                this.buckets[in allocator, index1] = (uint)(local.next + 1);
                            } else {
                                this.entries[in allocator, index2].next = local.next;
                            }

                            local.hashCode = -1;
                            local.next = this.freeList;

                            this.freeList = index3;
                            ++this.freeCount;
                            ++this.version;
                            return true;
                        }
                    }

                    index2 = index3;
                }
            }

            return false;
        }

        /// <summary><para>Removes the element with the specified key from the dictionary.</para></summary>
        /// <param name="allocator"></param>
        /// <param name="key">The key of the element to be removed from the dictionary.</param>
        /// <param name="value"></param>
        [INLINE(256)]
        public bool Remove(ref MemoryAllocator allocator, TKey key, out TValue value) {
            if (this.buckets.IsCreated == true) {
                var num = GetHashCode(key) & int.MaxValue;
                var index1 = (int)(num % this.buckets.Length);
                var index2 = -1;
                // ISSUE: variable of a reference type
                var next = 0;
                for (var index3 = (int)this.buckets[in allocator, index1] - 1; index3 >= 0; index3 = next) {
                    ref var local = ref this.entries[in allocator, index3];
                    next = local.next;
                    if (local.hashCode == num) {
                        if ((local.key.Equals(key) ? 1 : 0) != 0) {
                            if (index2 < 0) {
                                this.buckets[in allocator, index1] = (uint)(local.next + 1);
                            } else {
                                this.entries[in allocator, index2].next = local.next;
                            }

                            value = local.value;
                            local.hashCode = -1;
                            local.next = this.freeList;

                            this.freeList = index3;
                            ++this.freeCount;
                            ++this.version;
                            return true;
                        }
                    }

                    index2 = index3;
                }
            }

            value = default(TValue);
            return false;
        }

        /// <summary>To be added.</summary>
        /// <param name="allocator"></param>
        /// <param name="key">To be added.</param>
        /// <param name="value"></param>
        [INLINE(256)]
        public readonly bool TryGetValue(in MemoryAllocator allocator, TKey key, out TValue value) {
            var entry = this.FindEntry(in allocator, key);
            if (entry >= 0) {
                value = this.entries[in allocator, entry].value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        [INLINE(256)]
        public bool TryAdd(ref MemoryAllocator allocator, TKey key, TValue value) {
            return this.TryInsert(ref allocator, key, value, InsertionBehavior.None);
        }

        [INLINE(256)]
        public uint EnsureCapacity(ref MemoryAllocator allocator, uint capacity) {
            E.IS_CREATED(this);

            var num = this.entries.Length;
            if (num >= capacity) {
                return num;
            }

            if (this.buckets.IsCreated == false) {
                return this.Initialize(ref allocator, capacity);
            }

            var prime = HashHelpers.GetPrime(capacity);
            this.Resize(ref allocator, prime);
            return prime;
        }

        [INLINE(256)]
        public static int GetHashCode(TKey key) {
            return key.GetHashCode();
        }
        
    }

}