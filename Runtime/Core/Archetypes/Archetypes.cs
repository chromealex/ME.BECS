#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using CND = System.Diagnostics.ConditionalAttribute;
    
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
            public static Archetype Create(safe_ptr<State> state, uint componentsCount, uint entitiesCapacityPerArchetype) {
                return new Archetype() {
                    componentsCount = componentsCount,
                    components = new UIntHashSet(ref state.ptr->allocator, componentsCount),
                    componentBits = new BitArray(ref state.ptr->allocator, StaticTypes.counter + 1u),
                    entitiesList = new UIntListHash(ref state.ptr->allocator, entitiesCapacityPerArchetype),
                };
            }

            [INLINE(256)]
            public uint GetNext(safe_ptr<State> state, uint entId, in ComponentsFastTrack addItems, in ComponentsFastTrack removeItems, ref Archetypes archetypes) {
                
                // Migrate to archetype with addItems and removeItems
                var removeItemsCount = removeItems.Count;
                var removeItemsHash = removeItems.hash;
                if (this.componentsCount == 0u) {
                    // Migrating from zero archetype - ignore removeItems
                    removeItemsCount = 0u;
                    removeItemsHash = 0u;
                }
                E.RANGE_INVERSE(this.componentsCount + addItems.Count, removeItemsCount);
                var targetCount = this.componentsCount - removeItemsCount + addItems.Count;

                // Remove entity from previous
                this.RemoveEntity(state, entId);

                if (targetCount == 0u) {
                    // Jump to zero archetype
                    archetypes.list[in state.ptr->allocator, 0u].AddEntity(state, entId);
                    return 0u;
                }

                if (targetCount < archetypes.componentsCountToArchetypeIds.Length) {
                    // Look up archetype by targetCount
                    var hash = addItems.hash ^ (this.components.hash ^ removeItemsHash);
                    ref var dic = ref archetypes.componentsCountToArchetypeIds[state, targetCount];
                    if (dic.isCreated == true) {
                        ref var list = ref dic.GetValue(ref state.ptr->allocator, hash, out var exist);
                        if (exist == true) {
                            var currentBits = new TempBitArray(in state.ptr->allocator, this.componentBits, Constants.ALLOCATOR_TEMP);
                            currentBits.Union(addItems.root);
                            if (removeItemsCount > 0u) currentBits.Remove(removeItems.root);
                            //var fastComponentsRead = UIntHashSetRead.Create(in state.ptr->allocator, in this.components);
                            for (uint i = 0u; i < list.Count; ++i) {
                                var archIdx = list[in state.ptr->allocator, i];
                                ref var arch = ref archetypes.list[in state.ptr->allocator, archIdx];
                                if (arch.ContainsComponents(in state.ptr->allocator, in currentBits, in addItems, in removeItems) == true) {
                                    // Add entity to the found archetype
                                    arch.AddEntity(state, entId);
                                    return archIdx;
                                }
                            }
                        }
                    }
                }
                
                // Archetype not found - create the new one
                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                var newArchetype = Archetype.Create(state, targetCount, 1u);
                newArchetype.components.CopyFrom(ref state.ptr->allocator, this.components);
                newArchetype.componentBits.Set(ref state.ptr->allocator, this.componentBits);
                if (removeItems.Count > 0u) {
                    newArchetype.components.Remove(ref state.ptr->allocator, removeItems);
                    newArchetype.componentBits.Remove(in state.ptr->allocator, removeItems.root);
                }

                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                if (addItems.Count > 0u) {
                    newArchetype.components.Add(ref state.ptr->allocator, addItems);
                    newArchetype.componentBits.Union(ref state.ptr->allocator, addItems.root);
                }

                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                newArchetype.AddEntity(state, entId);
                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                Add(state, in newArchetype, out var newIdx);
                
                return newIdx;
            }

            [INLINE(256)]
            public void RemoveEntity(safe_ptr<State> state, uint entId) {

                var idx = state.ptr->archetypes.entToIdxInArchetype[state, entId];
                CheckExist(state, this);
                var movedEntId = this.entitiesList[in state.ptr->allocator, this.entitiesList.Count - 1u];
                state.ptr->archetypes.entToIdxInArchetype[state, movedEntId] = idx;
                this.entitiesList.RemoveAtFast(in state.ptr->allocator, idx);
                
            }

            [INLINE(256)]
            public uint AddEntity(safe_ptr<State> state, uint entId) {

                var idx = this.entitiesList.Count;
                this.entitiesList.Add(ref state.ptr->allocator, entId);
                state.ptr->archetypes.entToIdxInArchetype[state, entId] = idx;
                CheckExist(state, this);
                
                return idx;

            }

            [INLINE(256)]
            private bool ContainsComponents(in MemoryAllocator allocator,
                                            in TempBitArray componentBits,
                                            in ComponentsFastTrack containsItems,
                                            in ComponentsFastTrack notContainsItems) {

                if (this.componentBits.ContainsAll(in allocator, componentBits) == false/*||
                    this.componentBits.NotContainsAll(in allocator, notContainsItems.root) == false*/) {
                    return false;
                }
                return true;

            }

            public uint GetReservedSizeInBytes(safe_ptr<State> state) {
                
                var size = 0u;
                size += this.components.GetReservedSizeInBytes();
                size += this.componentBits.GetReservedSizeInBytes();
                size += this.entitiesList.GetReservedSizeInBytes();
                return size;
                
            }

        }

        private LockSpinner lockIndex;
        public List<Archetype> list;
        public List<uint> allArchetypes;
        public BitArray allArchetypesForQuery;
        public MemArray<uint> entToArchetypeIdx;
        public MemArray<uint> entToIdxInArchetype;
        // [componentsCount] => [hash, list[archIdx]]
        public MemArray<UIntDictionary<List<uint>>> componentsCountToArchetypeIds;
        public MemArray<BitArray> archetypesWithTypeIdBits;
        public uint Count => this.allArchetypes.Count;

        public uint GetReservedSizeInBytes(safe_ptr<State> state) {

            if (this.list.IsCreated == false) return 0u;

            var size = 0u;
            for (uint i = 0u; i < this.list.Count; ++i) {
                ref var arch = ref this.list[in state.ptr->allocator, i];
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
                ref var dic = ref this.componentsCountToArchetypeIds[in state.ptr->allocator, i];
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
            JobUtils.Lock(ref this.lockIndex);
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
            JobUtils.Unlock(ref this.lockIndex);
        }

        [INLINE(256)]
        internal static void Add(safe_ptr<State> state, in Archetype archetype, out uint idx) {
            
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
            ref var allocator = ref state.ptr->allocator;
            idx = state.ptr->archetypes.list.Add(ref allocator, archetype);
            var idxLength = idx + 1u;
            var len = math.max(idxLength, state.ptr->archetypes.allArchetypes.Capacity);
            state.ptr->archetypes.allArchetypes.Add(ref allocator, idx);
            // archetypesWithTypeIdBits[index] (bits) length must be less or equals than allArchetypesForQuery's length
            // because we use intersects method to find out bits overlapping, so we need to use len parameter in both
            state.ptr->archetypes.allArchetypesForQuery.Resize(ref allocator, len);
            state.ptr->archetypes.allArchetypesForQuery.Set(in allocator, (int)idx, true);
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
            { // collect with
                var e = archetype.components.GetEnumerator(in allocator);
                while (e.MoveNext() == true) {
                    var cId = e.Current;
                    {
                        MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                        ref var list = ref state.ptr->archetypes.archetypesWithTypeIdBits[in allocator, cId];
                        MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                        if (list.isCreated == false) list = new BitArray(ref allocator, len);
                        list.Resize(ref allocator, len);
                        MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                        list.Set(in allocator, (int)idx, true);
                        MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                    }
                }
            }
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
            { // update all static queries
                ref var queries = ref state.ptr->queries.queryData;
                for (uint i = 0u; i < state.ptr->queries.nextId; ++i) {
                    ref var query = ref queries[in state.ptr->allocator, i];
                    query.Validate(state);
                }
            }
            MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
            {
                state.ptr->archetypes.componentsCountToArchetypeIds.Resize(ref allocator, archetype.componentsCount + 1u, 2);
                ref var arr = ref state.ptr->archetypes.componentsCountToArchetypeIds[in allocator, archetype.componentsCount];
                if (arr.isCreated == false) arr = new UIntDictionary<List<uint>>(ref allocator, 1u);
                var hash = archetype.components.hash;
                ref var list = ref arr.GetValue(ref allocator, hash, out var exist);
                if (exist == true && list.IsCreated == true) {
                    list.Add(ref allocator, idx);
                } else {
                    list = new List<uint>(ref allocator, 1u);
                    list.Add(ref allocator, idx);
                }
            }
            
            CheckExist(state, archetype);

        }

        [INLINE(256)]
        public static void AddEntity(safe_ptr<State> state, UnsafeList<Ent>* list, uint maxId) {

            JobUtils.Lock(ref state.ptr->archetypes.lockIndex);
            state.ptr->archetypes.entToArchetypeIdx.Resize(ref state.ptr->allocator, maxId + 1u, 2);
            state.ptr->archetypes.entToIdxInArchetype.Resize(ref state.ptr->allocator, maxId + 1u, 2);

            for (int i = 0; i < list->Length; ++i) {
                var ent = list->ElementAt(i);
                state.ptr->archetypes.list[in state.ptr->allocator, 0].AddEntity(state, ent.id);
            }
            JobUtils.Unlock(ref state.ptr->archetypes.lockIndex);

        }

        [INLINE(256)]
        public static void AddEntity(safe_ptr<State> state, in Ent ent) {

            E.IS_IN_TICK(state);
            
            JobUtils.Lock(ref state.ptr->archetypes.lockIndex);
            CheckEntityTimes(state, ent.id, 0);
            CheckNoEntity(state, ent.id, 0);
            state.ptr->archetypes.entToArchetypeIdx.Resize(ref state.ptr->allocator, ent.id + 1u, 2);
            state.ptr->archetypes.entToIdxInArchetype.Resize(ref state.ptr->allocator, ent.id + 1u, 2);
            state.ptr->archetypes.list[in state.ptr->allocator, 0].AddEntity(state, ent.id);
            state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, ent.id] = 0u;
            CheckEntityTimes(state, ent.id, 1);
            CheckEntity(state, ent.id, 0);
            JobUtils.Unlock(ref state.ptr->archetypes.lockIndex);
            
        }

        [INLINE(256)]
        public static void RemoveEntity(safe_ptr<State> state, in Ent ent) {

            E.IS_IN_TICK(state);
            
            JobUtils.Lock(ref state.ptr->archetypes.lockIndex);
            CheckEntityTimes(state, ent.id, 1);
            var idx = state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, ent.id];
            CheckEntity(state, ent.id, (int)idx);
            state.ptr->archetypes.list[in state.ptr->allocator, idx].RemoveEntity(state, ent.id);
            state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, ent.id] = 0u;
            CheckNoEntity(state, ent.id, (int)idx);
            CheckNoEntity(state, ent.id, 0);
            CheckNoEntity((int)idx, state, ent.id);
            CheckEntityTimes(state, ent.id, 1);
            JobUtils.Unlock(ref state.ptr->archetypes.lockIndex);
            
        }

        [INLINE(256)]
        public static void ApplyBatch(safe_ptr<State> state, uint entId, in ComponentsFastTrack addItems, in ComponentsFastTrack removeItems) {

            if (addItems.Count > 0 || removeItems.Count > 0) {
                JobUtils.Lock(ref state.ptr->archetypes.lockIndex);
                CheckEntityTimes(state, entId, 1);
                var idx = state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, entId];
                ref var archetype = ref state.ptr->archetypes.list[in state.ptr->allocator, idx];
                // Look up for new archetype
                CheckEntity(state, entId, (int)idx);
                CheckArch(state, archetype, (int)idx);
                state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, entId] = archetype.GetNext(state, entId, in addItems, in removeItems, ref state.ptr->archetypes);
                CheckArch(state, archetype, (int)idx);
                CheckEntity(state, entId, (int)state.ptr->archetypes.entToArchetypeIdx[in state.ptr->allocator, entId]);
                CheckEntityTimes(state, entId, 1);
                JobUtils.Unlock(ref state.ptr->archetypes.lockIndex);
            }

        }

        [INLINE(256)]
        public static Archetypes Create(safe_ptr<State> state, uint capacity, uint entitiesCapacity) {
            if (capacity == 0u) capacity = 1u;
            if (capacity < StaticTypes.counter + 1u) capacity = StaticTypes.counter + 1u;
            var archs = new Archetypes() {
                list = new List<Archetype>(ref state.ptr->allocator, capacity),
                entToArchetypeIdx = new MemArray<uint>(ref state.ptr->allocator, entitiesCapacity),
                entToIdxInArchetype = new MemArray<uint>(ref state.ptr->allocator, entitiesCapacity),
                componentsCountToArchetypeIds = new MemArray<UIntDictionary<List<uint>>>(ref state.ptr->allocator, capacity),
                archetypesWithTypeIdBits = new MemArray<BitArray>(ref state.ptr->allocator, StaticTypes.counter + 1u),
                allArchetypes = new List<uint>(ref state.ptr->allocator, capacity),
                allArchetypesForQuery = new BitArray(ref state.ptr->allocator, capacity),
            };
            archs.list.Add(ref state.ptr->allocator, Archetype.Create(state, 0u, entitiesCapacity));
            archs.allArchetypes.Add(ref state.ptr->allocator, 0u);
            archs.allArchetypesForQuery.Set(in state.ptr->allocator, 0, true);
            archs.componentsCountToArchetypeIds[in state.ptr->allocator, 0] = default;
            return archs;
        }

        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckEntity(safe_ptr<State> state, uint entId, int archIdx) {

            //var isListed = state.ptr->archetypes.list[state.ptr->allocator, (uint)archIdx].entitiesList.Contains(state.ptr->allocator, entId);
            //if (isListed == false) {
            var listedIdx = -1;
            var cnt = 0;
            for (uint i = 0; i < state.ptr->archetypes.list.Count; ++i) {
                var item = state.ptr->archetypes.list[state, i];
                if (item.entitiesList.Contains(state.ptr->allocator, entId) == true) {
                    listedIdx = (int)i;
                    ++cnt;
                }
            }
            if (archIdx != listedIdx || cnt > 1) UnityEngine.Debug.LogError("EntId: " + entId + " belongs to " + archIdx + " but listed in " + listedIdx + " (cnt: " + cnt + ")");
            //}

        }

        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckEntityTimes(safe_ptr<State> state, uint entId, int times) {

            var listedIdx = -1;
            var cnt = 0;
            for (uint i = 0; i < state.ptr->archetypes.list.Count; ++i) {
                var item = state.ptr->archetypes.list[state, i];
                if (item.entitiesList.Contains(state.ptr->allocator, entId) == true) {
                    listedIdx = (int)i;
                    ++cnt;
                }
            }
            if (cnt > times) UnityEngine.Debug.LogError("EntId: " + entId + " listed in " + listedIdx + " and in other archs (cnt: " + cnt + ")");
            
        }

        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckNoEntity(safe_ptr<State> state, uint entId, int archIdx) {

            UnityEngine.Debug.Assert(state.ptr->archetypes.list[state.ptr->allocator, (uint)archIdx].entitiesList.Contains(state.ptr->allocator, entId) == false);

        }

        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckNoEntity(int listId, safe_ptr<State> state, uint entId) {

            var listedIdx = -1;
            var cnt = 0;
            for (uint i = 0; i < state.ptr->archetypes.list.Count; ++i) {
                var item = state.ptr->archetypes.list[state, i];
                if (item.entitiesList.Contains(state.ptr->allocator, entId) == true) {
                    listedIdx = (int)i;
                    ++cnt;
                }
            }
            if (listedIdx >= 0) UnityEngine.Debug.LogError("EntId: " + entId + " belongs to " + listId + " and listed in " + listedIdx + " (cnt: " + cnt + "), but it is required not be listed in any");
            
        }

        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckExist(safe_ptr<State> state, Archetype archetype) {

            var cnt = 0;
            for (uint i = 0; i < state.ptr->archetypes.list.Count; ++i) {
                var arch = state.ptr->archetypes.list[state, i];
                if (arch.componentsCount == archetype.componentsCount && arch.componentBits.ContainsAll(state.ptr->allocator, archetype.componentBits) == true) {
                    ++cnt;
                }
            }

            if (cnt > 1) {
                var str = new System.Collections.Generic.List<string>();
                for (uint i = 0; i < state.ptr->archetypes.list.Count; ++i) {
                    var arch = state.ptr->archetypes.list[state, i];
                    if (arch.componentsCount == archetype.componentsCount && arch.componentBits.ContainsAll(state.ptr->allocator, archetype.componentBits) == true) {
                        var array = new System.Collections.Generic.List<int>((int)arch.componentBits.Length);
                        for (var j = 0; j < arch.componentBits.Length; ++j) {
                            if (arch.componentBits.IsSet(in state.ptr->allocator, j) == true) array.Add(j);
                        }
                        var comps = new System.Collections.Generic.List<int>((int)arch.components.Count);
                        var e = arch.components.GetEnumerator(state);
                        while (e.MoveNext() == true) {
                             comps.Add((int)e.Current);
                        }
                        str.Add("#" + i + ". Components: " + arch.componentsCount + " :: " + arch.components.Count + ".\nBits: " + string.Join(", ", array) + "\nCmps: " + string.Join(", ", comps));
                    }
                }
                UnityEngine.Debug.LogError("Found archetypes with the same bits on board: \n" + string.Join("\n", str));
            }
            
        }
        
        [CND(COND.ARCHETYPES_INTERNAL_CHECKS)]
        private static void CheckArch(safe_ptr<State> state, Archetype archetype, int idx = -1) {
            var archetypes = state.ptr->archetypes;
            for (uint i = 0; i < archetype.entitiesList.Count; ++i) {
                var ent = archetype.entitiesList[state.ptr->allocator, i];
                if (archetypes.entToIdxInArchetype[state, ent] != i) {
                    throw new System.Exception("Arch test failed: index " + i + ", entId: " + ent + ":" + archetypes.entToIdxInArchetype[state, ent] + ", arch idx: " + idx + ", arch: " + ListStr(state, archetypes, archetype.entitiesList));
                }
            }
        }

        private static string ListStr(safe_ptr<State> state, Archetypes archetypes, UIntListHash entitiesList) {
            var str = string.Empty;
            for (uint i = 0; i < entitiesList.Count; ++i) {
                var ent = entitiesList[state.ptr->allocator, i];
                str += ", " + ent + ":" + archetypes.entToIdxInArchetype[state, ent];
            }

            return str;
        }

        private static string ListStr(safe_ptr<State> state, UIntHashSet componentsList) {
            var str = string.Empty;
            var e = componentsList.GetEnumerator(state);
            while (e.MoveNext() == true) {
                str += ", " + e.Current;
            }

            return str;
        }

    }

}