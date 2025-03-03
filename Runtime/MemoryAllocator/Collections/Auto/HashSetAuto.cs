namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe struct HashSetAuto<T> : IIsCreated where T : unmanaged, System.IEquatable<T> {

        public struct Enumerator {

            private uint lastIndex;
            private uint index;
            private T current;
            private safe_ptr<Slot> slotsPtr;

            [INLINE(256)]
            internal Enumerator(in HashSetAuto<T> set, safe_ptr<State> state) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (safe_ptr<Slot>)set.slots.GetUnsafePtrCached(in state.ptr->allocator);
                this.current = default;
            }

            [INLINE(256)]
            public bool MoveNext() {
                while (this.index < this.lastIndex) {
                    var v = this.slotsPtr + this.index;
                    if (v.ptr->hashCode >= 0) {
                        this.current = v.ptr->value;
                        ++this.index;
                        return true;
                    }

                    ++this.index;
                }

                this.index = this.lastIndex + 1u;
                this.current = default;
                return false;
            }

            public T Current => this.current;

        }

        public struct Slot {
            internal int hashCode;      // Lower 31 bits of hash code, -1 if unused
            internal int next;          // Index of next entry, -1 if last
            internal T value;
        }
        
        public const int LOWER31_BIT_MASK = 0x7FFFFFFF;
        
        internal MemArrayAuto<uint> buckets;
        internal MemArrayAuto<Slot> slots;
        internal uint count;
        internal uint lastIndex;
        internal int freeList;
        internal uint version;
        internal uint hash;

        public readonly Ent ent => this.buckets.ent;

        public bool IsCreated {
            [INLINE(256)]
            get => this.buckets.IsCreated;
        }

        public uint Count {
            [INLINE(256)]
            get => this.count;
        }

        [INLINE(256)]
        public HashSetAuto(in Ent ent, uint capacity) {

            this = default;
            this.Initialize(in ent, capacity);

        }

        [INLINE(256)]
        public HashSetAuto(in HashSetAuto<T> other) {

            E.IS_CREATED(other);
            
            this = other;
            this.buckets = new MemArrayAuto<uint>(this.ent, other.buckets);
            this.slots = new MemArrayAuto<Slot>(this.ent, other.slots);

        }

        [INLINE(256)]
        public bool Equals(in HashSetAuto<T> other) {

            E.IS_CREATED(this);
            E.IS_CREATED(other);

            if (this.count != other.count) return false;
            if (this.hash != other.hash) return false;
            if (this.count == 0u && other.count == 0u) return true;

            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            var otherSlotsPtr = (safe_ptr<Slot>)other.slots.GetUnsafePtrCached();
            var otherBucketsPtr = (safe_ptr<int>)other.buckets.GetUnsafePtrCached();
            uint idx = 0u;
            while (idx < this.lastIndex) {
                var v = slotsPtr + idx;
                if (v.ptr->hashCode >= 0) {
                    if (other.Contains(v.ptr->value, otherSlotsPtr, otherBucketsPtr) == false) {
                        return false;
                    }
                }
                ++idx;
            }

            return true;

        }

        [INLINE(256)]
        public void Set(in HashSetAuto<T> other) {
            
            this = other;
            this.buckets = new MemArrayAuto<uint>(other.ent, other.buckets);
            this.slots = new MemArrayAuto<Slot>(other.ent, other.slots);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.buckets.BurstMode(in allocator, state);
            this.slots.BurstMode(in allocator, state);
        }
        
        [INLINE(256)]
        public void Dispose() {
            
            this.buckets.Dispose();
            this.slots.Dispose();
            this = default;
            
        }

        [INLINE(256)]
        public readonly MemPtr GetMemPtr() {
            
            E.IS_CREATED(this);
            return this.buckets.arrPtr;

        }

        [INLINE(256)]
        public void ReplaceWith(in HashSetAuto<T> other) {

            if (this.GetMemPtr() == other.GetMemPtr()) {
                return;
            }

            this.Dispose();
            this = other;

        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator() {
            
            return new Enumerator(this, this.ent.World.state);
            
        }

        /// <summary>
        /// Remove all items from this set. This clears the elements but not the underlying 
        /// buckets and slots array. Follow this call by TrimExcess to release these.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            if (this.lastIndex > 0) {
                // clear the elements so that the gc can reclaim the references.
                // clear only up to m_lastIndex for m_slots
                this.slots.Clear(0, this.lastIndex);
                this.buckets.Clear(0, this.buckets.Length);
                this.lastIndex = 0u;
                this.count = 0u;
                this.freeList = -1;
                this.hash = 0u;
            }
            ++this.version;
        }

        /// <summary>
        /// Checks if this hashset contains the item
        /// </summary>
        /// <param name="item">item to check for containment</param>
        /// <returns>true if item contained; false if not</returns>
        [INLINE(256)]
        public readonly bool Contains(T item) {
            E.IS_CREATED(this);
            uint hashCode = GetHashCode(item) & HashSetAuto<T>.LOWER31_BIT_MASK;
            // see note at "HashSet" level describing why "- 1" appears in for loop
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            for (int i = (int)this.buckets[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    Equal((slotsPtr + i).ptr->value, item) == true) {
                    return true;
                }
            }
            // either m_buckets is null or wasn't found
            return false;
        }
        
        [INLINE(256)]
        public readonly bool Contains(T item, safe_ptr<Slot> slotsPtr, safe_ptr<int> bucketsPtr) {
            uint hashCode = GetHashCode(item) & HashSetAuto<T>.LOWER31_BIT_MASK;
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    Equal((slotsPtr + i).ptr->value, item) == true) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public readonly bool Contains(T item, uint hashCode, safe_ptr<Slot> slotsPtr, safe_ptr<int> bucketsPtr) {
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    Equal((slotsPtr + i).ptr->value, item) == true) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public void RemoveExcept(in HashSetAuto<T> other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    var item = slot.ptr->value;
                    if (!other.Contains(item)) {
                        this.Remove(item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Remove(in HashSetAuto<T> other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    var item = slot.ptr->value;
                    if (other.Contains(item)) {
                        this.Remove(item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Add(in HashSetAuto<T> other) {
            var slotsPtr = (safe_ptr<Slot>)other.slots.GetUnsafePtrCached();
            for (int i = 0; i < other.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    this.Add(slot.ptr->value);
                }
            }
        }

        /// <summary>
        /// Remove item from this hashset
        /// </summary>
        /// <param name="item">item to remove</param>
        /// <returns>true if removed; false if not (i.e. if the item wasn't in the HashSet)</returns>
        [INLINE(256)]
        public bool Remove(T item) {
            if (this.buckets.IsCreated == true) {
                uint hashCode = GetHashCode(item) & HashSetAuto<T>.LOWER31_BIT_MASK;
                uint bucket = hashCode % this.buckets.Length;
                int last = -1;
                var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached();
                var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
                for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; last = i, i = (slotsPtr + i).ptr->next) {
                    var slot = slotsPtr + i;
                    if (slot.ptr->hashCode == hashCode &&
                        Equal(slot.ptr->value, item) == true) {
                        if (last < 0) {
                            // first iteration; update buckets
                            *(bucketsPtr + bucket).ptr = slot.ptr->next + 1;
                        }
                        else {
                            // subsequent iterations; update 'next' pointers
                            (slotsPtr + last).ptr->next = slot.ptr->next;
                        }
                        slot.ptr->hashCode = -1;
                        slot.ptr->value = default;
                        slot.ptr->next = this.freeList;

                        this.hash ^= GetHashCode(item);

                        ++this.version;
                        if (--this.count == 0) {
                            this.lastIndex = 0;
                            this.freeList = -1;
                        } else {
                            this.freeList = i;
                        }
                        return true;
                    }
                }
            }
            // either m_buckets is null or wasn't found
            return false;
        }

        /// <summary>
        /// Initializes buckets and slots arrays. Uses suggested capacity by finding next prime
        /// greater than or equal to capacity.
        /// </summary>
        /// <param name="ent"></param>
        /// <param name="capacity"></param>
        [INLINE(256)]
        private void Initialize(in Ent ent, uint capacity) {
            uint size = HashHelpers.GetPrime(capacity);
            this.buckets = new MemArrayAuto<uint>(in ent, size);
            this.slots = new MemArrayAuto<Slot>(in ent, size);
            var slots = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            for (int i = 0; i < this.slots.Length; ++i) {
                (*(slots + i).ptr).hashCode = -1;
            }
            this.freeList = -1;
        }

        /// <summary>
        /// Expand to new capacity. New capacity is next prime greater than or equal to suggested 
        /// size. This is called when the underlying array is filled. This performs no 
        /// defragmentation, allowing faster execution; note that this is reasonable since 
        /// AddIfNotPresent attempts to insert new elements in re-opened spots.
        /// </summary>
        [INLINE(256)]
        private void IncreaseCapacity() {
            uint newSize = HashHelpers.ExpandPrime(this.count);
            if (newSize <= this.count) {
                throw new System.ArgumentException();
            }

            // Able to increase capacity; copy elements to larger array and rehash
            this.SetCapacity(newSize, false);
        }

        /// <summary>
        /// Set the underlying buckets array to size newSize and rehash.  Note that newSize
        /// *must* be a prime.  It is very likely that you want to call IncreaseCapacity()
        /// instead of this method.
        /// </summary>
        [INLINE(256)]
        private void SetCapacity(uint newSize, bool forceNewHashCodes) { 
            
            var newSlots = new MemArrayAuto<Slot>(this.ent, newSize);
            if (this.slots.IsCreated == true) {
                NativeArrayUtils.CopyNoChecks(in this.slots, 0, ref newSlots, 0, this.lastIndex);
            }

            if (forceNewHashCodes == true) {
                for(int i = 0; i < this.lastIndex; i++) {
                    if(newSlots[i].hashCode != -1) {
                        newSlots[i].hashCode = (int)GetHashCode(newSlots[i].value);
                    }
                }
            }

            var newBuckets = new MemArrayAuto<uint>(this.ent, newSize);
            for (uint i = 0; i < this.lastIndex; ++i) {
                uint bucket = (uint)(newSlots[i].hashCode % newSize);
                newSlots[i].next = (int)newBuckets[bucket] - 1;
                newBuckets[bucket] = i + 1;
            }
            if (this.slots.IsCreated == true) this.slots.Dispose();
            if (this.buckets.IsCreated == true) this.buckets.Dispose();
            this.slots = newSlots;
            this.buckets = newBuckets;
        }

        /// <summary>
        /// Add item to this HashSet. Returns bool indicating whether item was added (won't be 
        /// added if already present)
        /// </summary>
        /// <param name="value"></param>
        /// <returns>true if added, false if already present</returns>
        [INLINE(256)]
        public bool Add(T value) {
            
            E.IS_CREATED(this);

            var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached();
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
            
            uint hashCode = GetHashCode(value) & HashSetAuto<T>.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                var slot = slotsPtr + i;
                if (slot.ptr->hashCode == hashCode &&
                    Equal(slot.ptr->value, value) == true) {
                    return false;
                }
            }

            this.hash ^= GetHashCode(value);
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index).ptr->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity();
                    // this will change during resize
                    bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached();
                    slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
                    bucket = hashCode % this.buckets.Length;
                }
                index = this.lastIndex;
                ++this.lastIndex;
            }

            {
                var slot = slotsPtr + index;
                slot.ptr->hashCode = (int)hashCode;
                slot.ptr->value = value;
                slot.ptr->next = *(bucketsPtr + bucket).ptr - 1;
                *(bucketsPtr + bucket).ptr = (int)(index + 1u);
                ++this.count;
                ++this.version;
            }

            return true;
        }
        
        [INLINE(256)]
        public bool Add(T value, ref safe_ptr<int> bucketsPtr, ref safe_ptr<Slot> slotsPtr) {
            
            E.IS_CREATED(this);
            
            uint hashCode = GetHashCode(value) & HashSetAuto<T>.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                var slot = slotsPtr + i;
                if (slot.ptr->hashCode == hashCode &&
                    Equal(slot.ptr->value, value) == true) {
                    return false;
                }
            }

            this.hash ^= GetHashCode(value);
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index).ptr->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity();
                    // this will change during resize
                    bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached();
                    slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached();
                    bucket = hashCode % this.buckets.Length;
                }
                index = this.lastIndex;
                ++this.lastIndex;
            }

            {
                var slot = slotsPtr + index;
                slot.ptr->hashCode = (int)hashCode;
                slot.ptr->value = value;
                slot.ptr->next = *(bucketsPtr + bucket).ptr - 1;
                *(bucketsPtr + bucket).ptr = (int)(index + 1u);
                ++this.count;
                ++this.version;
            }

            return true;
        }

        [INLINE(256)]
        public readonly uint GetHash() {
            return this.hash;
        }

        [INLINE(256)]
        public void CopyFrom(in HashSetAuto<T> other) {

            NativeArrayUtils.CopyExact(in other.buckets, ref this.buckets);
            this.slots.CopyFrom(in other.slots);
            var thisBuckets = this.buckets;
            var thisSlots = this.slots;
            this = other;
            this.buckets = thisBuckets;
            this.slots = thisSlots;

        }

        [INLINE(256)]
        public static bool Equal(T v1, T v2) {
            return v1.Equals(v2);
        }

        [INLINE(256)]
        public static uint GetHashCode(T item) {
            return (uint)item.GetHashCode();
        }

    }

}
