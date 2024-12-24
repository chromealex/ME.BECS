namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [System.Diagnostics.DebuggerTypeProxyAttribute(typeof(UIntHashSetProxy))]
    public unsafe struct UIntHashSet : IIsCreated {

        public struct Enumerator {

            private int lastIndex;
            private int index;
            private uint current;
            private safe_ptr<Slot> slotsPtr;

            [INLINE(256)]
            internal Enumerator(in UIntHashSet set, safe_ptr<State> state) {
                this.lastIndex = set.lastIndex;
                this.index = 0;
                this.slotsPtr = (safe_ptr<Slot>)set.slots.GetUnsafePtrCached(in state.ptr->allocator);
                this.current = default;
            }

            [INLINE(256)]
            internal Enumerator(in UIntHashSet set, MemoryAllocator allocator) {
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

                this.index = this.lastIndex + 1;
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
        
        internal MemArray<int> buckets;
        internal MemArray<Slot> slots;
        internal int count;
        internal int lastIndex;
        internal int freeList;
        internal int version;
        public uint hash;
        
        public bool IsCreated {
            [INLINE(256)]
            get => this.buckets.IsCreated;
        }

        public uint Count {
            [INLINE(256)]
            get => (uint)this.count;
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
            this.buckets = new MemArray<int>(ref allocator, other.buckets);
            this.slots = new MemArray<Slot>(ref allocator, other.slots);

        }

        [INLINE(256)]
        public bool Equals(in MemoryAllocator allocator, in UIntHashSet other) {

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
        public void Set(ref MemoryAllocator allocator, in UIntHashSet other) {
            
            this = other;
            this.buckets = new MemArray<int>(ref allocator, other.buckets);
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
                this.slots.Clear(ref allocator, 0, (uint)this.lastIndex);
                this.buckets.Clear(ref allocator, 0, this.buckets.Length);
                this.lastIndex = 0;
                this.count = 0;
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
            if (this.buckets.IsCreated == true) {
                int hashCode = item.GetHashCode() & UIntHashSet.LOWER31_BIT_MASK;
                var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtr(in allocator);
                var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtr(in allocator);
                // see note at "HashSet" level describing why "- 1" appears in for loop
                for (int i = bucketsPtr[hashCode % (int)this.buckets.Length] - 1; i >= 0; i = slotsPtr[i].next) {
                    if (slotsPtr[i].hashCode == hashCode && slotsPtr[i].value == item) {
                        return true;
                    }
                }
            }
            // either buckets is null or wasn't found
            return false;
        }
        
        [INLINE(256)]
        public readonly bool Contains(uint item, safe_ptr<Slot> slotsPtr, safe_ptr<int> bucketsPtr) {
            uint hashCode = item & UIntHashSet.LOWER31_BIT_MASK;
            for (int i = bucketsPtr[hashCode % this.buckets.Length] - 1; i >= 0; i = (slotsPtr + i).ptr->next) {
                if ((slotsPtr + i).ptr->hashCode == hashCode &&
                    (slotsPtr + i).ptr->value == item) {
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
        public void Remove(ref MemoryAllocator allocator, in BatchList other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
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
        public uint Remove(ref MemoryAllocator allocator, in ComponentsFastTrack other) {
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
            var list = other.root;
            var removedCount = 0u;
            for (int i = 0; i < this.lastIndex; i++) {
                if ((slotsPtr + i).ptr->hashCode >= 0) {
                    // cache value in case delegate removes it
                    var value = (slotsPtr + i).ptr->value;
                    if (list.IsSet((int)value) == true) {
                        // check again that remove actually removed it
                        if (this.Remove(ref allocator, value) == true) {
                            ++removedCount;
                        }
                    }
                }
            }

            return removedCount;
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
            var bucketsPtr = (safe_ptr<int>)this.buckets.GetUnsafePtrCached(in allocator);
            var slotsPtr = (safe_ptr<Slot>)this.slots.GetUnsafePtrCached(in allocator);
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
        public void Add(ref MemoryAllocator allocator, in UIntHashSet other) {
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
        public bool Remove(ref MemoryAllocator allocator, uint item) {
            if (this.buckets.IsCreated == true) {
                int hashCode = item.GetHashCode() & UIntHashSet.LOWER31_BIT_MASK;
                int bucket = hashCode % (int)this.buckets.Length;
                int last = -1;
                var buckets = (safe_ptr<int>)this.buckets.GetUnsafePtr(in allocator);
                var slots = (safe_ptr<Slot>)this.slots.GetUnsafePtr(in allocator);
                for (int i = buckets[bucket] - 1; i >= 0; last = i, i = slots[i].next) {
                    if (slots[i].hashCode == hashCode && slots[i].value == item) {
                        if (last < 0) {
                            // first iteration; update buckets
                            buckets[bucket] = slots[i].next + 1;
                        } else {
                            // subsequent iterations; update 'next' pointers
                            slots[last].next = slots[i].next;
                        }
                        slots[i].hashCode = -1;
                        slots[i].value = default;
                        slots[i].next = this.freeList;

                        this.hash ^= item;
                        this.count--;
                        this.version++;
                        if (this.count == 0) {
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
            this.buckets = new MemArray<int>(ref allocator, size);
            this.slots = new MemArray<Slot>(ref allocator, size);
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
            uint newSize = HashHelpers.ExpandPrime((uint)this.count);
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
                NativeArrayUtils.CopyNoChecks(ref allocator, in this.slots, 0, ref newSlots, 0, (uint)this.lastIndex);
            }

            if (forceNewHashCodes == true) {
                for(int i = 0; i < this.lastIndex; i++) {
                    if(newSlots[in allocator, i].hashCode != -1) {
                        newSlots[in allocator, i].hashCode = newSlots[in allocator, i].value.GetHashCode() & UIntHashSet.LOWER31_BIT_MASK;
                    }
                }
            }

            var newBuckets = new MemArray<int>(ref allocator, newSize);
            for (int i = 0; i < this.lastIndex; ++i) {
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
        public bool Add(ref MemoryAllocator allocator, uint value) {
            var buckets = (safe_ptr<int>)this.buckets.GetUnsafePtr(in allocator);
            var slots = (safe_ptr<Slot>)this.slots.GetUnsafePtr(in allocator);
            return this.Add(ref allocator, value, ref buckets, ref slots);
        }
        
        [INLINE(256)]
        public bool Add(ref MemoryAllocator allocator, uint value, ref safe_ptr<int> buckets, ref safe_ptr<Slot> slots) {
            
            if (this.buckets.IsCreated == false) {
                this.Initialize(ref allocator, 0);
            }

            int hashCode = value.GetHashCode() & UIntHashSet.LOWER31_BIT_MASK;
            int bucket = hashCode % (int)this.buckets.Length;
            for (int i = buckets[bucket] - 1; i >= 0; i = slots[i].next) {
                if (slots[i].hashCode == hashCode && slots[i].value == value) {
                    return false;
                }
            }

            this.hash ^= value;
            
            int index;
            if (this.freeList >= 0) {
                index = this.freeList;
                this.freeList = slots[index].next;
            } else {
                if (this.lastIndex == this.slots.Length) {
                    this.IncreaseCapacity(ref allocator);
                    // this will change during resize
                    bucket = hashCode % (int)this.buckets.Length;
                    buckets = (safe_ptr<int>)this.buckets.GetUnsafePtr(in allocator);
                    slots = (safe_ptr<Slot>)this.slots.GetUnsafePtr(in allocator);
                }
                index = this.lastIndex;
                ++this.lastIndex;
            }
            slots[index].hashCode = hashCode;
            slots[index].value = value;
            slots[index].next = buckets[bucket] - 1;
            buckets[bucket] = index + 1;
            ++this.count;
            ++this.version;

            return true;
            
        }

        [INLINE(256)]
        public void CopyFrom(ref MemoryAllocator allocator, in UIntHashSet other) {

            NativeArrayUtils.CopyExact(ref allocator, in other.buckets, ref this.buckets);
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
