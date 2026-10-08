namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Manages entity slots, generations and version storage within a world.
    /// </summary>
    public unsafe partial struct Ents {

        /// <summary>
        /// Defines entity components enumerator data used by entity processing.
        /// </summary>
        public struct EntityComponentsEnumerator {

            private safe_ptr<ulong> words;
            private uint wordsCount;
            private uint wordIndex;
            private ulong word;
            private uint current;

            [INLINE(256)]
            internal EntityComponentsEnumerator(safe_ptr<ulong> words, uint wordsCount) {
                this.words = words;
                this.wordsCount = wordsCount;
                this.wordIndex = 0u;
                this.word = wordsCount > 0u ? words[0u] : 0UL;
                this.current = 0u;
            }

            /// <summary>
            /// Element at the enumerator's current position.
            /// </summary>
            public uint Current {
                [INLINE(256)]
                get => this.current;
            }

            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            [INLINE(256)]
            public bool MoveNext() {
                while (true) {
                    if (this.word != 0UL) {
                        var bit = (uint)Unity.Mathematics.math.tzcnt(this.word);
                        this.current = (this.wordIndex << 6) + bit;
                        this.word &= this.word - 1UL;
                        return true;
                    }
                    ++this.wordIndex;
                    if (this.wordIndex >= this.wordsCount) return false;
                    this.word = this.words[this.wordIndex];
                }
            }

        }

        /// <summary>
        /// Entity to components words used by <c>Ents</c>.
        /// </summary>
        public uint entityToComponentsWords;
        /// <summary>
        /// Entity to components used by <c>Ents</c>.
        /// </summary>
        public MemArray<ulong> entityToComponents;
        /// <summary>
        /// Entity to components locks used by <c>Ents</c>.
        /// </summary>
        public MemArray<LockSpinner> entityToComponentsLocks;

        /// <summary>
        /// Resizes entity components.
        /// </summary>
        [INLINE(256)]
        public void ResizeEntityComponents(safe_ptr<State> state, uint entitiesCapacity) {
            if (this.entityToComponentsWords == 0u) this.entityToComponentsWords = Bitwise.GetLength(StaticTypes.counter + 1u);
            this.entityToComponents.Resize(ref state.ptr->allocator, entitiesCapacity * this.entityToComponentsWords, 2);
            this.entityToComponentsLocks.Resize(ref state.ptr->allocator, entitiesCapacity, 2);
        }

        /// <summary>
        /// Returns entity components lock.
        /// </summary>
        [INLINE(256)]
        public ref LockSpinner GetEntityComponentsLock(safe_ptr<State> state, uint entityId) {
            return ref this.entityToComponentsLocks[in state.ptr->allocator, entityId];
        }

        /// <summary>
        /// Returns entity components words.
        /// </summary>
        [INLINE(256)]
        public safe_ptr<ulong> GetEntityComponentsWords(safe_ptr<State> state, uint entityId) {
            return (safe_ptr<ulong>)this.entityToComponents.GetUnsafePtr(in state.ptr->allocator) + entityId * this.entityToComponentsWords;
        }

        /// <summary>
        /// Returns entity components enumerator.
        /// </summary>
        [INLINE(256)]
        public EntityComponentsEnumerator GetEntityComponentsEnumerator(safe_ptr<State> state, uint entityId) {
            return new EntityComponentsEnumerator(this.GetEntityComponentsWords(state, entityId), this.entityToComponentsWords);
        }

        /// <summary>
        /// Returns entity components count.
        /// </summary>
        [INLINE(256)]
        public uint GetEntityComponentsCount(safe_ptr<State> state, uint entityId) {
            ref var spinner = ref this.GetEntityComponentsLock(state, entityId);
            spinner.Lock();
            var words = this.GetEntityComponentsWords(state, entityId);
            var count = 0u;
            for (uint i = 0u; i < this.entityToComponentsWords; ++i) count += (uint)Unity.Mathematics.math.countbits(words[i]);
            spinner.Unlock();
            return count;
        }

        /// <summary>
        /// Clears entity components.
        /// </summary>
        [INLINE(256)]
        public void ClearEntityComponents(safe_ptr<State> state, uint entityId) {
            ref var spinner = ref this.GetEntityComponentsLock(state, entityId);
            spinner.Lock();
            Cuts._memclear(this.GetEntityComponentsWords(state, entityId), this.entityToComponentsWords * sizeof(ulong));
            spinner.Unlock();
        }

        /// <summary>
        /// Handles the add component callback.
        /// </summary>
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            var wordIndex = typeId >> 6;
            E.RANGE(wordIndex, 0u, this.entityToComponentsWords);
            ref var spinner = ref this.GetEntityComponentsLock(state, entityId);
            spinner.Lock();
            var words = this.GetEntityComponentsWords(state, entityId);
            words[wordIndex] |= 1UL << (int)(typeId & 63u);
            spinner.Unlock();
        }

        /// <summary>
        /// Handles the remove component callback.
        /// </summary>
        [INLINE(256)]
        public void OnRemoveComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            var wordIndex = typeId >> 6;
            E.RANGE(wordIndex, 0u, this.entityToComponentsWords);
            ref var spinner = ref this.GetEntityComponentsLock(state, entityId);
            spinner.Lock();
            var words = this.GetEntityComponentsWords(state, entityId);
            words[wordIndex] &= ~(1UL << (int)(typeId & 63u));
            spinner.Unlock();
        }

        /// <summary>
        /// Invokes the Burst implementation of mode entity components.
        /// </summary>
        [INLINE(256)]
        public void BurstModeEntityComponents(in MemoryAllocator allocator, bool mode) {
            this.entityToComponents.BurstMode(in allocator, mode);
            this.entityToComponentsLocks.BurstMode(in allocator, mode);
        }

        /// <summary>
        /// Returns entity components reserved size in bytes.
        /// </summary>
        [INLINE(256)]
        public uint GetEntityComponentsReservedSizeInBytes() {
            return this.entityToComponents.GetReservedSizeInBytes() + this.entityToComponentsLocks.GetReservedSizeInBytes();
        }

        /// <summary>
        /// Serializes headers flat queries.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            writer.Write(this.entityToComponentsWords);
            writer.Write(this.entityToComponents);
            writer.Write(this.entityToComponentsLocks);
        }

        /// <summary>
        /// Deserializes headers flat queries.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            reader.Read(ref this.entityToComponentsWords);
            reader.Read(ref this.entityToComponents);
            reader.Read(ref this.entityToComponentsLocks);
        }

    }
    
    /// <summary>
    /// Provides the legacy entity-storage operations retained by this implementation.
    /// </summary>
    public unsafe partial struct EntsOld {

        /// <summary>
        /// Defines locked entity to component data used by entity processing.
        /// </summary>
        public struct LockedEntityToComponent {

            /// <summary>
            /// Spin lock protecting concurrent access to this state.
            /// </summary>
            public LockSpinner lockSpinner;
            /// <summary>
            /// Entity handles processed or stored by this operation.
            /// </summary>
            public HashSet<uint> entities;
            
            /// <summary>
            /// Initializes <c>LockedEntityToComponent</c> from the supplied allocator, capacity.
            /// </summary>
            public LockedEntityToComponent(ref MemoryAllocator allocator, uint capacity) {
                this.lockSpinner = default;
                this.entities = new HashSet<uint>(ref allocator, capacity);
            }

        }
        /// <summary>
        /// Entity to components used by <c>EntsOld</c>.
        /// </summary>
        public MemArray<LockedEntityToComponent> entityToComponents;
        
        /// <summary>
        /// Handles the add component callback.
        /// </summary>
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Add(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }

        /// <summary>
        /// Handles the remove component callback.
        /// </summary>
        [INLINE(256)]
        public void OnRemoveComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Remove(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }

        /// <summary>
        /// Serializes headers flat queries.
        /// </summary>
        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            writer.Write(this.entityToComponents);
        }

        /// <summary>
        /// Deserializes headers flat queries.
        /// </summary>
        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            reader.Read(ref this.entityToComponents);
        }

    }

}
