namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public unsafe struct Archetypes {

        public struct Archetype {

            public UIntListHash entitiesList;
            public uint componentsCount;
            public UIntHashSet components;
            public BitArray componentBits;

            [INLINE(256)]
            public void BurstMode(in MemoryAllocator allocator, in bool state) {
                this.components.BurstMode(in allocator, state);
                this.componentBits.BurstMode(in allocator, state);
                this.entitiesList.BurstMode(in allocator, state);
            }

            [INLINE(256)]
            public static Archetype Create(State* state, uint componentsCount, uint entitiesCapacityPerArchetype) {
                return new Archetype() {
                    componentsCount = componentsCount,
                    components = new UIntHashSet(ref state->allocator, componentsCount),
                    componentBits = new BitArray(ref state->allocator, StaticTypes.counter + 1u),
                    entitiesList = new UIntListHash(ref state->allocator, entitiesCapacityPerArchetype),
                };
            }

            [INLINE(256)]
            public uint GetNext(State* state, uint entId, in ComponentsFastTrack addItems, in ComponentsFastTrack removeItems, ref Archetypes archetypes) {
                
                // Remove entity from previous
                this.RemoveEntity(state, entId);

                // Migrate to archetype with addItems and removeItems
                var removeItemsCount = removeItems.Count;
                if (this.componentsCount == 0u) {
                    // Migrating from zero archetype - ignore removeItems
                    removeItemsCount = 0u;
                }
                E.RANGE_INVERSE(this.componentsCount + addItems.Count, removeItemsCount);
                var targetCount = this.componentsCount - removeItemsCount + addItems.Count;
                if (targetCount == 0u) {
                    // Jump to zero archetype
                    archetypes.list[in state->allocator, 0].entitiesList.Add(ref state->allocator, entId);
                    return 0u;
                }

                var currentBits = new TempBitArray(in state->allocator, this.componentBits, Constants.ALLOCATOR_TEMP);
                currentBits.Union(addItems.root);
                currentBits.Remove(removeItems.root);
                
                if (targetCount < archetypes.componentsCountToArchetypeIds.Length) {
                    // Look up archetype by targetCount
                    var hash = addItems.hash ^ (this.components.hash ^ removeItems.hash);
                    ref var dic = ref archetypes.componentsCountToArchetypeIds[state, targetCount];
                    if (dic.isCreated == true) {
                        ref var list = ref dic.GetValue(ref state->allocator, hash, out var exist);
                        if (exist == true) {
                            //var fastComponentsRead = UIntHashSetRead.Create(in state->allocator, in this.components);
                            for (uint i = 0u; i < list.Count; ++i) {
                                var archIdx = list[in state->allocator, i];
                                ref var arch = ref archetypes.list[in state->allocator, archIdx];
                                if (arch.ContainsComponents(in state->allocator, in currentBits, in addItems, in removeItems) == true) {
                                    // Add entity to the new one
                                    arch.AddEntity(state, entId);
                                    return archIdx;
                                }
                            }
                        }
                    }
                }
                
                // Archetype not found - create the new one
                MemoryAllocatorExt.ValidateConsistency(state->allocator);
                var newArchetype = Archetype.Create(state, targetCount, 1u);
                newArchetype.components.CopyFrom(ref state->allocator, this.components);
                newArchetype.componentBits = new BitArray(ref state->allocator, this.componentBits);
                if (removeItems.Count > 0u) {
                    newArchetype.components.Remove(ref state->allocator, removeItems);
                    newArchetype.componentBits.Remove(in state->allocator, removeItems.root);
                }

                MemoryAllocatorExt.ValidateConsistency(state->allocator);
                if (addItems.Count > 0u) {
                    newArchetype.components.Add(ref state->allocator, addItems);
                    newArchetype.componentBits.Union(ref state->allocator, addItems.root);
                }

                MemoryAllocatorExt.ValidateConsistency(state->allocator);
                newArchetype.AddEntity(state, entId);
                MemoryAllocatorExt.ValidateConsistency(state->allocator);
                archetypes.Add(state, in newArchetype, out var newIdx);
                
                return newIdx;
            }

            [INLINE(256)]
            public void RemoveEntity(State* state, uint entId) {

                var idx = state->archetypes.entToIdxInArchetype[state, entId];
                var movedEntId = this.entitiesList[in state->allocator, this.entitiesList.Count - 1u];
                state->archetypes.entToIdxInArchetype[state, movedEntId] = idx;
                this.entitiesList.RemoveAtFast(in state->allocator, idx);
                
                /*this.entities.Remove(ref state->allocator, entId);
                var idx = this.entitiesToIdxInList.GetValueAndRemove(in state->allocator, entId);
                var movedEntId = this.idxInListToEntId.GetValueAndRemove(in state->allocator, this.entitiesList.Count - 1u);
                if (movedEntId != entId) {
                    this.entitiesToIdxInList[in state->allocator, movedEntId] = idx;
                    this.idxInListToEntId[in state->allocator, idx] = movedEntId;
                }
                this.entitiesList.RemoveAtFast(in state->allocator, idx);
                this.entitiesToIdxInList.Remove(in state->allocator, entId);*/

            }

            [INLINE(256)]
            public uint AddEntity(State* state, uint entId) {

                var idx = this.entitiesList.Count;
                this.entitiesList.Add(ref state->allocator, entId);
                state->archetypes.entToIdxInArchetype[state, entId] = idx;
                
                return idx;

            }

            [INLINE(256)]
            private bool ContainsComponents(in MemoryAllocator allocator,
                                            in TempBitArray componentBits,
                                            in ComponentsFastTrack containsItems,
                                            in ComponentsFastTrack notContainsItems) {

                if (this.componentBits.ContainsAll(in allocator, componentBits) == false /*||
                    this.componentBits.NotContainsAll(in allocator, notContainsItems.root) == false*/) {
                    return false;
                }
                return true;

            }

            public uint GetReservedSizeInBytes(State* state) {
                
                var size = 0u;
                size += this.components.GetReservedSizeInBytes();
                size += this.componentBits.GetReservedSizeInBytes();
                size += this.entitiesList.GetReservedSizeInBytes();
                return size;
                
            }

        }

        public int lockIndex;
        public List<Archetype> list;
        public List<uint> allArchetypes;
        public BitArray allArchetypesForQuery;
        public MemArray<uint> entToArchetypeIdx;
        public MemArray<uint> entToIdxInArchetype;
        // [componentsCount] => [hash, list[archIdx]]
        public MemArray<UIntDictionary<List<uint>>> componentsCountToArchetypeIds;
        public MemArray<BitArray> archetypesWithTypeIdBits;
        public uint Count => this.allArchetypes.Count;

        public uint GetReservedSizeInBytes(State* state) {

            if (this.list.isCreated == false) return 0u;

            var size = 0u;
            for (uint i = 0u; i < this.list.Count; ++i) {
                ref var arch = ref this.list[in state->allocator, i];
                size += arch.GetReservedSizeInBytes(state);
            }
            size += this.list.GetReservedSizeInBytes();
            size += this.allArchetypes.GetReservedSizeInBytes();
            size += this.allArchetypesForQuery.GetReservedSizeInBytes();
            size += this.entToArchetypeIdx.GetReservedSizeInBytes();
            size += this.entToIdxInArchetype.GetReservedSizeInBytes();
            size += this.componentsCountToArchetypeIds.GetReservedSizeInBytes();
            size += this.archetypesWithTypeIdBits.GetReservedSizeInBytes();
            for (uint i = 0u; i < this.componentsCountToArchetypeIds.Length; ++i) {
                ref var dic = ref this.componentsCountToArchetypeIds[in state->allocator, i];
                if (dic.isCreated == false) continue;
                var e = dic.GetEnumerator(state);
                while (e.MoveNext() == true) {
                    size += e.Current.value.GetReservedSizeInBytes();
                }
            }
            
            return size;
            
        }
        
        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool mode) {
            this.list.BurstMode(in allocator, mode);
            for (uint i = 0u; i < this.list.Count; ++i) {
                ref var arch = ref this.list[in allocator, i];
                arch.BurstMode(in allocator, mode);
            }
            this.allArchetypes.BurstMode(in allocator, mode);
            this.allArchetypesForQuery.BurstMode(in allocator, mode);
            this.entToArchetypeIdx.BurstMode(in allocator, mode);
            this.entToIdxInArchetype.BurstMode(in allocator, mode);
            this.componentsCountToArchetypeIds.BurstMode(in allocator, mode);
            this.archetypesWithTypeIdBits.BurstMode(in allocator, mode);
        }

        [INLINE(256)]
        internal ref Archetype Add(State* state, in Archetype archetype, out uint idx) {
            
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            ref var allocator = ref state->allocator;
            idx = this.list.Add(ref allocator, archetype);
            this.allArchetypes.Add(ref allocator, idx);
            this.allArchetypesForQuery.Resize(ref allocator, idx + 1u);
            this.allArchetypesForQuery.Set(in allocator, (int)idx, true);
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            { // collect with
                var e = archetype.components.GetEnumerator(in allocator);
                while (e.MoveNext() == true) {
                    var cId = e.Current;
                    {
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                        this.archetypesWithTypeIdBits.Resize(ref allocator, cId + 1u);
                        ref var list = ref this.archetypesWithTypeIdBits[in allocator, cId];
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                        if (list.isCreated == false) list = new BitArray(ref allocator, idx + 1u);
                        list.Resize(ref allocator, idx + 1u);
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                        list.Set(in allocator, (int)idx, true);
                        MemoryAllocatorExt.ValidateConsistency(state->allocator);
                    }
                }
            }
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            { // update all static queries
                ref var queries = ref state->queries.queryData;
                for (uint i = 0u; i < state->queries.nextId; ++i) {
                    ref var query = ref queries[in state->allocator, i];
                    query.Validate(state);
                }
            }
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            {
                this.componentsCountToArchetypeIds.Resize(ref allocator, archetype.componentsCount + 1u);
                ref var arr = ref this.componentsCountToArchetypeIds[in allocator, archetype.componentsCount];
                if (arr.isCreated == false) arr = new UIntDictionary<List<uint>>(ref allocator, 1u);
                var hash = archetype.components.hash;
                ref var list = ref arr.GetValue(ref allocator, hash, out var exist);
                if (exist == true && list.isCreated == true) {
                    list.Add(ref allocator, idx);
                } else {
                    list = new List<uint>(ref allocator, 1u);
                    list.Add(ref allocator, idx);
                }
            }
            return ref this.list[in allocator, idx];
            
        }

        [INLINE(256)]
        public void AddEntity(State* state, UnsafeList<Ent>* list, uint maxId) {

            this.entToArchetypeIdx.Resize(ref state->allocator, maxId + 1u);
            this.entToIdxInArchetype.Resize(ref state->allocator, maxId + 1u);

            for (int i = 0; i < list->Length; ++i) {
                var ent = list->ElementAt(i);
                this.list[in state->allocator, 0].AddEntity(state, ent.id);
            }

        }

        [INLINE(256)]
        public void AddEntity(State* state, in Ent ent) {

            E.IS_IN_TICK(state);
            
            this.entToArchetypeIdx.Resize(ref state->allocator, ent.id + 1u);
            this.entToIdxInArchetype.Resize(ref state->allocator, ent.id + 1u);
            this.list[in state->allocator, 0].AddEntity(state, ent.id);
            this.entToArchetypeIdx[in state->allocator, ent.id] = 0u;
            
        }

        [INLINE(256)]
        public void RemoveEntity(State* state, uint entId) {

            E.IS_IN_TICK(state);
            
            var idx = this.entToArchetypeIdx[in state->allocator, entId];
            this.list[in state->allocator, idx].RemoveEntity(state, entId);
            this.entToArchetypeIdx[in state->allocator, entId] = 0u;
            
        }

        [INLINE(256)]
        public void ApplyBatch(State* state, uint entId, in ComponentsFastTrack addItems, in ComponentsFastTrack removeItems) {

            if (addItems.Count > 0 || removeItems.Count > 0) {
                var idx = this.entToArchetypeIdx[in state->allocator, entId];
                ref var archetype = ref this.list[in state->allocator, idx];
                // Look up for new archetype
                this.entToArchetypeIdx[in state->allocator, entId] = archetype.GetNext(state, entId, in addItems, in removeItems, ref this);
            }

        }

        [INLINE(256)]
        public static Archetypes Create(State* state, uint capacity, uint entitiesCapacity) {
            if (capacity == 0u) capacity = 1u;
            var archs = new Archetypes() {
                list = new List<Archetype>(ref state->allocator, capacity),
                entToArchetypeIdx = new MemArray<uint>(ref state->allocator, capacity, growFactor: 2),
                entToIdxInArchetype = new MemArray<uint>(ref state->allocator, capacity, growFactor: 2),
                componentsCountToArchetypeIds = new MemArray<UIntDictionary<List<uint>>>(ref state->allocator, capacity, growFactor: 2),
                archetypesWithTypeIdBits = new MemArray<BitArray>(ref state->allocator, capacity, growFactor: 2),
                allArchetypes = new List<uint>(ref state->allocator, capacity),
                allArchetypesForQuery = new BitArray(ref state->allocator, capacity),
            };
            archs.list.Add(ref state->allocator, Archetype.Create(state, 0u, entitiesCapacity));
            archs.allArchetypes.Add(ref state->allocator, 0u);
            archs.allArchetypesForQuery.Set(in state->allocator, 0, true);
            archs.componentsCountToArchetypeIds[in state->allocator, 0] = default;
            return archs;
        }

    }

}