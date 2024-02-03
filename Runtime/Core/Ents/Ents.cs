namespace ME.BECS {

    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public unsafe struct Ents {

        public MemArray<ushort> generations;
        public MemArray<uint> versions;
        public MemArray<uint> versionsGroup;
        public MemArray<bool> aliveBits;
        public Stack<uint> free;
        public MemArray<LockSpinner> locksPerEntity;
        public ReadWriteSpinner readWriteSpinner;
        public LockSpinner popLock;
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

            if (this.generations.isCreated == false) return 0u;

            var size = 0u;
            size += this.generations.GetReservedSizeInBytes();
            size += this.versions.GetReservedSizeInBytes();
            size += this.versionsGroup.GetReservedSizeInBytes();
            size += this.aliveBits.GetReservedSizeInBytes();
            size += this.free.GetReservedSizeInBytes();
            
            return size;
            
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool mode) {
            this.aliveBits.BurstMode(in allocator, mode);
            this.generations.BurstMode(in allocator, mode);
            this.versions.BurstMode(in allocator, mode);
            this.versionsGroup.BurstMode(in allocator, mode);
            this.free.BurstMode(in allocator, mode);
        }

        [INLINE(256)]
        public static Ents Create(State* state, uint entityCapacity) {

            if (entityCapacity == 0u) entityCapacity = 1u;
            
            var ents = new Ents() {
                generations = new MemArray<ushort>(ref state->allocator, entityCapacity, growFactor: 2),
                versions = new MemArray<uint>(ref state->allocator, entityCapacity, growFactor: 2),
                versionsGroup = new MemArray<uint>(ref state->allocator, entityCapacity * (StaticTypesGroupsBurst.maxId + 1u), growFactor: 2),
                aliveBits = new MemArray<bool>(ref state->allocator, entityCapacity),
                free = new Stack<uint>(ref state->allocator, entityCapacity, growFactor: 2),
                locksPerEntity = new MemArray<LockSpinner>(ref state->allocator, entityCapacity, growFactor: 2),
                readWriteSpinner = ReadWriteSpinner.Create(state),
            };
            var ptr = (uint*)ents.free.GetUnsafePtr(in state->allocator);
            for (uint i = ents.generations.Length, k = 0u; i > 0u; --i, ++k) {
                ents.free.PushNoChecks(i - 1u, ptr + k);
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
            this.generations.Resize(ref state->allocator, maxId + 1u);
            this.versionsGroup.Resize(ref state->allocator, (maxId + 1u) * (StaticTypesGroupsBurst.maxId + 1u));
            this.versions.Resize(ref state->allocator, maxId + 1u);
            this.aliveBits.Resize(ref state->allocator, maxId + 1u);
            
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
        public Ent Add(State* state, ushort worldId, out bool reused) {

            E.IS_IN_TICK(state);
            
            const ushort version = 1;
            
            var idx = 0u;
            var cnt = this.free.Count;
            if (cnt > 0u) {
                this.popLock.Lock();
                cnt = this.free.Count;
                if (cnt > 0u) {
                    idx = this.free.Pop(in state->allocator);
                }
                this.popLock.Unlock();
            }
            
            if (cnt > 0u) {

                reused = true;
                JobUtils.Increment(ref this.aliveCount);
                this.readWriteSpinner.ReadBegin(state);
                var nextGen = ++this.generations[in state->allocator, idx];
                this.versions[in state->allocator, idx] = version;
                var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * idx;
                _memclear((byte*)this.versionsGroup.GetUnsafePtr(in state->allocator) + groupsIndex * sizeof(int), (StaticTypesGroupsBurst.maxId + 1u) * sizeof(int));
                this.aliveBits[in state->allocator, idx] = true;
                this.readWriteSpinner.ReadEnd(state);
                return new Ent(idx, nextGen, worldId);

            } else {

                reused = false;
                const ushort gen = 1;
                JobUtils.Increment(ref this.aliveCount);
                idx = JobUtils.Increment(ref this.entitiesCount);
                var ent = new Ent(idx - 1u, gen, worldId);
                idx = ent.id;
                this.readWriteSpinner.WriteBegin(state);
                this.locksPerEntity.Resize(ref state->allocator, idx + 1u);
                this.generations.Resize(ref state->allocator, idx + 1u);
                this.generations[in state->allocator, idx] = gen;
                this.versionsGroup.Resize(ref state->allocator, (idx + 1u) * (StaticTypesGroupsBurst.maxId + 1u));
                this.versions.Resize(ref state->allocator, idx + 1u);
                this.versions[in state->allocator, idx] = version;
                this.aliveBits.Resize(ref state->allocator, idx + 1u);
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
        public void RemoveTaskComplete(State* state, UnsafeList<uint>* list) {

            this.free.PushRange(ref state->allocator, list);
            
        }

        [INLINE(256)]
        public void Remove(State* state, in Ent ent) {
            
            E.IS_IN_TICK(state);
            
            this.RemoveThreaded(state, ent.id);
            
            //this.free.PushLock(ref this.popLock, ref state->allocator, ent.id);
            this.popLock.Lock();
            this.free.Push(ref state->allocator, ent.id);
            this.popLock.Unlock();
            
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
            
            JobUtils.Increment(ref this.versions[in state->allocator, ent.id]);
            Journal.VersionUp(in ent);

            if (groupId > 0u) {
                this.UpVersionGroup(state, ent.id, groupId);
            }

        }

        [INLINE(256)]
        public void UpVersionGroup(State* state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            JobUtils.Increment(ref this.versionsGroup[in state->allocator, groupsIndex + groupId]);
            
        }

    }
    
}