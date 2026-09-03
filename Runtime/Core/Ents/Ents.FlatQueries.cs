namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    public unsafe partial struct Ents {

        #if ENABLE_BECS_FLAT_QUERIES
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

            public uint Current {
                [INLINE(256)]
                get => this.current;
            }

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

        public uint entityToComponentsWords;
        public MemArray<ulong> entityToComponents;
        public MemArray<LockSpinner> entityToComponentsLocks;

        [INLINE(256)]
        public void ResizeEntityComponents(safe_ptr<State> state, uint entitiesCapacity) {
            if (this.entityToComponentsWords == 0u) this.entityToComponentsWords = Bitwise.GetLength(StaticTypes.counter + 1u);
            this.entityToComponents.Resize(ref state.ptr->allocator, entitiesCapacity * this.entityToComponentsWords, 2);
            this.entityToComponentsLocks.Resize(ref state.ptr->allocator, entitiesCapacity, 2);
        }

        [INLINE(256)]
        public ref LockSpinner GetEntityComponentsLock(safe_ptr<State> state, uint entityId) {
            return ref this.entityToComponentsLocks[in state.ptr->allocator, entityId];
        }

        [INLINE(256)]
        public safe_ptr<ulong> GetEntityComponentsWords(safe_ptr<State> state, uint entityId) {
            return (safe_ptr<ulong>)this.entityToComponents.GetUnsafePtr(in state.ptr->allocator) + entityId * this.entityToComponentsWords;
        }

        [INLINE(256)]
        public EntityComponentsEnumerator GetEntityComponentsEnumerator(safe_ptr<State> state, uint entityId) {
            return new EntityComponentsEnumerator(this.GetEntityComponentsWords(state, entityId), this.entityToComponentsWords);
        }

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

        [INLINE(256)]
        public void ClearEntityComponents(safe_ptr<State> state, uint entityId) {
            ref var spinner = ref this.GetEntityComponentsLock(state, entityId);
            spinner.Lock();
            Cuts._memclear(this.GetEntityComponentsWords(state, entityId), this.entityToComponentsWords * sizeof(ulong));
            spinner.Unlock();
        }

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

        [INLINE(256)]
        public void BurstModeEntityComponents(in MemoryAllocator allocator, bool mode) {
            this.entityToComponents.BurstMode(in allocator, mode);
            this.entityToComponentsLocks.BurstMode(in allocator, mode);
        }

        [INLINE(256)]
        public uint GetEntityComponentsReservedSizeInBytes() {
            return this.entityToComponents.GetReservedSizeInBytes() + this.entityToComponentsLocks.GetReservedSizeInBytes();
        }
        #endif

        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            #if ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.entityToComponentsWords);
            writer.Write(this.entityToComponents);
            writer.Write(this.entityToComponentsLocks);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            #if ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.entityToComponentsWords);
            reader.Read(ref this.entityToComponents);
            reader.Read(ref this.entityToComponentsLocks);
            #endif
        }

    }
    
    public unsafe partial struct EntsOld {

        #if ENABLE_BECS_FLAT_QUERIES
        public struct LockedEntityToComponent {

            public LockSpinner lockSpinner;
            public HashSet<uint> entities;
            
            public LockedEntityToComponent(ref MemoryAllocator allocator, uint capacity) {
                this.lockSpinner = default;
                this.entities = new HashSet<uint>(ref allocator, capacity);
            }

        }
        public MemArray<LockedEntityToComponent> entityToComponents;
        
        [INLINE(256)]
        public void OnAddComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Add(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }

        [INLINE(256)]
        public void OnRemoveComponent(safe_ptr<State> state, uint entityId, uint typeId) {
            ref var list = ref this.entityToComponents[in state.ptr->allocator, entityId];
            list.lockSpinner.Lock();
            list.entities.Remove(ref state.ptr->allocator, typeId);
            list.lockSpinner.Unlock();
        }
        #endif

        [INLINE(256)]
        public void SerializeHeadersFlatQueries(ref StreamBufferWriter writer) {
            #if ENABLE_BECS_FLAT_QUERIES
            writer.Write(this.entityToComponents);
            #endif
        }

        [INLINE(256)]
        public void DeserializeHeadersFlatQueries(ref StreamBufferReader reader) {
            #if ENABLE_BECS_FLAT_QUERIES
            reader.Read(ref this.entityToComponents);
            #endif
        }

    }

}
