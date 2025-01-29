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

        public int Hash => Utils.Hash(this.FreeCount, this.EntitiesCount);

        [INLINE(256)]
        public static void Lock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Lock();
        }

        [INLINE(256)]
        public static void Unlock(safe_ptr<State> state, in Ent ent) {
            state.ptr->entities.locksPerEntity[state, ent.id].Unlock();
        }

        public uint GetReservedSizeInBytes(safe_ptr<State> state) {

            if (this.generations.IsCreated == false) return 0u;

            var size = TSize<Ents>.size;
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
        public static Ents Create(safe_ptr<State> state, uint entityCapacity) {

            if (entityCapacity == 0u) entityCapacity = 1u;
            
            var ents = new Ents() {
                generations = new MemArray<ushort>(ref state.ptr->allocator, entityCapacity),
                versions = new MemArray<uint>(ref state.ptr->allocator, entityCapacity),
                seeds = new MemArray<uint>(ref state.ptr->allocator, entityCapacity),
                versionsGroup = new MemArray<uint>(ref state.ptr->allocator, entityCapacity * (StaticTypesGroupsBurst.maxId + 1u)),
                aliveBits = new MemArray<bool>(ref state.ptr->allocator, entityCapacity),
                free = new JobThreadStack<uint>(ref state.ptr->allocator, entityCapacity),
                destroyed = new List<uint>(ref state.ptr->allocator, entityCapacity),
                locksPerEntity = new MemArray<LockSpinner>(ref state.ptr->allocator, entityCapacity),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };
            //var ptr = (uint*)ents.free.GetUnsafePtr(in state.ptr->allocator);
            for (uint i = ents.generations.Length, k = 0u; i > 0u; --i, ++k) {
                //ents.free.PushNoChecks(i - 1u, ptr + k);
                ents.free.Push(ref state.ptr->allocator, i - 1u);
                ++ents.entitiesCount;
            }
            return ents;

        }

        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, in Ent ent) {

            if (ent.id > state.ptr->entities.entitiesCount) return false;
            state.ptr->entities.readWriteSpinner.ReadBegin(state);
            var gen = state.ptr->entities.generations[in state.ptr->allocator, ent.id];
            state.ptr->entities.readWriteSpinner.ReadEnd(state);
            return gen > 0 && ent.gen == gen;

        }

        [INLINE(256)]
        public static bool IsAlive(safe_ptr<State> state, uint entId, out ushort gen) {

            gen = 0;
            if (entId > state.ptr->entities.entitiesCount) return false;
            if (entId >= state.ptr->entities.aliveBits.Length || state.ptr->entities.aliveBits[in state.ptr->allocator, (int)entId] == false) return false;
            state.ptr->entities.readWriteSpinner.ReadBegin(state);
            gen = state.ptr->entities.generations[in state.ptr->allocator, entId];
            state.ptr->entities.readWriteSpinner.ReadEnd(state);
            return true;

        }

        [INLINE(256)]
        public static void Initialize(safe_ptr<State> state, UnsafeList<Ent>* list, uint maxId) {
            
            const ushort version = 1;
            
            state.ptr->entities.readWriteSpinner.WriteBegin(state);
            
            // Resize by maxId
            state.ptr->entities.generations.Resize(ref state.ptr->allocator, maxId + 1u, 2);
            state.ptr->entities.versionsGroup.Resize(ref state.ptr->allocator, (maxId + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
            state.ptr->entities.versions.Resize(ref state.ptr->allocator, maxId + 1u, 2);
            state.ptr->entities.seeds.Resize(ref state.ptr->allocator, maxId + 1u, 2);
            state.ptr->entities.aliveBits.Resize(ref state.ptr->allocator, maxId + 1u, 1);
            
            // Apply list
            for (int i = 0; i < list->Length; ++i) {
                var ent = list->ElementAt(i);
                state.ptr->entities.generations[in state.ptr->allocator, ent.id] = ent.gen;
                state.ptr->entities.versions[in state.ptr->allocator, ent.id] = version;
                state.ptr->entities.aliveBits[in state.ptr->allocator, (int)ent.id] = true;
            }

            state.ptr->entities.readWriteSpinner.WriteEnd();

        }

        [INLINE(256)]
        public static void EnsureFree(safe_ptr<State> state, ushort worldId, uint count) {
            
            E.IS_IN_TICK(state);

            var delta = (int)count - (int)state.ptr->entities.free.Count;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    var ent = Ent.New_INTERNAL(worldId, default);
                    ent.Destroy();
                }
                Ents.ApplyDestroyed(state);
            }

        }

        [INLINE(256)]
        public static Ent Add(safe_ptr<State> state, ushort worldId, out bool reused, in JobInfo jobInfo) {

            E.IS_IN_TICK(state);
            
            const ushort version = 1;
            
            var idx = 0u;
            var cnt = state.ptr->entities.free.Count;
            if (cnt > jobInfo.Offset) {
                state.ptr->entities.popLock.Lock();
                cnt = state.ptr->entities.free.Count;
                if (cnt > jobInfo.Offset) {
                    idx = state.ptr->entities.free.Pop(in state.ptr->allocator, in jobInfo);
                }
                state.ptr->entities.popLock.Unlock();
            }
            
            if (cnt > 0u) {

                reused = true;
                JobUtils.Increment(ref state.ptr->entities.aliveCount);
                state.ptr->entities.readWriteSpinner.ReadBegin(state);
                var nextGen = ++state.ptr->entities.generations[in state.ptr->allocator, idx];
                state.ptr->entities.versions[in state.ptr->allocator, idx] = version;
                state.ptr->entities.seeds[in state.ptr->allocator, idx] = idx;
                var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * idx;
                _memclear((safe_ptr<byte>)state.ptr->entities.versionsGroup.GetUnsafePtr(in state.ptr->allocator) + groupsIndex * TSize<uint>.size, (StaticTypesGroupsBurst.maxId + 1u) * TSize<uint>.size);
                state.ptr->entities.aliveBits[in state.ptr->allocator, idx] = true;
                state.ptr->entities.readWriteSpinner.ReadEnd(state);
                return new Ent(idx, nextGen, worldId);

            } else {

                E.THREAD_CHECK("Add entity");
                
                reused = false;
                const ushort gen = 1;
                JobUtils.Increment(ref state.ptr->entities.aliveCount);
                idx = JobUtils.Increment(ref state.ptr->entities.entitiesCount);
                var ent = new Ent(idx - 1u, gen, worldId);
                idx = ent.id;
                state.ptr->entities.readWriteSpinner.WriteBegin(state);
                state.ptr->entities.locksPerEntity.Resize(ref state.ptr->allocator, idx + 1u, 2);
                state.ptr->entities.generations.Resize(ref state.ptr->allocator, idx + 1u, 2);
                state.ptr->entities.generations[in state.ptr->allocator, idx] = gen;
                state.ptr->entities.versionsGroup.Resize(ref state.ptr->allocator, (idx + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
                state.ptr->entities.versions.Resize(ref state.ptr->allocator, idx + 1u, 2);
                state.ptr->entities.versions[in state.ptr->allocator, idx] = version;
                state.ptr->entities.seeds.Resize(ref state.ptr->allocator, idx + 1u, 2);
                state.ptr->entities.seeds[in state.ptr->allocator, idx] = idx;
                state.ptr->entities.aliveBits.Resize(ref state.ptr->allocator, idx + 1u, 1);
                state.ptr->entities.aliveBits[in state.ptr->allocator, idx] = true;
                state.ptr->entities.readWriteSpinner.WriteEnd();
                return ent;

            }
            
        }

        [INLINE(256)]
        public static void RemoveThreaded(safe_ptr<State> state, uint entId) {
            
            JobUtils.Decrement(ref state.ptr->entities.aliveCount);
            state.ptr->entities.readWriteSpinner.ReadBegin(state);
            ++state.ptr->entities.generations[in state.ptr->allocator, entId];
            state.ptr->entities.aliveBits[in state.ptr->allocator, entId] = false;
            state.ptr->entities.readWriteSpinner.ReadEnd(state);

        }

        [INLINE(256)]
        public static void Remove(safe_ptr<State> state, in Ent ent) {
            
            E.IS_IN_TICK(state);
            
            Ents.RemoveThreaded(state, ent.id);

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
                state.ptr->entities.popLock.Lock();
                state.ptr->entities.free.PushRange(ref state.ptr->allocator, state.ptr->entities.destroyed);
                state.ptr->entities.popLock.Unlock();
                state.ptr->entities.destroyed.Clear();
            }
            state.ptr->entities.destroyedLock.Unlock();

        }

        [INLINE(256)]
        public static ushort GetGeneration(safe_ptr<State> state, uint id) {

            if (id >= state.ptr->entities.generations.Length) return 0;
            state.ptr->entities.readWriteSpinner.ReadBegin(state);
            var gen = state.ptr->entities.generations[in state.ptr->allocator, id];
            state.ptr->entities.readWriteSpinner.ReadEnd(state);
            return gen;

        }

        [INLINE(256)]
        public static uint GetVersion(safe_ptr<State> state, in Ent ent) {

            if (ent.id >= state.ptr->entities.versions.Length) return 0u;
            state.ptr->entities.readWriteSpinner.ReadBegin(state);
            var version = state.ptr->entities.versions[in state.ptr->allocator, ent.id];
            state.ptr->entities.readWriteSpinner.ReadEnd(state);
            return version;

        }

        [INLINE(256)]
        public static uint GetVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * ent.id;
            var idx = groupsIndex + groupId;
            if (idx >= state.ptr->entities.versionsGroup.Length) return 0u;
            return state.ptr->entities.versionsGroup[in state.ptr->allocator, idx];

        }

        [INLINE(256)]
        public static void UpVersion<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            Ents.UpVersion(state, in ent, StaticTypes<T>.groupId);
            
        }

        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent, uint groupId) {

            Ents.UpVersion(state, in ent);

            if (groupId > 0u) {
                Ents.UpVersionGroup(state, ent.id, groupId);
            }

        }

        [INLINE(256)]
        public static void UpVersion(safe_ptr<State> state, in Ent ent) {
            
            JobUtils.Increment(ref state.ptr->entities.versions[in state.ptr->allocator, ent.id]);
            Journal.VersionUp(in ent);

        }

        [INLINE(256)]
        public static void UpVersionGroup(safe_ptr<State> state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            JobUtils.Increment(ref state.ptr->entities.versionsGroup[in state.ptr->allocator, groupsIndex + groupId]);
            
        }

        [INLINE(256)]
        public static uint GetNextSeed(safe_ptr<State> state, in Ent ent) {
            return JobUtils.Increment(ref state.ptr->entities.seeds[in state.ptr->allocator, ent.id]);
        }

    }
    
}