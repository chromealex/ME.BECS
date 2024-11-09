namespace ME.BECS {

    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public unsafe struct Ents {

        public MemArray<ushort> generations;
        public MemArray<uint> versions;
        public MemArray<uint> seeds;
        public MemArray<uint> versionsGroup;
        public MemArray<bool> aliveBits;
        public JobThreadStack<uint> free;
        public List<uint> destroyed;
        public MemArray<LockSpinner> locksPerEntity;
        public ReadWriteSpinner readWriteSpinner;
        public LockSpinner popLock;
        public LockSpinner destroyedLock;
        public uint Capacity => this.generations.Length;
        public uint FreeCount => this.free.Count;
        public uint EntitiesCount => this.aliveCount;

        private uint entitiesCount;
        private uint aliveCount;

        [INLINE(256)]
        public static void Lock(State* state, in Ent ent) {
            state->entities.locksPerEntity[state, ent.id].Lock();
        }

        [INLINE(256)]
        public static void Unlock(State* state, in Ent ent) {
            state->entities.locksPerEntity[state, ent.id].Unlock();
        }

        public uint GetReservedSizeInBytes(State* state) {

            if (this.generations.IsCreated == false) return 0u;

            var size = 0u;
            size += this.generations.GetReservedSizeInBytes();
            size += this.versions.GetReservedSizeInBytes();
            size += this.seeds.GetReservedSizeInBytes();
            size += this.versionsGroup.GetReservedSizeInBytes();
            size += this.aliveBits.GetReservedSizeInBytes();
            size += this.free.GetReservedSizeInBytes();
            size += this.destroyed.GetReservedSizeInBytes();
            
            return size;
            
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool mode) {
            this.aliveBits.BurstMode(in allocator, mode);
            this.generations.BurstMode(in allocator, mode);
            this.versions.BurstMode(in allocator, mode);
            this.seeds.BurstMode(in allocator, mode);
            this.versionsGroup.BurstMode(in allocator, mode);
            this.free.BurstMode(in allocator, mode);
            this.destroyed.BurstMode(in allocator, mode);
        }

        [INLINE(256)]
        public static Ents Create(State* state, uint entityCapacity) {

            if (entityCapacity == 0u) entityCapacity = 1u;
            
            var ents = new Ents() {
                generations = new MemArray<ushort>(ref state->allocator, entityCapacity),
                versions = new MemArray<uint>(ref state->allocator, entityCapacity),
                seeds = new MemArray<uint>(ref state->allocator, entityCapacity),
                versionsGroup = new MemArray<uint>(ref state->allocator, entityCapacity * (StaticTypesGroupsBurst.maxId + 1u)),
                aliveBits = new MemArray<bool>(ref state->allocator, entityCapacity),
                free = new JobThreadStack<uint>(ref state->allocator, entityCapacity),
                destroyed = new List<uint>(ref state->allocator, entityCapacity),
                locksPerEntity = new MemArray<LockSpinner>(ref state->allocator, entityCapacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };
            //var ptr = (uint*)ents.free.GetUnsafePtr(in state->allocator);
            for (uint i = ents.generations.Length, k = 0u; i > 0u; --i, ++k) {
                //ents.free.PushNoChecks(i - 1u, ptr + k);
                ents.free.Push(ref state->allocator, i - 1u);
                ++ents.entitiesCount;
            }
            return ents;

        }

        [INLINE(256)]
        public static bool IsAlive(State* state, in Ent ent) {

            if (ent.id > state->entities.entitiesCount) return false;
            state->entities.readWriteSpinner.ReadBegin(state);
            var gen = state->entities.generations[in state->allocator, ent.id];
            state->entities.readWriteSpinner.ReadEnd(state);
            return gen > 0 && ent.gen == gen;

        }

        [INLINE(256)]
        public static bool IsAlive(State* state, uint entId, out ushort gen) {

            gen = 0;
            if (entId > state->entities.entitiesCount) return false;
            if (entId >= state->entities.aliveBits.Length || state->entities.aliveBits[in state->allocator, (int)entId] == false) return false;
            state->entities.readWriteSpinner.ReadBegin(state);
            gen = state->entities.generations[in state->allocator, entId];
            state->entities.readWriteSpinner.ReadEnd(state);
            return true;

        }

        [INLINE(256)]
        public static void Initialize(State* state, UnsafeList<Ent>* list, uint maxId) {
            
            const ushort version = 1;
            
            state->entities.readWriteSpinner.WriteBegin(state);
            
            // Resize by maxId
            state->entities.generations.Resize(ref state->allocator, maxId + 1u, 2);
            state->entities.versionsGroup.Resize(ref state->allocator, (maxId + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
            state->entities.versions.Resize(ref state->allocator, maxId + 1u, 2);
            state->entities.seeds.Resize(ref state->allocator, maxId + 1u, 2);
            state->entities.aliveBits.Resize(ref state->allocator, maxId + 1u, 1);
            
            // Apply list
            for (int i = 0; i < list->Length; ++i) {
                var ent = list->ElementAt(i);
                state->entities.generations[in state->allocator, ent.id] = ent.gen;
                state->entities.versions[in state->allocator, ent.id] = version;
                state->entities.aliveBits[in state->allocator, (int)ent.id] = true;
            }

            state->entities.readWriteSpinner.WriteEnd();

        }

        [INLINE(256)]
        public static void EnsureFree(State* state, ushort worldId, uint count) {
            
            E.IS_IN_TICK(state);

            var delta = (int)count - (int)state->entities.free.Count;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    var ent = Ent.New_INTERNAL(worldId, default);
                    ent.Destroy();
                }
                Ents.ApplyDestroyed(state);
            }

        }

        [INLINE(256)]
        public static Ent Add(State* state, ushort worldId, out bool reused, JobInfo jobInfo) {

            E.IS_IN_TICK(state);
            
            const ushort version = 1;
            
            var idx = 0u;
            var cnt = state->entities.free.Count;
            if (cnt > jobInfo.Offset) {
                state->entities.popLock.Lock();
                cnt = state->entities.free.Count;
                if (cnt > jobInfo.Offset) {
                    idx = state->entities.free.Pop(in state->allocator, jobInfo);
                }
                state->entities.popLock.Unlock();
            }
            
            if (cnt > 0u) {

                reused = true;
                JobUtils.Increment(ref state->entities.aliveCount);
                state->entities.readWriteSpinner.ReadBegin(state);
                var nextGen = ++state->entities.generations[in state->allocator, idx];
                state->entities.versions[in state->allocator, idx] = version;
                state->entities.seeds[in state->allocator, idx] = idx;
                var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * idx;
                _memclear((byte*)state->entities.versionsGroup.GetUnsafePtr(in state->allocator) + groupsIndex * TSize<uint>.size, (StaticTypesGroupsBurst.maxId + 1u) * TSize<uint>.size);
                state->entities.aliveBits[in state->allocator, idx] = true;
                state->entities.readWriteSpinner.ReadEnd(state);
                return new Ent(idx, nextGen, worldId);

            } else {

                E.THREAD_CHECK("Add entity");
                
                reused = false;
                const ushort gen = 1;
                JobUtils.Increment(ref state->entities.aliveCount);
                idx = JobUtils.Increment(ref state->entities.entitiesCount);
                var ent = new Ent(idx - 1u, gen, worldId);
                idx = ent.id;
                state->entities.readWriteSpinner.WriteBegin(state);
                state->entities.locksPerEntity.Resize(ref state->allocator, idx + 1u, 2);
                state->entities.generations.Resize(ref state->allocator, idx + 1u, 2);
                state->entities.generations[in state->allocator, idx] = gen;
                state->entities.versionsGroup.Resize(ref state->allocator, (idx + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
                state->entities.versions.Resize(ref state->allocator, idx + 1u, 2);
                state->entities.versions[in state->allocator, idx] = version;
                state->entities.seeds.Resize(ref state->allocator, idx + 1u, 2);
                state->entities.seeds[in state->allocator, idx] = idx;
                state->entities.aliveBits.Resize(ref state->allocator, idx + 1u, 1);
                state->entities.aliveBits[in state->allocator, idx] = true;
                state->entities.readWriteSpinner.WriteEnd();
                return ent;

            }
            
        }

        [INLINE(256)]
        public static void RemoveThreaded(State* state, uint entId) {
            
            JobUtils.Decrement(ref state->entities.aliveCount);
            state->entities.readWriteSpinner.ReadBegin(state);
            ++state->entities.generations[in state->allocator, entId];
            state->entities.aliveBits[in state->allocator, entId] = false;
            state->entities.readWriteSpinner.ReadEnd(state);

        }

        [INLINE(256)]
        public static void Remove(State* state, in Ent ent) {
            
            E.IS_IN_TICK(state);
            
            Ents.RemoveThreaded(state, ent.id);

            state->entities.destroyedLock.Lock();
            state->entities.destroyed.Add(ref state->allocator, ent.id);
            state->entities.destroyedLock.Unlock();
            
        }

        [INLINE(256)]
        public static void ApplyDestroyed(State* state) {

            state->entities.destroyedLock.Lock();
            if (state->entities.destroyed.Count == 0u) {
                state->entities.destroyedLock.Unlock();
                return;
            }
            {
                state->entities.destroyed.Sort<uint>(state);
                state->entities.popLock.Lock();
                state->entities.free.PushRange(ref state->allocator, state->entities.destroyed);
                state->entities.popLock.Unlock();
                state->entities.destroyed.Clear();
            }
            state->entities.destroyedLock.Unlock();

        }

        [INLINE(256)]
        public static ushort GetGeneration(State* state, uint id) {

            if (id >= state->entities.generations.Length) return 0;
            state->entities.readWriteSpinner.ReadBegin(state);
            var gen = state->entities.generations[in state->allocator, id];
            state->entities.readWriteSpinner.ReadEnd(state);
            return gen;

        }

        [INLINE(256)]
        public static uint GetVersion(State* state, in Ent ent) {

            if (ent.id >= state->entities.versions.Length) return 0u;
            state->entities.readWriteSpinner.ReadBegin(state);
            var version = state->entities.versions[in state->allocator, ent.id];
            state->entities.readWriteSpinner.ReadEnd(state);
            return version;

        }

        [INLINE(256)]
        public static uint GetVersion(State* state, in Ent ent, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * ent.id;
            var idx = groupsIndex + groupId;
            if (idx >= state->entities.versionsGroup.Length) return 0u;
            return state->entities.versionsGroup[in state->allocator, idx];

        }

        [INLINE(256)]
        public static void UpVersion<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            Ents.UpVersion(state, in ent, StaticTypes<T>.groupId);
            
        }

        [INLINE(256)]
        public static void UpVersion(State* state, in Ent ent, uint groupId) {

            Ents.UpVersion(state, in ent);

            if (groupId > 0u) {
                Ents.UpVersionGroup(state, ent.id, groupId);
            }

        }

        [INLINE(256)]
        public static void UpVersion(State* state, in Ent ent) {
            
            JobUtils.Increment(ref state->entities.versions[in state->allocator, ent.id]);
            Journal.VersionUp(in ent);

        }

        [INLINE(256)]
        public static void UpVersionGroup(State* state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            JobUtils.Increment(ref state->entities.versionsGroup[in state->allocator, groupsIndex + groupId]);
            
        }

        [INLINE(256)]
        public static uint GetNextSeed(State* state, in Ent ent) {
            return JobUtils.Increment(ref state->entities.seeds[in state->allocator, ent.id]);
        }

    }
    
}