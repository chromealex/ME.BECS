namespace ME.BECS {
    
    using System.Threading;
    using Unity.Mathematics;
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    internal static class EntityTypesManaged {

        public static readonly System.Collections.Generic.Dictionary<ushort, System.Type> typeByGroupId = new System.Collections.Generic.Dictionary<ushort, System.Type>();

    }

    public class EntityTypes {

        private static readonly Unity.Burst.SharedStatic<uint> groupsCountData = Unity.Burst.SharedStatic<uint>.GetOrCreate<EntityTypes>();
        public static ref uint groupsCount => ref groupsCountData.Data;

        public static void Init() {
            EntityTypesManaged.typeByGroupId.Clear();
        }

        public static void Register<T>(ushort id) where T : unmanaged, IEntityType {
            EntityTypes<T>.id = id;
            EntityTypesManaged.typeByGroupId.Add(id, typeof(T));
        }

    }
    
    public class EntityTypes<T> where T : unmanaged, IEntityType {

        private static readonly Unity.Burst.SharedStatic<ushort> idData = Unity.Burst.SharedStatic<ushort>.GetOrCreate<EntityTypes<T>>();
        public static ref ushort id => ref idData.Data;

    }
    
    public interface IEntityType { }

    public unsafe partial struct Ents {

        public const uint ENTITIES_PER_PAGE = sizeof(uint) * 8;

        public struct Group {

            public uint free;
            public uint offset;
            public bool IsEmpty => this.free == 0u;
            public uint Count => ENTITIES_PER_PAGE - (uint)math.countbits(this.free);

            [INLINE(256)]
            public static Group Create(ref uint freeCount, uint offset) {
                JobUtils.Increment(ref freeCount, ENTITIES_PER_PAGE);
                var group = new Group() {
                    offset = offset,
                    free = uint.MaxValue,
                };
                return group;
            }

            [INLINE(256)]
            public bool TryNew(out uint id) {
                while (true) {
                    var free = Volatile.Read(ref this.free);
                    if (free == 0u) {
                        id = 0u;
                        return false;
                    }
                    var bit = math.tzcnt(free);
                    var mask = 1u << bit;
                    var newFree = free & ~mask;
                    if (JobUtils.CompareExchange(ref this.free, newFree, free) == free) {
                        id = this.offset + (uint)bit;
                        return true;
                    }
                }
            }

            [INLINE(256)]
            public void Delete(uint id) {
                var bit = (int)(id - this.offset);
                var mask = 1u << bit;
                while (true) {
                    var free = Volatile.Read(ref this.free);
                    var newFree = free | mask;
                    if (JobUtils.CompareExchange(ref this.free, newFree, free) == free) {
                        return;
                    }
                }
            }

        }

        public struct Groups {

            public List<Group> groups;
            public List<uint> freeGroups;
            public HashSet<uint> freeGroupsHas;
            public ReadWriteSpinner resizeLock;

            public uint Count(World world) {
                var cnt = 0u;
                for (uint i = 0u; i < this.groups.Count; ++i) {
                    var group = this.groups[world.state, i];
                    cnt += group.Count;
                }
                return cnt;
            }

            [INLINE(256)]
            public static Groups Create(safe_ptr<State> state, uint initGroupsCount) {
                return new Groups() {
                    groups = new List<Group>(ref state.ptr->allocator, initGroupsCount),
                    freeGroupsHas = new HashSet<uint>(ref state.ptr->allocator, initGroupsCount),
                    freeGroups = new List<uint>(ref state.ptr->allocator, initGroupsCount),
                    resizeLock = ReadWriteSpinner.Create(state),
                };
            }

            [INLINE(256)]
            private bool TryNew(safe_ptr<State> state, JobInfo jobInfo, uint groupId, out uint id, out uint groupIndex, out uint freeGroupIndex) {
                id = 0u;
                groupIndex = uint.MaxValue;
                freeGroupIndex = 0u;
                if (this.freeGroups.Count == 0u) return false;
                var offset = jobInfo.GetOffset(groupId);
                for (uint i = 0; i < this.freeGroups.Count; ++i) {
                    freeGroupIndex = offset % this.freeGroups.Count;
                    var groupIdx = this.freeGroups[state, freeGroupIndex];
                    ref var group = ref this.groups[state, groupIdx];
                    if (group.TryNew(out var entId) == true) {
                        id = entId;
                        groupIndex = groupIdx;
                        return true;
                    }
                }

                id = default;
                return false;
            }

            [INLINE(256)]
            public uint New(safe_ptr<State> state, JobInfo jobInfo, uint groupId, ref uint nextGroupId, out uint localGroupIndex, out bool reuse) {
                
                if (state.ptr->entities.freeCount > 0u) {
                    this.resizeLock.WriteBegin(state);
                    if (this.TryNew(state, jobInfo, groupId, out var entId, out var usedGroupIndex, out var freeGroupIndex) == true) {
                        localGroupIndex = usedGroupIndex;
                        var group = this.groups[state, usedGroupIndex];
                        if (group.IsEmpty == true) {
                            this.freeGroups.RemoveAt(ref state.ptr->allocator, freeGroupIndex);
                            this.freeGroupsHas.Remove(ref state.ptr->allocator, usedGroupIndex);
                        }
                        this.resizeLock.WriteEnd();
                        reuse = true;
                        return entId;
                    }
                    this.resizeLock.WriteEnd();
                }

                {
                    if (JobUtils.IsInParallelJob() == true) {
                        throw new System.Exception("EnsureFree must be called before parallel job");
                    }
                    reuse = false;
                    // create new group
                    this.resizeLock.WriteBegin(state);
                    if (state.ptr->entities.freeCount > 0u && this.TryNew(state, jobInfo, groupId, out var entId, out var usedGroupIndex, out var freeGroupIndex) == true) {
                        localGroupIndex = usedGroupIndex;
                        var group = this.groups[state, usedGroupIndex];
                        if (group.IsEmpty == true) {
                            this.freeGroups.RemoveAt(ref state.ptr->allocator, freeGroupIndex);
                            this.freeGroupsHas.Remove(ref state.ptr->allocator, usedGroupIndex);
                        }
                        this.resizeLock.WriteEnd();
                        return entId;
                    }

                    {
                        var nextId = JobUtils.Increment(ref nextGroupId) - 1;
                        var group = Group.Create(ref state.ptr->entities.freeCount, nextId * ENTITIES_PER_PAGE);
                        group.TryNew(out var id);
                        this.groups.Add(ref state.ptr->allocator, group);
                        var idx = this.groups.Count - 1u;
                        localGroupIndex = idx;
                        this.freeGroups.Add(ref state.ptr->allocator, idx);
                        this.freeGroupsHas.Add(ref state.ptr->allocator, idx);
                        this.resizeLock.WriteEnd();
                        return id;
                    }
                }
                
            }

            [INLINE(256)]
            public void Delete(safe_ptr<State> state, uint entId) {
                this.resizeLock.ReadBegin(state);
                var localGroupIndex = state.ptr->entities.entityToGroupLocal[state, entId];
                ref var group = ref this.groups[state, localGroupIndex];
                group.Delete(entId);
                this.resizeLock.ReadEnd(state);
                this.resizeLock.WriteBegin(state);
                if (this.freeGroupsHas.Add(ref state.ptr->allocator, localGroupIndex) == true) {
                    this.freeGroups.Add(ref state.ptr->allocator, localGroupIndex);
                }
                this.resizeLock.WriteEnd();
            }

            public uint GetReservedSizeInBytes(safe_ptr<State> state) {
                var size = 0u;
                size += this.groups.GetReservedSizeInBytes();
                size += this.freeGroups.GetReservedSizeInBytes();
                size += this.freeGroupsHas.GetReservedSizeInBytes();
                return size;
            }

        }
        
        public MemArray<Groups> groupByEntityType;
        public MemArray<ushort> generations;
        public MemArray<ushort> entityToGroup;
        public MemArray<uint> entityToGroupLocal;
        public MemArray<uint> versions;
        public MemArray<uint> seeds;
        public MemArray<ushort> versionsGroup;
        public MemArray<LockSpinner> locksPerEntity;
        public List<uint> destroyed;
        public LockSpinner destroyedLock;
        public LockSpinner prewarmLock;
        public BitArray aliveBits;

        public uint aliveCount;
        public uint freeCount;
        public uint nextGroupId;
        public ReadWriteSpinner resizeLock;

        [INLINE(256)]
        public void SerializeHeaders(ref StreamBufferWriter writer) {
            writer.Write(this.generations);
            writer.Write(this.versions);
            writer.Write(this.seeds);
            writer.Write(this.versionsGroup);
            writer.Write(this.entityToGroup);
            writer.Write(this.entityToGroupLocal);
            writer.Write(this.groupByEntityType);
            writer.Write(this.nextGroupId);
            writer.Write(this.resizeLock);
            writer.Write(this.locksPerEntity);
            writer.Write(this.aliveCount);
            writer.Write(this.freeCount);
            writer.Write(this.destroyed);
            writer.Write(this.destroyedLock);
            writer.Write(this.prewarmLock);
            writer.Write(this.aliveBits);
            this.SerializeHeadersFlatQueries(ref writer);
        }

        [INLINE(256)]
        public void DeserializeHeaders(ref StreamBufferReader reader) {
            reader.Read(ref this.generations);
            reader.Read(ref this.versions);
            reader.Read(ref this.seeds);
            reader.Read(ref this.versionsGroup);
            reader.Read(ref this.entityToGroup);
            reader.Read(ref this.entityToGroupLocal);
            reader.Read(ref this.groupByEntityType);
            reader.Read(ref this.nextGroupId);
            reader.Read(ref this.resizeLock);
            reader.Read(ref this.locksPerEntity);
            reader.Read(ref this.aliveCount);
            reader.Read(ref this.freeCount);
            reader.Read(ref this.destroyed);
            reader.Read(ref this.destroyedLock);
            reader.Read(ref this.prewarmLock);
            reader.Read(ref this.aliveBits);
            this.DeserializeHeadersFlatQueries(ref reader);
        }
        
        public uint Capacity => this.generations.Length;
        public uint EntitiesCount => this.aliveCount;
        public uint FreeCount => this.freeCount;
        public int Hash => Utils.Hash(this.FreeCount, this.EntitiesCount, this.nextGroupId);

        public uint GetEntitiesCount<T>(World world) where T : unmanaged, IEntityType {
            return this.groupByEntityType[world.state, EntityTypes<T>.id].Count(world);
        }

        public uint GetReservedSizeInBytes(safe_ptr<State> state) {
            if (this.generations.IsCreated == false) return 0u;

            var size = TSize<Ents>.size;
            size += this.generations.GetReservedSizeInBytes();
            size += this.versions.GetReservedSizeInBytes();
            size += this.seeds.GetReservedSizeInBytes();
            size += this.versionsGroup.GetReservedSizeInBytes();
            size += this.entityToGroup.GetReservedSizeInBytes();
            size += this.entityToGroupLocal.GetReservedSizeInBytes();
            for (uint i = 0u; i < this.groupByEntityType.Length; ++i) {
                size += this.groupByEntityType[state, i].GetReservedSizeInBytes(state);
            }
            size += this.groupByEntityType.GetReservedSizeInBytes();
            size += this.destroyed.GetReservedSizeInBytes();
            size += this.aliveBits.GetReservedSizeInBytes();
            
            return size;
        }

        [INLINE(256)]
        public static void PrewarmBegin(safe_ptr<State> state) {
            state.ptr->entities.prewarmLock.Lock();
        }

        [INLINE(256)]
        public static void PrewarmEnd(safe_ptr<State> state) {
            state.ptr->entities.prewarmLock.Unlock();
        }

        [INLINE(256)]
        public static void Lock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Lock();
        }

        [INLINE(256)]
        public static void Unlock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Unlock();
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool mode) {
            this.generations.BurstMode(in allocator, mode);
            this.versions.BurstMode(in allocator, mode);
            this.seeds.BurstMode(in allocator, mode);
            this.versionsGroup.BurstMode(in allocator, mode);
            this.groupByEntityType.BurstMode(in allocator, mode);
            this.entityToGroup.BurstMode(in allocator, mode);
            this.locksPerEntity.BurstMode(in allocator, mode);
            this.destroyed.BurstMode(in allocator, mode);
            this.aliveBits.BurstMode(in allocator, mode);
        }

        [NotThreadSafe]
        [INLINE(256)]
        public static Ents Create(safe_ptr<State> state, uint groupsCount, uint entitiesCapacity) {
            var ents = new Ents();
            ents.Init(state, groupsCount, entitiesCapacity);
            return ents;
        }
        
        [NotThreadSafe]
        [INLINE(256)]
        public void Init(safe_ptr<State> state, uint groupsCount, uint entitiesCapacity) {
            var initGroupsCount = entitiesCapacity / ENTITIES_PER_PAGE;
            this.resizeLock = ReadWriteSpinner.Create(state);
            this.groupByEntityType = new MemArray<Groups>(ref state.ptr->allocator, groupsCount + 1u);
            Resize(state, ref this, entitiesCapacity);
            this.destroyedLock = default;
            this.nextGroupId = 0u;
            this.freeCount = 0u;
            this.aliveCount = 0u;
            for (uint i = 0u; i < groupsCount + 1u; ++i) {
                this.groupByEntityType[state, i] = Groups.Create(state, initGroupsCount);
            }
        }

        [NotThreadSafe]
        [INLINE(256)]
        public void SetCapacity(safe_ptr<State> state, ushort groupId, uint entitiesCapacity) {
            var initGroupsCount = entitiesCapacity / ENTITIES_PER_PAGE;
            ref var groups = ref this.groupByEntityType[state, groupId];
            groups.groups.Resize(ref state.ptr->allocator, initGroupsCount);
            for (uint i = 0u; i < initGroupsCount; ++i) {
                groups.groups.Add(ref state.ptr->allocator, Group.Create(ref this.freeCount, i * ENTITIES_PER_PAGE));
                groups.freeGroupsHas.Add(ref state.ptr->allocator, i);
                groups.freeGroups.Add(ref state.ptr->allocator, i);
            }
            this.nextGroupId += initGroupsCount;
        }

        [NotThreadSafe]
        [INLINE(256)]
        public static uint EnsureFree(safe_ptr<State> state, uint groupId, uint required) {
            
            E.IS_IN_TICK(state);

            var free = state.ptr->entities.FreeCount;
            if (free >= required) {
                return 0u;
            }

            var need = required - free;
            var groupsNeeded = (uint)math.ceil(need / (float)ENTITIES_PER_PAGE);
            ref var group = ref state.ptr->entities.groupByEntityType[state, groupId];
            var newGroupsCount = group.groups.Count + groupsNeeded;
            group.resizeLock.WriteBegin(state);
            group.groups.Resize(ref state.ptr->allocator, newGroupsCount);
            var currentGroups = group.groups.Count;
            for (uint i = currentGroups; i < newGroupsCount; ++i) {
                var nextId = JobUtils.Increment(ref state.ptr->entities.nextGroupId) - 1;
                var g = Group.Create(ref state.ptr->entities.freeCount, nextId * ENTITIES_PER_PAGE);
                group.groups.Add(ref state.ptr->allocator, g);
                var idx = group.groups.Count - 1u;
                group.freeGroupsHas.Add(ref state.ptr->allocator, idx);
                group.freeGroups.Add(ref state.ptr->allocator, idx);
            }
            group.resizeLock.WriteEnd();

            state.ptr->entities.resizeLock.WriteBegin(state);
            Resize(state, ref state.ptr->entities, newGroupsCount * ENTITIES_PER_PAGE);
            state.ptr->entities.resizeLock.WriteEnd();

            return newGroupsCount * ENTITIES_PER_PAGE;
            
        }
        
        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, in Ent ent) {
            return IsAlive(state, ent.id, ent.gen);
        }
        
        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, uint entId, ushort gen) {
            if (entId >= state.ptr->entities.generations.Length || gen == 0) return false;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var result = state.ptr->entities.generations[state, entId] == gen;
            state.ptr->entities.resizeLock.ReadEnd(state);
            return result;
        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent) {
            
            E.IS_IN_TICK(state);

            JobUtils.Decrement(ref state.ptr->entities.aliveCount);
            state.ptr->entities.resizeLock.ReadBegin(state);
            ++state.ptr->entities.generations[state, ent.id];
            state.ptr->entities.aliveBits.SetThreaded(in state.ptr->allocator, ent.id, false);
            state.ptr->entities.resizeLock.ReadEnd(state);

            state.ptr->entities.destroyedLock.Lock();
            state.ptr->entities.destroyed.Add(ref state.ptr->allocator, ent.id);
            state.ptr->entities.destroyedLock.Unlock();
            
        }
        
        [INLINE(256)]
        public static void ApplyDestroyed(safe_ptr<State> state) {
            
            state.ptr->entities.destroyedLock.Lock();
            if (state.ptr->entities.destroyed.Count == 0u) {
                state.ptr->entities.destroyedLock.Unlock();
                return;
            }
            {
                state.ptr->entities.destroyed.Sort<uint>(state);
                for (uint i = 0u; i < state.ptr->entities.destroyed.Count; ++i) {
                    var entId = state.ptr->entities.destroyed[state, i];
                    var groupId = state.ptr->entities.entityToGroup[state, entId];
                    ref var groups = ref state.ptr->entities.groupByEntityType[state, groupId];
                    groups.Delete(state, entId);
                }
                state.ptr->entities.freeCount += state.ptr->entities.destroyed.Count;
                state.ptr->entities.destroyed.Clear();
            }
            state.ptr->entities.destroyedLock.Unlock();
            
        }

        [NotThreadSafe]
        [INLINE(256)]
        private static void Resize(safe_ptr<State> state, ref Ents entities, uint len) {
            len = Bitwise.AlignUp(len, ENTITIES_PER_PAGE);
            if (entities.aliveBits.IsCreated == false) {
                entities.aliveBits = new BitArray(ref state.ptr->allocator, len, threadSafe: true);
            }
            entities.aliveBits.Resize(ref state.ptr->allocator, len, growFactor: 2);
            entities.destroyed.Resize(ref state.ptr->allocator, len);
            entities.entityToGroup.Resize(ref state.ptr->allocator, len, 2);
            entities.entityToGroupLocal.Resize(ref state.ptr->allocator, len, 2);
            entities.generations.Resize(ref state.ptr->allocator, len, 2);
            entities.versions.Resize(ref state.ptr->allocator, len, 2);
            entities.versionsGroup.Resize(ref state.ptr->allocator, len * (StaticTypesTrackedBurst.maxId + 1u), 2);
            entities.locksPerEntity.Resize(ref state.ptr->allocator, len, 2);
            entities.seeds.Resize(ref state.ptr->allocator, len, 2);
            #if ENABLE_BECS_FLAT_QUERIES
            entities.entityToComponents.Resize(ref state.ptr->allocator, len, 2);
            #endif
        }

        [NotThreadSafe]
        [INLINE(256)]
        public static ushort GetEntityGroupId(safe_ptr<State> state, uint entId) {
            return state.ptr->entities.entityToGroup[state, entId];
        }

        [NotThreadSafe]
        [INLINE(256)]
        public static Ent New(safe_ptr<State> state, ushort worldId, ushort groupId, out bool reuse, in JobInfo jobInfo) {
            
            E.IS_IN_TICK(state);

            if (jobInfo.itemsPerCall.ptr != null) {
                reuse = true;
                return jobInfo.GetEntity(groupId);
            }
            
            if (JobUtils.IsInParallelJob() == true) {
                throw new System.Exception("EnsureFree must be called before parallel job");
            }
            
            ref var groups = ref state.ptr->entities.groupByEntityType[state, groupId];
            var entId = groups.New(state, jobInfo, groupId, ref state.ptr->entities.nextGroupId, out uint localGroupIndex, out reuse);
            
            const ushort version = 1;

            JobUtils.Increment(ref state.ptr->entities.aliveCount);
            JobUtils.Decrement(ref state.ptr->entities.freeCount);
            ushort gen = 1;
            if (reuse == false) {
                var len = entId + 1u;
                state.ptr->entities.resizeLock.WriteBegin(state);
                {
                    Resize(state, ref state.ptr->entities, len);
                    state.ptr->entities.aliveBits.SetThreaded(in state.ptr->allocator, entId, true);
                    state.ptr->entities.entityToGroup[state, entId] = groupId;
                    state.ptr->entities.entityToGroupLocal[state, entId] = localGroupIndex;
                    state.ptr->entities.generations[in state.ptr->allocator, entId] = gen;
                    state.ptr->entities.versions[in state.ptr->allocator, entId] = version;
                    #if ENABLE_BECS_FLAT_QUERIES
                    state.ptr->entities.entityToComponents[in state.ptr->allocator, entId] = new LockedEntityToComponent(ref state.ptr->allocator, LockedEntityToComponent.DEFAULT_CAPACITY);
                    #endif
                }
                state.ptr->entities.resizeLock.WriteEnd();
            } else {
                var idx = entId;
                state.ptr->entities.resizeLock.ReadBegin(state);
                {
                    state.ptr->entities.aliveBits.SetThreaded(in state.ptr->allocator, entId, true);
                    gen = ++state.ptr->entities.generations[in state.ptr->allocator, idx];
                    state.ptr->entities.versions[in state.ptr->allocator, idx] = version;
                    state.ptr->entities.seeds[in state.ptr->allocator, idx] = idx + state.ptr->seed;
                    var groupsIndex = (StaticTypesTrackedBurst.maxId + 1u) * idx;
                    _memclear((safe_ptr<byte>)state.ptr->entities.versionsGroup.GetUnsafePtr(in state.ptr->allocator) + groupsIndex * TSize<ushort>.size, (StaticTypesTrackedBurst.maxId + 1u) * TSize<ushort>.size);
                    state.ptr->entities.entityToGroup[state, entId] = groupId;
                    state.ptr->entities.entityToGroupLocal[state, entId] = localGroupIndex;
                    #if ENABLE_BECS_FLAT_QUERIES
                    state.ptr->entities.entityToComponents[in state.ptr->allocator, entId].Clear(state, LockedEntityToComponent.DEFAULT_CAPACITY);
                    #endif
                }
                state.ptr->entities.resizeLock.ReadEnd(state);
            }
            return new Ent(entId, gen, worldId);
        }
        
        [INLINE(256)]
        public static ushort GetGeneration(safe_ptr<State> state, uint id) {

            if (id >= state.ptr->entities.generations.Length) return 0;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var gen = state.ptr->entities.generations[in state.ptr->allocator, id];
            state.ptr->entities.resizeLock.ReadEnd(state);
            return gen;

        }

        [INLINE(256)]
        public static uint GetVersion(safe_ptr<State> state, in Ent ent) {

            if (ent.id >= state.ptr->entities.versions.Length) return 0u;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var version = state.ptr->entities.versions[in state.ptr->allocator, ent.id];
            state.ptr->entities.resizeLock.ReadEnd(state);
            return version;

        }

        [INLINE(256)]
        public static ushort GetVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            var groupsIndex = (StaticTypesTrackedBurst.maxId + 1u) * ent.id;
            var idx = groupsIndex + groupId;
            if (idx >= state.ptr->entities.versionsGroup.Length) return 0;
            return state.ptr->entities.versionsGroup[in state.ptr->allocator, idx];

        }

        [INLINE(256)]
        public static void UpVersion<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            Ents.UpVersion(state, in ent, StaticTypes<T>.trackerIndex);
            
        }

        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            Ents.UpVersion(state, in ent); 
            Ents.UpVersionGroup(state, ent.id, groupId);

        }

        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent) {
            
            ++state.ptr->entities.versions[in state.ptr->allocator, ent.id];
            Journal.VersionUp(in ent);

        }

        [INLINE(256)]
        public static void UpVersionGroup(safe_ptr<State> state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesTrackedBurst.maxId + 1u) * id + groupId;
            ++state.ptr->entities.versionsGroup[in state.ptr->allocator, groupsIndex];

        }

        [INLINE(256)]
        public static uint GetNextSeed(safe_ptr<State> state, in Ent ent) {
            return JobUtils.Increment(ref state.ptr->entities.seeds[in state.ptr->allocator, ent.id]);
        }

        [INLINE(256)]
        public void SetSeed(safe_ptr<State> state, uint seed) {

            for (uint i = 0; i < state.ptr->entities.seeds.Length; ++i) {
                state.ptr->entities.seeds[in state.ptr->allocator, i] = i + seed;
            }
            
        }

    }

}