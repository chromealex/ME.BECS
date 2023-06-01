namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public readonly unsafe ref struct UIntHashSetRead {

        public ref struct Enumerator {

            internal UIntHashSetRead set;
            private uint index;
            private uint current;

            [INLINE(256)]
            public bool MoveNext() {
                while (this.index < this.set.lastIndex) {
                    var v = this.set.slotsPtr + this.index;
                    if (v->hashCode >= 0) {
                        this.current = v->value;
                        ++this.index;
                        return true;
                    }

                    ++this.index;
                }

                this.index = this.set.lastIndex + 1u;
                this.current = default;
                return false;
            }

            public uint Current => this.current;

        }

        public readonly UIntHashSet.Slot* slotsPtr;
        public readonly int* bucketsPtr;
        public readonly uint lastIndex;
        public readonly uint hash;
        public readonly uint bucketsLength;

        [INLINE(256)]
        public UIntHashSetRead(in MemoryAllocator allocator, in UIntHashSet set) {
            this.bucketsPtr = (int*)set.buckets.GetUnsafePtrCached(in allocator);
            this.slotsPtr = (UIntHashSet.Slot*)set.slots.GetUnsafePtrCached(in allocator);
            this.lastIndex = set.lastIndex;
            this.hash = set.hash;
            this.bucketsLength = set.buckets.Length;
        }
                
        [INLINE(256)]
        public static UIntHashSetRead Create(in MemoryAllocator allocator, in UIntHashSet set) {
            return new UIntHashSetRead(in allocator, in set);
        }

        [INLINE(256)]
        public bool Contains(uint item) {
            uint hashCode = item & UIntHashSet.LOWER31_BIT_MASK;
            for (int i = this.bucketsPtr[hashCode % this.bucketsLength] - 1; i >= 0; i = (this.slotsPtr + i)->next) {
                if ((this.slotsPtr + i)->hashCode == hashCode &&
                    (this.slotsPtr + i)->value == item) {
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

    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(UIntHashSetProxy))]
    public unsafe struct UIntHashSet : IIsCreated {

        public struct Enumerator {

            private uint lastIndex;
            private uint index;
            private uint current;
            private Slot* slotsPtr;

            [INLINE(256)]
            internal Enumerator(in UIntHashSet set, State* state) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (Slot*)set.slots.GetUnsafePtrCached(in state->allocator);
                this.current = default;
            }

            [INLINE(256)]
            internal Enumerator(in UIntHashSet set, MemoryAllocator allocator) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (Slot*)set.slots.GetUnsafePtrCached(in allocator);
                this.current = default;
            }

            [INLINE(256)]
            public Enumerator(Slot* slotsPtr, uint lastIndex) {
                this.lastIndex = lastIndex;
                this.index = 0;
                this.slotsPtr = slotsPtr;
                this.current = default;
            }

            [INLINE(256)]
            public bool MoveNext() {
                while (this.index < this.lastIndex) {
                    var v = this.slotsPtr + this.index;
                    if (v->hashCode >= 0) {
                        this.current = v->value;
                        ++this.index;
                        return true;
                    }

                    ++this.index;
                }

                this.index = this.lastIndex + 1u;
                this.current = default;
                return false;
            }

            public uint Current => this.current;

        }

        public struct Slot {
            internal int hashCode;      // Lower 31 bits of hash code, -1 if unused
            internal int next;          // Index of next entry, -1 if last
            internal uint value;
        }
        
        public const int LOWER31_BIT_MASK = 0x7FFFFFFF;
        
        internal MemArray<uint> buckets;
        internal MemArray<Slot> slots;
        internal uint count;
        internal uint lastIndex;
        internal int freeList;
        internal uint version;
        internal uint hash;

        public bool isCreated {
            [INLINE(256)]
            get => this.buckets.isCreated;
        }

        public uint Count {
            [INLINE(256)]
            get => this.count;
        }

        [INLINE(256)]
        public UIntHashSet(ref MemoryAllocator allocator, uint capacity) {

            this = default;
            this.Initialize(ref allocator, capacity);

        }

        [INLINE(256)]
        public UIntHashSet(ref MemoryAllocator allocator, in UIntHashSet other) {

            E.IS_CREATED(other);

            this = other;
            this.buckets = new MemArray<uint>(ref allocator, other.buckets);
            this.slots = new MemArray<Slot>(ref allocator, other.slots);

        }

        [INLINE(256)]
        public bool Equals(in MemoryAllocator allocator, in UIntHashSet other) {

            E.IS_CREATED(this);
            E.IS_CREATED(other);
            
            if (this.count != other.count) return false;
            if (this.hash != other.hash) return false;
            if (this.count == 0u && other.count == 0u) return true;

            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            var otherSlotsPtr = (Slot*)other.slots.GetUnsafePtrCached(in allocator);
            var otherBucketsPtr = (int*)other.buckets.GetUnsafePtrCached(in allocator);
            uint idx = 0u;
            while (idx < this.lastIndex) {
                var v = slotsPtr + idx;
                if (v->hashCode >= 0) {
                    if (other.Contains(v->value, otherSlotsPtr, otherBucketsPtr) == false) {
                        return false;
                    }
                }
                ++idx;
            }

            return true;

        }

        [INLINE(256)]
        public void Set(ref MemoryAllocator allocator, in UIntHashSet other) {
            
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
        public void ReplaceWith(ref MemoryAllocator allocator, in UIntHashSet other) {

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
        public readonly Enumerator GetEnumerator(State* state) {
            
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
        public readonly bool Contains(in MemoryAllocator allocator, uint item) {
            E.IS_CREATED(this);
            uint hashCode = item & UIntHashSet.LOWER31_BIT_MASK;
            // see note at "HashSet" level describing why "- 1" appears in for loop
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = (int)this.buckets[in allocator, hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i)->next) {
                if ((slotsPtr + i)->hashCode == hashCode &&
                    (slotsPtr + i)->value == item) {
                    return true;
                }
            }
            // either m_buckets is null or wasn't found
            return false;
        }
        
        [INLINE(256)]
        public readonly bool Contains(uint item, Slot* slotsPtr, int* bucketsPtr) {
            uint hashCode = item & UIntHashSet.LOWER31_BIT_MASK;
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i)->next) {
                if ((slotsPtr + i)->hashCode == hashCode &&
                    (slotsPtr + i)->value == item) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public readonly bool Contains(uint item, uint hashCode, Slot* slotsPtr, int* bucketsPtr) {
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i)->next) {
                if ((slotsPtr + i)->hashCode == hashCode &&
                    (slotsPtr + i)->value == item) {
                    return true;
                }
            }
            return false;
        }

        [INLINE(256)]
        public void RemoveExcept(ref MemoryAllocator allocator, in UIntHashSet other) {
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot->hashCode >= 0) {
                    var item = slot->value;
                    if (!other.Contains(in allocator, item)) {
                        this.Remove(ref allocator, item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Remove(ref MemoryAllocator allocator, in BatchList other) {
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
            var list = other.list;
            for (int i = 0; i < list.Length; ++i) {
                if (list.IsSet(i) == true) {
                    var typeId = (uint)i;
                    if (this.Contains(typeId, slotsPtr, bucketsPtr) == true) {
                        this.Remove(ref allocator, typeId);
                    }
                }
            }
        }
        
        [INLINE(256)]
        public void Remove(ref MemoryAllocator allocator, in ComponentsFastTrack other) {
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
            var list = other.root;
            for (int i = 0; i < list.Length; ++i) {
                if (list.IsSet(i) == true) {
                    var typeId = (uint)i;
                    if (this.Contains(typeId, slotsPtr, bucketsPtr) == true) {
                        this.Remove(ref allocator, typeId);
                    }
                }
            }
        }

        /*
        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, in BatchList other) {
            var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            var node = other.root;
            while (node != null) {
                this.Add(ref allocator, node->typeId, ref bucketsPtr, ref slotsPtr);
                node = node->next;
            }
        }*/

        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, in ComponentsFastTrack other) {
            var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            var list = other.root;
            for (int i = 0; i < list.Length; ++i) {
                if (list.IsSet(i) == true) {
                    var typeId = (uint)i;
                    this.Add(ref allocator, typeId, ref bucketsPtr, ref slotsPtr);
                }
            }
        }

        [INLINE(256)]
        public void Remove(ref MemoryAllocator allocator, in UIntHashSet other) {
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < this.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot->hashCode >= 0) {
                    var item = slot->value;
                    if (other.Contains(in allocator, item)) {
                        this.Remove(ref allocator, item);
                        if (this.count == 0) return;
                    }
                }
            }
        }

        [INLINE(256)]
        public void Add(ref MemoryAllocator allocator, in UIntHashSet other) {
            var slotsPtr = (Slot*)other.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < other.lastIndex; i++) {
                var slot = (slotsPtr + i);
                if (slot->hashCode >= 0) {
                    this.Add(ref allocator, slot->value);
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
        public bool Remove(ref MemoryAllocator allocator, uint item) {
            if (this.buckets.isCreated == true) {
                uint hashCode = item & UIntHashSet.LOWER31_BIT_MASK;
                uint bucket = hashCode % this.buckets.Length;
                int last = -1;
                var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
                var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
                for (int i = *(bucketsPtr + bucket) - 1; i >= 0; last = i, i = (slotsPtr + i)->next) {
                    var slot = slotsPtr + i;
                    if (slot->hashCode == hashCode &&
                        slot->value == item) {
                        if (last < 0) {
                            // first iteration; update buckets
                            *(bucketsPtr + bucket) = slot->next + 1;
                        }
                        else {
                            // subsequent iterations; update 'next' pointers
                            (slotsPtr + last)->next = slot->next;
                        }
                        slot->hashCode = -1;
                        slot->value = default;
                        slot->next = this.freeList;

                        this.hash ^= item;

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
            var slots = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            for (int i = 0; i < this.slots.Length; ++i) {
                (*(slots + i)).hashCode = -1;
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
            if (this.slots.isCreated == true) {
                NativeArrayUtils.CopyNoChecks(ref allocator, in this.slots, 0, ref newSlots, 0, this.lastIndex);
            }

            if (forceNewHashCodes == true) {
                for(int i = 0; i < this.lastIndex; i++) {
                    if(newSlots[in allocator, i].hashCode != -1) {
                        newSlots[in allocator, i].hashCode = (int)newSlots[in allocator, i].value;
                    }
                }
            }

            var newBuckets = new MemArray<uint>(ref allocator, newSize);
            for (uint i = 0; i < this.lastIndex; ++i) {
                uint bucket = (uint)(newSlots[in allocator, i].hashCode % newSize);
                newSlots[in allocator, i].next = (int)newBuckets[in allocator, bucket] - 1;
                newBuckets[in allocator, bucket] = i + 1;
            }
            if (this.slots.isCreated == true) this.slots.Dispose(ref allocator);
            if (this.buckets.isCreated == true) this.buckets.Dispose(ref allocator);
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
        public bool Add(ref MemoryAllocator allocator, uint value) {
            
            if (this.buckets.isCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            var bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
            var slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
            
            uint hashCode = value & UIntHashSet.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket) - 1; i >= 0; i = (slotsPtr + i)->next) {
                var slot = slotsPtr + i;
                if (slot->hashCode == hashCode &&
                    slot->value == value) {
                    return false;
                }
            }

            this.hash ^= value;
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index)->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity(ref allocator);
                    // this will change during resize
                    bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
                    slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
                    bucket = hashCode % this.buckets.Length;
                }
                index = this.lastIndex;
                ++this.lastIndex;
            }

            {
                var slot = slotsPtr + index;
                slot->hashCode = (int)hashCode;
                slot->value = value;
                slot->next = *(bucketsPtr + bucket) - 1;
                *(bucketsPtr + bucket) = (int)(index + 1u);
                ++this.count;
                ++this.version;
            }

            return true;
        }
        
        [INLINE(256)]
        public bool Add(ref MemoryAllocator allocator, uint value, ref int* bucketsPtr, ref Slot* slotsPtr) {
            
            if (this.buckets.isCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            uint hashCode = value & UIntHashSet.LOWER31_BIT_MASK;
            uint bucket = hashCode % this.buckets.Length;
            for (int i = *(bucketsPtr + bucket) - 1; i >= 0; i = (slotsPtr + i)->next) {
                var slot = slotsPtr + i;
                if (slot->hashCode == hashCode &&
                    slot->value == value) {
                    return false;
                }
            }

            this.hash ^= value;
            
            uint index;
            if (this.freeList >= 0) {
                index = (uint)this.freeList;
                this.freeList = (slotsPtr + index)->next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity(ref allocator);
                    // this will change during resize
                    bucketsPtr = (int*)this.buckets.GetUnsafePtrCached(in allocator);
                    slotsPtr = (Slot*)this.slots.GetUnsafePtrCached(in allocator);
                    bucket = hashCode % this.buckets.Length;
                }
                index = this.lastIndex;
                ++this.lastIndex;
            }

            {
                var slot = slotsPtr + index;
                slot->hashCode = (int)hashCode;
                slot->value = value;
                slot->next = *(bucketsPtr + bucket) - 1;
                *(bucketsPtr + bucket) = (int)(index + 1u);
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
        public void CopyFrom(ref MemoryAllocator allocator, in UIntHashSet other) {

            this.buckets.CopyFrom(ref allocator, other.buckets);
            this.slots.CopyFrom(ref allocator, other.slots);
            var thisBuckets = this.buckets;
            var thisSlots = this.slots;
            this = other;
            this.buckets = thisBuckets;
            this.slots = thisSlots;

        }

        public uint GetReservedSizeInBytes() {
            return this.buckets.GetReservedSizeInBytes() + this.slots.GetReservedSizeInBytes();
        }

    }

}
