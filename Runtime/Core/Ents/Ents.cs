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
        public void Lock(State* state, in Ent ent) {
            this.locksPerEntity[state, ent.id].Lock();
        }

        [INLINE(256)]
        public void Unlock(State* state, in Ent ent) {
            this.locksPerEntity[state, ent.id].Unlock();
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
        public bool IsAlive(State* state, in Ent ent) {

            if (ent.id > this.entitiesCount) return false;
            this.readWriteSpinner.ReadBegin(state);
            var gen = this.generations[in state->allocator, ent.id];
            this.readWriteSpinner.ReadEnd(state);
            return gen > 0 && ent.gen == gen;

        }

        [INLINE(256)]
        public bool IsAlive(State* state, uint entId, out ushort gen) {

            gen = 0;
            if (entId > this.entitiesCount) return false;
            if (entId >= this.aliveBits.Length || this.aliveBits[in state->allocator, (int)entId] == false) return false;
            this.readWriteSpinner.ReadBegin(state);
            gen = this.generations[in state->allocator, entId];
            this.readWriteSpinner.ReadEnd(state);
            return true;

        }

        [INLINE(256)]
        public void Initialize(State* state, UnsafeList<Ent>* list, uint maxId) {
            
            const ushort version = 1;
            
            this.readWriteSpinner.WriteBegin(state);
            
            // Resize by maxId
            this.generations.Resize(ref state->allocator, maxId + 1u, 2);
            this.versionsGroup.Resize(ref state->allocator, (maxId + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
            this.versions.Resize(ref state->allocator, maxId + 1u, 2);
            this.seeds.Resize(ref state->allocator, maxId + 1u, 2);
            this.aliveBits.Resize(ref state->allocator, maxId + 1u, 1);
            
            // Apply list
            for (int i = 0; i < list->Length; ++i) {
                var ent = list->ElementAt(i);
                this.generations[in state->allocator, ent.id] = ent.gen;
                this.versions[in state->allocator, ent.id] = version;
                this.aliveBits[in state->allocator, (int)ent.id] = true;
            }

            this.readWriteSpinner.WriteEnd();

        }

        [INLINE(256)]
        public void EnsureFree(State* state, ushort worldId, uint count) {
            
            E.IS_IN_TICK(state);

            var delta = (int)count - (int)this.free.Count;
            if (delta > 0) {
                for (int i = 0; i < delta; ++i) {
                    var ent = Ent.New_INTERNAL(worldId, default);
                    ent.Destroy();
                }
                this.ApplyDestroyed(state);
            }

        }

        [INLINE(256)]
        public Ent Add(State* state, ushort worldId, out bool reused, JobInfo jobInfo) {

            E.IS_IN_TICK(state);
            
            const ushort version = 1;
            
            var idx = 0u;
            var cnt = this.free.Count;
            if (cnt > jobInfo.Offset) {
                this.popLock.Lock();
                cnt = this.free.Count;
                if (cnt > jobInfo.Offset) {
                    idx = this.free.Pop(in state->allocator, jobInfo);
                }
                this.popLock.Unlock();
            }
            
            if (cnt > 0u) {

                reused = true;
                JobUtils.Increment(ref this.aliveCount);
                this.readWriteSpinner.ReadBegin(state);
                var nextGen = ++this.generations[in state->allocator, idx];
                this.versions[in state->allocator, idx] = version;
                this.seeds[in state->allocator, idx] = idx;
                var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * idx;
                _memclear((byte*)this.versionsGroup.GetUnsafePtr(in state->allocator) + groupsIndex * TSize<uint>.size, (StaticTypesGroupsBurst.maxId + 1u) * TSize<uint>.size);
                this.aliveBits[in state->allocator, idx] = true;
                this.readWriteSpinner.ReadEnd(state);
                return new Ent(idx, nextGen, worldId);

            } else {

                E.THREAD_CHECK("Add entity");
                
                reused = false;
                const ushort gen = 1;
                JobUtils.Increment(ref this.aliveCount);
                idx = JobUtils.Increment(ref this.entitiesCount);
                var ent = new Ent(idx - 1u, gen, worldId);
                idx = ent.id;
                this.readWriteSpinner.WriteBegin(state);
                this.locksPerEntity.Resize(ref state->allocator, idx + 1u, 2);
                this.generations.Resize(ref state->allocator, idx + 1u, 2);
                this.generations[in state->allocator, idx] = gen;
                this.versionsGroup.Resize(ref state->allocator, (idx + 1u) * (StaticTypesGroupsBurst.maxId + 1u), 2);
                this.versions.Resize(ref state->allocator, idx + 1u, 2);
                this.versions[in state->allocator, idx] = version;
                this.seeds.Resize(ref state->allocator, idx + 1u, 2);
                this.seeds[in state->allocator, idx] = idx;
                this.aliveBits.Resize(ref state->allocator, idx + 1u, 1);
                this.aliveBits[in state->allocator, idx] = true;
                this.readWriteSpinner.WriteEnd();
                return ent;

            }
            
        }

        [INLINE(256)]
        public void RemoveThreaded(State* state, uint entId) {
            
            JobUtils.Decrement(ref this.aliveCount);
            this.readWriteSpinner.ReadBegin(state);
            ++this.generations[in state->allocator, entId];
            this.aliveBits[in state->allocator, entId] = false;
            this.readWriteSpinner.ReadEnd(state);

        }

        [INLINE(256)]
        public void Remove(State* state, in Ent ent) {
            
            E.IS_IN_TICK(state);
            
            this.RemoveThreaded(state, ent.id);

            this.destroyedLock.Lock();
            this.destroyed.Add(ref state->allocator, ent.id);
            this.destroyedLock.Unlock();
            
        }

        [INLINE(256)]
        public void ApplyDestroyed(State* state) {

            this.destroyedLock.Lock();
            if (this.destroyed.Count == 0u) {
                this.destroyedLock.Unlock();
                return;
            }
            {
                this.destroyed.Sort<uint>(state);
                this.popLock.Lock();
                this.free.PushRange(ref state->allocator, this.destroyed);
                this.popLock.Unlock();
                this.destroyed.Clear();
            }
            this.destroyedLock.Unlock();

        }

        [INLINE(256)]
        public ushort GetGeneration(State* state, uint id) {

            if (id >= this.generations.Length) return 0;
            this.readWriteSpinner.ReadBegin(state);
            var gen = this.generations[in state->allocator, id];
            this.readWriteSpinner.ReadEnd(state);
            return gen;

        }

        [INLINE(256)]
        public uint GetVersion(State* state, in Ent ent) {

            if (ent.id >= this.versions.Length) return 0u;
            this.readWriteSpinner.ReadBegin(state);
            var version = this.versions[in state->allocator, ent.id];
            this.readWriteSpinner.ReadEnd(state);
            return version;

        }

        [INLINE(256)]
        public uint GetVersion(State* state, in Ent ent, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * ent.id;
            var idx = groupsIndex + groupId;
            if (idx >= this.versionsGroup.Length) return 0u;
            return this.versionsGroup[in state->allocator, idx];

        }

        [INLINE(256)]
        public void UpVersion<T>(State* state, in Ent ent) where T : unmanaged, IComponent {

            this.UpVersion(state, in ent, StaticTypes<T>.groupId);
            
        }

        [INLINE(256)]
        public void UpVersion(State* state, in Ent ent, uint groupId) {

            this.UpVersion(state, in ent);

            if (groupId > 0u) {
                this.UpVersionGroup(state, ent.id, groupId);
            }

        }

        [INLINE(256)]
        public void UpVersion(State* state, in Ent ent) {
            
            JobUtils.Increment(ref this.versions[in state->allocator, ent.id]);
            Journal.VersionUp(in ent);

        }

        [INLINE(256)]
        public void UpVersionGroup(State* state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            JobUtils.Increment(ref this.versionsGroup[in state->allocator, groupsIndex + groupId]);
            
        }

        [INLINE(256)]
        public uint GetNextSeed(State* state, in Ent ent) {
            return JobUtils.Increment(ref this.seeds[in state->allocator, ent.id]);
        }

    }
    
}