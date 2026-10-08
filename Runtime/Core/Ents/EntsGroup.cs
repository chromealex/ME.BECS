namespace ME.BECS {
    
    using System.Threading;
    using Unity.Mathematics;
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    internal static class EntityTypesManaged {

        public static readonly System.Collections.Generic.Dictionary<ushort, System.Type> typeByGroupId = new System.Collections.Generic.Dictionary<ushort, System.Type>();

    }

    /// <summary>
    /// Registers typed entity categories used by creation and bootstrap.
    /// </summary>
    public class EntityTypes {

        private static readonly Unity.Burst.SharedStatic<uint> groupsCountData = Unity.Burst.SharedStatic<uint>.GetOrCreate<EntityTypes>();
        /// <summary>
        /// Groups count for the associated storage.
        /// </summary>
        public static ref uint groupsCount => ref groupsCountData.Data;

        /// <summary>
        /// Initializes entity types state from the supplied context.
        /// </summary>
        public static void Init() {
            EntityTypesManaged.typeByGroupId.Clear();
        }

        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public static void Register<T>(ushort id) where T : unmanaged, IEntityType {
            EntityTypes<T>.id = id;
            EntityTypesManaged.typeByGroupId.Add(id, typeof(T));
        }

    }
    
    /// <summary>
    /// Registers typed entity categories used by creation and bootstrap.
    /// </summary>
    public class EntityTypes<T> where T : unmanaged, IEntityType {

        private static readonly Unity.Burst.SharedStatic<ushort> idData = Unity.Burst.SharedStatic<ushort>.GetOrCreate<EntityTypes<T>>();
        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public static ref ushort id => ref idData.Data;

    }
    
    /// <summary>
    /// Marks a typed entity creation category used by bootstrap registration.
    /// </summary>
    public interface IEntityType { }

    /// <summary>
    /// Manages entity slots, generations and version storage within a world.
    /// </summary>
    public unsafe partial struct Ents {

        /// <summary>
        /// Entities per page constant used by <c>Ents</c>.
        /// </summary>
        public const uint ENTITIES_PER_PAGE = sizeof(uint) * 8;

        /// <summary>
        /// Defines group state and operations for <c>Ents</c>.
        /// </summary>
        public struct Group {

            /// <summary>
            /// Free used by <c>Ents.Group</c>.
            /// </summary>
            public uint free;
            /// <summary>
            /// Offset into the associated storage or coordinate space.
            /// </summary>
            public uint offset;
            /// <summary>
            /// Whether this value contains no elements.
            /// </summary>
            public bool IsEmpty => this.free == 0u;
            /// <summary>
            /// Number of entries currently tracked by this value.
            /// </summary>
            public uint Count => ENTITIES_PER_PAGE - (uint)math.countbits(this.free);

            /// <summary>
            /// Creates <c>Group</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Group Create(ref uint freeCount, uint offset) {
                JobUtils.Increment(ref freeCount, ENTITIES_PER_PAGE);
                var group = new Group() {
                    offset = offset,
                    free = uint.MaxValue,
                };
                return group;
            }

            /// <summary>
            /// Attempts to new and reports whether the operation succeeded.
            /// </summary>
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

            /// <summary>
            /// Deletes the selected entry from the associated storage.
            /// </summary>
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

        /// <summary>
        /// Defines groups state and operations for <c>Ents</c>.
        /// </summary>
        public struct Groups {

            /// <summary>
            /// Groups used to partition the associated entries.
            /// </summary>
            public List<Group> groups;
            /// <summary>
            /// Free groups used by <c>Ents.Groups</c>.
            /// </summary>
            public List<uint> freeGroups;
            /// <summary>
            /// Free groups has used by <c>Ents.Groups</c>.
            /// </summary>
            public HashSet<uint> freeGroupsHas;
            /// <summary>
            /// Index of this entry within its containing storage.
            /// </summary>
            public uint index;
            /// <summary>
            /// Returns resize lock.
            /// </summary>
            public ref ReadWriteNativeSpinner GetResizeLock(ushort worldId) => ref LocksCache.GetReadWriteSpinner(worldId, LocksCache.ENT_GROUPS, this.index);

            /// <summary>
            /// Returns the number of matching entries.
            /// </summary>
            public uint Count(World world) {
                var cnt = 0u;
                for (uint i = 0u; i < this.groups.Count; ++i) {
                    var group = this.groups[world.state, i];
                    cnt += group.Count;
                }
                return cnt;
            }

            /// <summary>
            /// Creates <c>Groups</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public static Groups Create(uint index, safe_ptr<State> state, uint initGroupsCount) {
                using (new AllocatorTag(ALLOC_TAGS.ENTITIES)) {
                    return new Groups() {
                        index = index,
                        groups = new List<Group>(ref state.ptr->allocator, initGroupsCount),
                        freeGroupsHas = new HashSet<uint>(ref state.ptr->allocator, initGroupsCount),
                        freeGroups = new List<uint>(ref state.ptr->allocator, initGroupsCount),
                    };
                }
            }

            [INLINE(256)]
            private bool TryNew(safe_ptr<State> state, JobInfo jobInfo, uint groupId, out uint id, out uint groupIndex) {
                id = 0u;
                groupIndex = uint.MaxValue;
                if (this.freeGroups.Count == 0u) return false;
                var count = this.freeGroups.Count;
                var offset = jobInfo.GetOffset(groupId) % count;
                for (uint i = 0; i < this.freeGroups.Count; ++i) {
                    var freeGroupIndex = offset + i;
                    if (freeGroupIndex >= count) freeGroupIndex -= count;
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
            private void RemoveFreeGroupIfFull(safe_ptr<State> state, uint groupIndex) {
                ref var group = ref this.groups[state, groupIndex];
                if (group.IsEmpty == true && this.freeGroupsHas.Remove(ref state.ptr->allocator, groupIndex) == true) {
                    this.freeGroups.Remove(ref state.ptr->allocator, groupIndex);
                }
            }

            /// <summary>
            /// Creates <c>uint</c> using the supplied creation arguments.
            /// </summary>
            [INLINE(256)]
            public uint New(safe_ptr<State> state, ushort worldId, JobInfo jobInfo, uint groupId, ref uint nextGroupId, out uint localGroupIndex, out bool reuse) {
                ref var resizeLock = ref this.GetResizeLock(worldId);
                if (state.ptr->entities.freeCount > 0u) {
                    resizeLock.ReadBegin();
                    if (this.TryNew(state, jobInfo, groupId, out var entId, out var usedGroupIndex) == true) {
                        localGroupIndex = usedGroupIndex;
                        var group = this.groups[state, usedGroupIndex];
                        var isFull = group.IsEmpty;
                        resizeLock.ReadEnd();
                        if (isFull == true) {
                            // Upgrade only when the shared free-groups metadata must be changed.
                            resizeLock.WriteBegin();
                            this.RemoveFreeGroupIfFull(state, usedGroupIndex);
                            resizeLock.WriteEnd();
                        }
                        reuse = true;
                        return entId;
                    }
                    resizeLock.ReadEnd();
                }

                {
                    if (JobUtils.IsInParallelJob() == true) {
                        throw new System.Exception("EnsureFree must be called before parallel job");
                    }
                    reuse = false;
                    // create new group
                    resizeLock.WriteBegin();
                    if (state.ptr->entities.freeCount > 0u && this.TryNew(state, jobInfo, groupId, out var entId, out var usedGroupIndex) == true) {
                        localGroupIndex = usedGroupIndex;
                        this.RemoveFreeGroupIfFull(state, usedGroupIndex);
                        resizeLock.WriteEnd();
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
                        resizeLock.WriteEnd();
                        return id;
                    }
                }
                
            }

            /// <summary>
            /// Deletes the selected entry from the associated storage.
            /// </summary>
            [INLINE(256)]
            public void Delete(safe_ptr<State> state, ushort worldId, uint entId) {
                this.GetResizeLock(worldId).WriteBegin();
                this.DeleteNoLock(state, entId);
                this.GetResizeLock(worldId).WriteEnd();
            }

            [INLINE(256)]
            internal void DeleteNoLock(safe_ptr<State> state, uint entId) {
                var localGroupIndex = state.ptr->entities.entityToGroupLocal[state, entId];
                ref var group = ref this.groups[state, localGroupIndex];
                group.Delete(entId);
                if (this.freeGroupsHas.Add(ref state.ptr->allocator, localGroupIndex) == true) {
                    this.freeGroups.Add(ref state.ptr->allocator, localGroupIndex);
                }
            }

            /// <summary>
            /// Returns the amount of reserved storage in bytes.
            /// </summary>
            public uint GetReservedSizeInBytes(safe_ptr<State> state) {
                var size = 0u;
                size += this.groups.GetReservedSizeInBytes();
                size += this.freeGroups.GetReservedSizeInBytes();
                size += this.freeGroupsHas.GetReservedSizeInBytes();
                return size;
            }

        }
        
        /// <summary>
        /// Group by entity type used by <c>Ents</c>.
        /// </summary>
        public MemArray<Groups> groupByEntityType;
        /// <summary>
        /// Generations used by <c>Ents</c>.
        /// </summary>
        public MemArray<ushort> generations;
        /// <summary>
        /// Entity to group used by <c>Ents</c>.
        /// </summary>
        public MemArray<ushort> entityToGroup;
        /// <summary>
        /// Entity to group local used by <c>Ents</c>.
        /// </summary>
        public MemArray<uint> entityToGroupLocal;
        /// <summary>
        /// Versions used by <c>Ents</c>.
        /// </summary>
        public MemArray<uint> versions;
        /// <summary>
        /// Seeds used by <c>Ents</c>.
        /// </summary>
        public MemArray<uint> seeds;
        /// <summary>
        /// Versions group used by <c>Ents</c>.
        /// </summary>
        public MemArray<ushort> versionsGroup;
        /// <summary>
        /// Locks per entity used by <c>Ents</c>.
        /// </summary>
        public MemArray<LockSpinner> locksPerEntity;
        /// <summary>
        /// Whether destroyed behavior or state is selected.
        /// </summary>
        public List<uint> destroyed;
        /// <summary>
        /// Destroyed lock used by <c>Ents</c>.
        /// </summary>
        public LockSpinner destroyedLock;
        /// <summary>
        /// Prewarm lock used by <c>Ents</c>.
        /// </summary>
        public LockSpinner prewarmLock;
        /// <summary>
        /// Alive bits used by <c>Ents</c>.
        /// </summary>
        public BitArray aliveBits;

        /// <summary>
        /// Alive count for the associated storage.
        /// </summary>
        public uint aliveCount;
        /// <summary>
        /// Free count for the associated storage.
        /// </summary>
        public uint freeCount;
        /// <summary>
        /// Next group id used to locate the associated entry.
        /// </summary>
        public uint nextGroupId;
        /// <summary>
        /// Resize lock used by <c>Ents</c>.
        /// </summary>
        public ReadWriteSpinner resizeLock;

        /// <summary>
        /// Writes collection metadata to the stream without serializing the backing allocator blocks.
        /// </summary>
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

        /// <summary>
        /// Restores collection metadata from the stream; backing allocator storage is restored separately.
        /// </summary>
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
        
        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => this.generations.Length;
        /// <summary>
        /// Gets entities count; this implementation returns <c>this.aliveCount</c>.
        /// </summary>
        public uint EntitiesCount => this.aliveCount;
        /// <summary>
        /// Gets free count; this implementation returns <c>this.freeCount</c>.
        /// </summary>
        public uint FreeCount => this.freeCount;
        /// <summary>
        /// Indicates hash.
        /// </summary>
        public int Hash => Utils.Hash(this.FreeCount, this.EntitiesCount, this.nextGroupId);

        /// <summary>
        /// Returns entities count.
        /// </summary>
        public uint GetEntitiesCount<T>(World world) where T : unmanaged, IEntityType {
            return this.groupByEntityType[world.state, EntityTypes<T>.id].Count(world);
        }

        /// <summary>
        /// Returns the amount of reserved storage in bytes.
        /// </summary>
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
            size += this.GetEntityComponentsReservedSizeInBytes();
            
            return size;
        }

        /// <summary>
        /// Runs prewarming begin.
        /// </summary>
        [INLINE(256)]
        public static void PrewarmBegin(safe_ptr<State> state) {
            state.ptr->entities.prewarmLock.Lock();
        }

        /// <summary>
        /// Runs prewarming end.
        /// </summary>
        [INLINE(256)]
        public static void PrewarmEnd(safe_ptr<State> state) {
            state.ptr->entities.prewarmLock.Unlock();
        }

        /// <summary>
        /// Acquires the synchronization lock before accessing protected state.
        /// </summary>
        [INLINE(256)]
        public static void Lock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Lock();
        }

        /// <summary>
        /// Releases the synchronization lock after accessing protected state.
        /// </summary>
        [INLINE(256)]
        public static void Unlock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Unlock();
        }

        /// <summary>
        /// Updates cached native access for the requested Burst execution mode.
        /// </summary>
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
            this.BurstModeEntityComponents(in allocator, mode);
        }

        /// <summary>
        /// Creates <c>Ents</c> using the supplied creation arguments.
        /// </summary>
        [NotThreadSafe]
        [INLINE(256)]
        public static Ents Create(safe_ptr<State> state, uint groupsCount, uint entitiesCapacity) {
            var ents = new Ents();
            ents.Init(state, groupsCount, entitiesCapacity);
            return ents;
        }
        
        /// <summary>
        /// Initializes ents state from the supplied context.
        /// </summary>
        [NotThreadSafe]
        [INLINE(256)]
        public void Init(safe_ptr<State> state, uint groupsCount, uint entitiesCapacity) {
            var initGroupsCount = entitiesCapacity / ENTITIES_PER_PAGE;
            using (new AllocatorTag(ALLOC_TAGS.ENTITIES)) {
                this.resizeLock = ReadWriteSpinner.Create(state);
                this.groupByEntityType = new MemArray<Groups>(ref state.ptr->allocator, groupsCount + 1u);
                Resize(state, ref this, entitiesCapacity);
                this.destroyedLock = default;
                this.nextGroupId = 0u;
                this.freeCount = 0u;
                this.aliveCount = 0u;
                for (uint i = 0u; i < groupsCount + 1u; ++i) {
                    this.groupByEntityType[state, i] = Groups.Create(i, state, initGroupsCount);
                }
            }
        }

        /// <summary>
        /// Sets capacity.
        /// </summary>
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

        /// <summary>
        /// Ensures free.
        /// </summary>
        [NotThreadSafe]
        [INLINE(256)]
        public static uint EnsureFree(safe_ptr<State> state, ushort worldId, uint groupId, uint required) {
            
            E.IS_IN_TICK(state);

            var free = state.ptr->entities.FreeCount;
            if (free >= required) {
                return 0u;
            }

            var need = required - free;
            var groupsNeeded = (uint)math.ceil(need / (float)ENTITIES_PER_PAGE);
            ref var group = ref state.ptr->entities.groupByEntityType[state, groupId];
            var newGroupsCount = group.groups.Count + groupsNeeded;
            group.GetResizeLock(worldId).WriteBegin();
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
            group.GetResizeLock(worldId).WriteEnd();

            state.ptr->entities.resizeLock.WriteBegin(state);
            Resize(state, ref state.ptr->entities, newGroupsCount * ENTITIES_PER_PAGE);
            state.ptr->entities.resizeLock.WriteEnd();

            return newGroupsCount * ENTITIES_PER_PAGE;
            
        }
        
        /// <summary>
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, in Ent ent) {
            return IsAlive(state, ent.id, ent.gen);
        }
        
        /// <summary>
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, uint entId, ushort gen) {
            if (entId >= state.ptr->entities.generations.Length || gen == 0) return false;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var result = state.ptr->entities.generations[state, entId] == gen;
            state.ptr->entities.resizeLock.ReadEnd(state);
            return result;
        }

        /// <summary>
        /// Removes the specified entry from ents.
        /// </summary>
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
        
        /// <summary>
        /// Applies destroyed.
        /// </summary>
        [INLINE(256)]
        public static void ApplyDestroyed(safe_ptr<State> state, ushort worldId) {
            
            state.ptr->entities.destroyedLock.Lock();
            if (state.ptr->entities.destroyed.Count == 0u) {
                state.ptr->entities.destroyedLock.Unlock();
                return;
            }
            {
                state.ptr->entities.destroyed.Sort<uint>(state);
                for (uint i = 0u; i < state.ptr->entities.destroyed.Count;) {
                    var entId = state.ptr->entities.destroyed[state, i];
                    var groupId = state.ptr->entities.entityToGroup[state, entId];
                    ref var groups = ref state.ptr->entities.groupByEntityType[state, groupId];
                    groups.GetResizeLock(worldId).WriteBegin();
                    do {
                        groups.DeleteNoLock(state, entId);
                        ++i;
                        if (i >= state.ptr->entities.destroyed.Count) break;
                        entId = state.ptr->entities.destroyed[state, i];
                    } while (state.ptr->entities.entityToGroup[state, entId] == groupId);
                    groups.GetResizeLock(worldId).WriteEnd();
                }
                state.ptr->entities.freeCount += state.ptr->entities.destroyed.Count;
                state.ptr->entities.destroyed.Clear();
            }
            state.ptr->entities.destroyedLock.Unlock();
            
        }

        [NotThreadSafe]
        [INLINE(256)]
        private static void Resize(safe_ptr<State> state, ref Ents entities, uint len) {
            using (new AllocatorTag(ALLOC_TAGS.ENTITIES)) {
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
                entities.ResizeEntityComponents(state, len);
            }
        }

        /// <summary>
        /// Returns entity group ID.
        /// </summary>
        [NotThreadSafe]
        [INLINE(256)]
        public static ushort GetEntityGroupId(safe_ptr<State> state, uint entId) {
            return state.ptr->entities.entityToGroup[state, entId];
        }

        /// <summary>
        /// Creates <c>Ent</c> using the supplied creation arguments.
        /// </summary>
        [NotThreadSafe]
        [INLINE(256)]
        public static Ent New(safe_ptr<State> state, ushort worldId, ushort groupId, out bool reuse, in JobInfo jobInfo) {
            
            E.IS_IN_TICK(state);

            if (jobInfo.itemsPerCall.ptr != null) {
                reuse = true;
                return jobInfo.GetEntity(groupId);
            }

            jobInfo.CheckEntityLimit();
            
            if (JobUtils.IsInParallelJob() == true) {
                throw new System.Exception("EnsureFree must be called before parallel job");
            }
            
            ref var groups = ref state.ptr->entities.groupByEntityType[state, groupId];
            var entId = groups.New(state, worldId, jobInfo, groupId, ref state.ptr->entities.nextGroupId, out uint localGroupIndex, out reuse);
            
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
                    state.ptr->entities.ClearEntityComponents(state, entId);
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
                    state.ptr->entities.ClearEntityComponents(state, entId);
                }
                state.ptr->entities.resizeLock.ReadEnd(state);
            }
            return new Ent(entId, gen, worldId);
        }
        
        /// <summary>
        /// Returns generation.
        /// </summary>
        [INLINE(256)]
        public static ushort GetGeneration(safe_ptr<State> state, uint id) {

            if (id >= state.ptr->entities.generations.Length) return 0;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var gen = state.ptr->entities.generations[in state.ptr->allocator, id];
            state.ptr->entities.resizeLock.ReadEnd(state);
            return gen;

        }

        /// <summary>
        /// Returns version.
        /// </summary>
        [INLINE(256)]
        public static uint GetVersion(safe_ptr<State> state, in Ent ent) {

            if (ent.id >= state.ptr->entities.versions.Length) return 0u;
            state.ptr->entities.resizeLock.ReadBegin(state);
            var version = state.ptr->entities.versions[in state.ptr->allocator, ent.id];
            state.ptr->entities.resizeLock.ReadEnd(state);
            return version;

        }

        /// <summary>
        /// Returns version.
        /// </summary>
        [INLINE(256)]
        public static ushort GetVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            var groupsIndex = (StaticTypesTrackedBurst.maxId + 1u) * ent.id;
            var idx = groupsIndex + groupId;
            if (idx >= state.ptr->entities.versionsGroup.Length) return 0;
            return state.ptr->entities.versionsGroup[in state.ptr->allocator, idx];

        }

        /// <summary>
        /// Advances the change version tracked for the supplied entity or component.
        /// </summary>
        [INLINE(256)]
        public static void UpVersion<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            Ents.UpVersion(state, in ent, StaticTypes<T>.trackerIndex);
            
        }

        /// <summary>
        /// Advances the change version tracked for the supplied entity or component.
        /// </summary>
        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            Ents.UpVersion(state, in ent); 
            Ents.UpVersionGroup(state, ent.id, groupId);

        }

        /// <summary>
        /// Advances the change version tracked for the supplied entity or component.
        /// </summary>
        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent) {
            
            ++state.ptr->entities.versions[in state.ptr->allocator, ent.id];
            Journal.VersionUp(in ent);

        }

        /// <summary>
        /// Advances the change version for the specified component group.
        /// </summary>
        [INLINE(256)]
        public static void UpVersionGroup(safe_ptr<State> state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesTrackedBurst.maxId + 1u) * id + groupId;
            ++state.ptr->entities.versionsGroup[in state.ptr->allocator, groupsIndex];

        }

        /// <summary>
        /// Returns next seed.
        /// </summary>
        [INLINE(256)]
        public static uint GetNextSeed(safe_ptr<State> state, in Ent ent) {
            return JobUtils.Increment(ref state.ptr->entities.seeds[in state.ptr->allocator, ent.id]);
        }

        /// <summary>
        /// Sets seed.
        /// </summary>
        [INLINE(256)]
        public void SetSeed(safe_ptr<State> state, uint seed) {

            for (uint i = 0; i < state.ptr->entities.seeds.Length; ++i) {
                state.ptr->entities.seeds[in state.ptr->allocator, i] = i + seed;
            }
            
        }

    }

}
