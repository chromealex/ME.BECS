namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public readonly unsafe ref struct UIntPairHashSetRead {

        public ref struct Enumerator {

            internal UIntPairHashSetRead set;
            private uint index;
            private UIntPair current;

            [INLINE(256)]
            public bool MoveNext() {
                while (this.index < this.set.lastIndex) {
                    var v = this.set.slotsPtr + this.index;
                    if (v.ptr->hashCode >= 0) {
                        this.current = v.ptr->value;
                        ++this.index;
                        return true;
                    }

                    ++this.index;
                }

                this.index = this.set.lastIndex + 1u;
                this.current = default;
                return false;
            }

            public UIntPair Current => this.current;

        }

        public readonly safe_ptr<UIntPairHashSet.Slot> slotsPtr;
        public readonly safe_ptr<int> bucketsPtr;
        public readonly uint lastIndex;
        public readonly uint hash;
        public readonly uint bucketsLength;

        [INLINE(256)]
        public UIntPairHashSetRead(in MemoryAllocator allocator, in UIntPairHashSet set) {
            this.bucketsPtr = (safe_ptr<int>)set.buckets.GetUnsafePtrCached(in allocator);
            this.slotsPtr = (safe_ptr<UIntPairHashSet.Slot>)set.slots.GetUnsafePtrCached(in allocator);
            this.lastIndex = set.lastIndex;
            this.hash = set.hash;
            this.bucketsLength = set.buckets.Length;
        }
                
        [INLINE(256)]
        public static UIntPairHashSetRead Create(in MemoryAllocator allocator, in UIntPairHashSet set) {
            return new UIntPairHashSetRead(in allocator, in set);
        }

        [INLINE(256)]
        public bool Contains(UIntPair item) {
            uint hashCode = item.GetHash() & UIntPairHashSet.LOWER31_BIT_MASK;
            for (int i = this.bucketsPtr[hashCode % this.bucketsLength] - 1; i >= 0; i = (this.slotsPtr + i).ptr->next) {
                if ((this.slotsPtr + i).ptr->hashCode == hashCode &&
                    (this.slotsPtr + i).ptr->value == item) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public Enumerator GetEnumerator() {
            Enumerator e = default;
            e.set = this;
            return e;
        }
                
    }

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(UIntPairHashSetProxy))]
    public unsafe struct UIntPairHashSet : IIsCreated {

        public struct Enumerator {

            private uint lastIndex;
            private uint index;
            private UIntPair current;
            private safe_ptr<Slot> slotsPtr;

            [INLINE(256)]
            internal Enumerator(in UIntPairHashSet set, safe_ptr<State> state) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (safe_ptr<Slot>)set.slots.GetUnsafePtrCached(in state.ptr->allocator);
                this.current = default;
            }

            [INLINE(256)]
            internal Enumerator(in UIntPairHashSet set, MemoryAllocator allocator) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (safe_ptr<Slot>)set.slots.GetUnsafePtrCached(in allocator);
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

            public UIntPair Current => this.current;

        }

        public struct Slot {
            internal int hashCode;      // Lower 31 bits of hash code, -1 if unused
            internal int next;          // Index of next entry, -1 if last
            internal UIntPair value;
        }
        
        public const int LOWER31_BIT_MASK = 0x7FFFFFFF;
        
        internal MemArray<uint> buckets;
        internal MemArray<Slot> slots;
        internal uint count;
        internal uint lastIndex;
        internal int freeList;
        internal uint version;
        internal uint hash;

        public bool IsCreated {
            [INLINE(256)]
            get => this.buckets.IsCreated;
        }

        public uint Count {
            [INLINE(256)]
            get => this.count;
        }

        [INLINE(256)]
        public UIntPairHashSet(ref MemoryAllocator allocator, uint capacity) {

            this = default;
            this.Initialize(ref allocator, capacity);

        }

        [INLINE(256)]
        public UIntPairHashSet(ref MemoryAllocator allocator, in UIntPairHashSet other) {

            E.IS_CREATED(other);
            
            this = other;
            this.buckets = new MemArray<uint>(ref allocator, other.buckets);
            this.slots = new MemArray<Slot>(ref allocator, other.slots);

        }

        [INLINE(256)]
        public bool Equals(in MemoryAllocator allocator, in UIntPairHashSet other) {

            E.IS_CREATED(this);
            E.IS_CREATED(other);

            if (this.count != other.count) return false;
            if (this.hash != other.hash) return false;
            if (this.count == 0u && other.count == 0u) return true;

            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            var otherSlotsPtr = (safe_ptr<Slot>)other.slots.GetUnsafePtrCached(in allocator);
            var otherBucketsPtr = (safe_ptr<int>)other.buckets.GetUnsafePtrCached(in allocator);
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
        public void Set(ref MemoryAllocator allocator, in UIntPairHashSet other) {
            
            this = other;
            this.buckets = new MemArray<uint>(ref allocator, other.buckets);
            this.slots = new MemArray<Slot>(ref allocator, other.slots);

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.buckets.BurstMode(in allocator, state);
            this.slots.BurstMode(in allocator, state);
        }
        
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            
            this.buckets.Dispose(ref allocator);
            this.slots.Dispose(ref allocator);
            this = default;
            
        }

        [INLINE(256)]
        public readonly MemPtr GetMemPtr() {
            
            E.IS_CREATED(this);
            return this.buckets.arrPtr;

        }

        [INLINE(256)]
        public void ReplaceWith(ref MemoryAllocator allocator, in UIntPairHashSet other) {

            if (this.GetMemPtr() == other.GetMemPtr()) {
                return;
            }

            this.Dispose(ref allocator);
            this = other;

        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator(World world) {
            
            return new Enumerator(this, world.state);
            
        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator(safe_ptr<State> state) {
            
            return new Enumerator(this, state);
            
        }

        [INLINE(256)]
        public readonly Enumerator GetEnumerator(in MemoryAllocator allocator) {
            
            return new Enumerator(this, allocator);
            
        }

        /// <summary>
        /// Remove all items from this set. This clears the elements but not the underlying 
        /// buckets and slots array. Follow this call by TrimExcess to release these.
        /// </summary>
        /// <param name="allocator"></param>
        [INLINE(256)]
        public void Clear(ref MemoryAllocator allocator) {
            if (this.lastIndex > 0) {
                // clear the elements so that the gc can reclaim the references.
                // clear only up to m_lastIndex for m_slots
                this.slots.Clear(ref allocator, 0, this.lastIndex);
                this.buckets.Clear(ref allocator, 0, this.buckets.Length);
                this.lastIndex = 0u;
                this.count = 0u;
                this.freeList = -1;
                this.hash = 0u;
            }
            this.version++;
        }

        /// <summary>
        /// Checks if this hashset contains the item
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="item">item to check for containment</param>
        /// <returns>true if item contained; false if not</returns>
        [INLINE(256)]
        public readonly bool Contains(in MemoryAllocator allocator, UIntPair item) {
            E.IS_CREATED(this);
            uint hashCode = item.GetHash() & UIntPairHashSet.LOWER31_BIT_MASK;
            // see note at "HashSet" level describing why "- 1" appears in for loop
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = (int)this.buckets[in allocator, hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    (slotsPtr + i).ptr->value == item) {
                    return true;
                }
            }
            // either m_buckets is null or wasn't found
            return false;
        }
        
        [INLINE(256)]
        public readonly bool Contains(UIntPair item, safe_ptr<Slot> slotsPtr, safe_ptr<int> bucketsPtr) {
            uint hashCode = item.GetHash() & UIntPairHashSet.LOWER31_BIT_MASK;
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    (slotsPtr + i).ptr->value == item) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public readonly bool Contains(UIntPair item, uint hashCode, Slot* slotsPtr, int* bucketsPtr) {
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i)->next) {
                if ((slotsPtr + i)->hashCode == hashCode &&
                    (slotsPtr + i)->value == item) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public void RemoveExcept(ref MemoryAllocator allocator, in UIntPairHashSet other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    var item = slot.ptr->value;
                    if (!other.Contains(in allocator, item)) {
                        this.Remove(ref allocator, item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Remove(ref MemoryAllocator allocator, in UIntPairHashSet other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    var item = slot.ptr->value;
                    if (other.Contains(in allocator, item)) {
                        this.Remove(ref allocator, item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, in UIntPairHashSet other) {
            var slotsPtr = (safe_ptr<Slot>)other.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < other.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot.ptr->hashCode >= 0) {
                    this.Add(ref allocator, slot.ptr->value);
                }
            }
        }

        /// <summary>
        /// Remove item from this hashset
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="item">item to remove</param>
        /// <returns>true if removed; false if not (i.e. if the item wasn't in the HashSet)</returns>
        [INLINE(256)]
        public bool Remove(ref MemoryAllocator allocator, UIntPair item) {
            if (this.buckets.IsCreated == true) {
                uint hashCode = item.GetHash() & UIntPairHashSet.LOWER31_BIT_MASK;
                uint bucket = hashCode % this.buckets.Length;
                int last = -1;
                var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
                var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
                for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; last = i, i = (slotsPtr + i).ptr->next) {
                    var slot = slotsPtr + i;
                    if (slot.ptr->hashCode == hashCode &&
                        slot.ptr->value == item) {
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

                        this.hash ^= item.GetHash();

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
        /// <param name="allocator"></param>
        /// <param name="capacity"></param>
        [INLINE(256)]
        private void Initialize(ref MemoryAllocator allocator, uint capacity) {
            uint size = HashHelpers.GetPrime(capacity);
            this.buckets = new MemArray<uint>(ref allocator, size);
            this.slots = new MemArray<Slot>(ref allocator, size);
            var slots = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
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
        /// <param name="allocator"></param>
        [INLINE(256)]
        private void IncreaseCapacity(ref MemoryAllocator allocator) {
            uint newSize = HashHelpers.ExpandPrime(this.count);
            if (newSize <= this.count) {
                throw new System.ArgumentException();
            }

            // Able to increase capacity; copy elements to larger array and rehash
            this.SetCapacity(ref allocator, newSize, false);
        }

        /// <summary>
        /// Set the underlying buckets array to size newSize and rehash.  Note that newSize
        /// *must* be a prime.  It is very likely that you want to call IncreaseCapacity()
        /// instead of this method.
        /// </summary>
        [INLINE(256)]
        private void SetCapacity(ref MemoryAllocator allocator, uint newSize, bool forceNewHashCodes) { 
            
            var newSlots = new MemArray<Slot>(ref allocator, newSize);
            if (this.slots.IsCreated == true) {
                NativeArrayUtils.CopyNoChecks(ref allocator, in this.slots, 0, ref newSlots, 0, this.lastIndex);
            }

            if (forceNewHashCodes == true) {
                for(int i = 0; i < this.lastIndex; i++) {
                    if(newSlots[in allocator, i].hashCode != -1) {
                        newSlots[in allocator, i].hashCode = (int)newSlots[in allocator, i].value.GetHash();
                    }
                }
            }

            var newBuckets = new MemArray<uint>(ref allocator, newSize);
            for (uint i = 0; i < this.lastIndex; ++i) {
                uint bucket = (uint)(newSlots[in allocator, i].hashCode % newSize);
                newSlots[in allocator, i].next = (int)newBuckets[in allocator, bucket] - 1;
                newBuckets[in allocator, bucket] = i + 1;
            }
            if (this.slots.IsCreated == true) this.slots.Dispose(ref allocator);
            if (this.buckets.IsCreated == true) this.buckets.Dispose(ref allocator);
            this.slots = newSlots;
            this.buckets = newBuckets;
        }

        /// <summary>
        /// Add item to this HashSet. Returns bool indicating whether item was added (won't be 
        /// added if already present)
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="value"></param>
        /// <returns>true if added, false if already present</returns>
        [INLINE(256)]
        public bool Add(ref MemoryAllocator allocator, UIntPair value) {
            
            if (this.buckets.IsCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            
            uint hashCode = value.GetHash() & UIntHashSet.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                var slot = slotsPtr + i;
                if (slot.ptr->hashCode == hashCode &&
                    slot.ptr->value == value) {
                    return false;
                }
            }

            this.hash ^= value.GetHash();
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index).ptr->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity(ref allocator);
                    // this will change during resize
                    bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
                    slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
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
        public bool Add(ref MemoryAllocator allocator, UIntPair value, ref safe_ptr<int> bucketsPtr, ref safe_ptr<Slot> slotsPtr) {
            
            if (this.buckets.IsCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            uint hashCode = value.GetHash() & UIntHashSet.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket).ptr - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                var slot = slotsPtr + i;
                if (slot.ptr->hashCode == hashCode &&
                    slot.ptr->value == value) {
                    return false;
                }
            }

            this.hash ^= value.GetHash();
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index).ptr->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity(ref allocator);
                    // this will change during resize
                    bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
                    slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
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
        public void CopyFrom(ref MemoryAllocator allocator, in UIntPairHashSet other) {

            this.buckets.CopyFrom(ref allocator, other.buckets);
            this.slots.CopyFrom(ref allocator, other.slots);
            var thisBuckets = this.buckets;
            var thisSlots = this.slots;
            this = other;
            this.buckets = thisBuckets;
            this.slots = thisSlots;

        }

    }

}
