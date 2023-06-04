namespace ME.BECS {

    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public unsafe struct Ents {

        public MemArray<ushort> generations;
        public MemArray<uint> versions;
        public MemArray<uint> versionsGroup;
        public BitArray aliveBits;
        public Stack<uint> free;
        public int lockIndex;
        public uint Capacity => this.generations.Length;
        public uint FreeCount => this.free.Count;
        public uint EntitiesCount => this.aliveCount;

        private uint entitiesCount;
        private uint aliveCount;

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
                aliveBits = new BitArray(ref state->allocator, entityCapacity),
                free = new Stack<uint>(ref state->allocator, entityCapacity, growFactor: 2),
                lockIndex = 0,
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
            //if (JobUtils.IsInParallelJob() == true) return true;
            var gen = this.generations[in state->allocator, ent.id];
            return gen > 0 && ent.gen == gen;

        }

        [INLINE(256)]
        public bool IsAlive(State* state, uint entId, out ushort gen) {

            gen = 0;
            if (entId > this.entitiesCount) return false;
            if (entId >= this.aliveBits.Length || this.aliveBits.IsSet(in state->allocator, (int)entId) == false) return false;
            gen = this.generations[in state->allocator, entId];
            return true;

        }

        [INLINE(256)]
        public void Initialize(State* state, UnsafeList<Ent>* list, uint maxId) {
            
            const ushort version = 1;
            
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
                this.aliveBits.Set(in state->allocator, (int)ent.id, true);
            }

        }

        [INLINE(256)]
        public Ent Add(State* state, ushort worldId, out bool reused) {

            E.IS_IN_TICK(state);
            
            const ushort version = 1;
            
            if (this.free.Count > 0u) {

                reused = true;
                JobUtils.Increment(ref this.aliveCount);
                var idx = this.free.Pop(in state->allocator);
                var nextGen = ++this.generations[in state->allocator, idx];
                this.versions[in state->allocator, idx] = version;
                var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * idx;
                _memclear((byte*)this.versionsGroup.GetUnsafePtr(in state->allocator) + groupsIndex * sizeof(int), StaticTypesGroupsBurst.maxId + 1u);
                this.aliveBits.Set(in state->allocator, (int)idx, true);
                return new Ent(idx, nextGen, worldId);

            } else {

                reused = false;
                const ushort gen = 1;
                JobUtils.Increment(ref this.aliveCount);
                var idx = JobUtils.Increment(ref this.entitiesCount);
                var ent = new Ent(idx - 1u, gen, worldId);
                idx = ent.id;
                this.generations.Resize(ref state->allocator, idx + 1u);
                this.generations[in state->allocator, idx] = gen;
                this.versionsGroup.Resize(ref state->allocator, (idx + 1u) * (StaticTypesGroupsBurst.maxId + 1u));
                this.versions.Resize(ref state->allocator, idx + 1u);
                this.versions[in state->allocator, idx] = version;
                this.aliveBits.Resize(ref state->allocator, idx + 1u);
                this.aliveBits.Set(in state->allocator, (int)idx, true);
                return ent;

            }
            
        }

        [INLINE(256)]
        public void RemoveThreaded(State* state, uint entId) {
            
            JobUtils.Decrement(ref this.aliveCount);
            ++this.generations[in state->allocator, entId];
            this.aliveBits.Set(in state->allocator, (int)entId, false);
            
        }

        [INLINE(256)]
        public void RemoveTaskComplete(State* state, UnsafeList<uint>* list) {

            this.free.PushRange(ref state->allocator, list);
            
        }

        [INLINE(256)]
        public void Remove(State* state, uint entId) {
            
            E.IS_IN_TICK(state);
            
            this.RemoveThreaded(state, entId);
            this.free.Push(ref state->allocator, entId);

        }

        [INLINE(256)]
        public ushort GetGeneration(State* state, uint id) {

            if (id >= this.generations.Length) return 0;
            return this.generations[in state->allocator, id];

        }

        [INLINE(256)]
        public uint GetVersion(State* state, uint id) {

            if (id >= this.versions.Length) return 0u;
            return this.versions[in state->allocator, id];

        }

        [INLINE(256)]
        public uint GetVersion(State* state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            var idx = groupsIndex + groupId;
            if (idx >= this.versionsGroup.Length) return 0u;
            return this.versionsGroup[in state->allocator, idx];

        }

        [INLINE(256)]
        public void UpVersion<T>(State* state, uint id) where T : unmanaged {

            JobUtils.Increment(ref this.versions[in state->allocator, id]);

            var groupId = StaticTypes<T>.groupId;
            if (groupId > 0u) {
                this.UpVersionGroup(state, id, groupId);
            }

        }

        [INLINE(256)]
        public void UpVersion(State* state, uint id, uint groupId) {

            JobUtils.Increment(ref this.versions[in state->allocator, id]);

            if (groupId > 0u) {
                this.UpVersionGroup(state, id, groupId);
            }

        }

        [INLINE(256)]
        public void UpVersionGroup(State* state, uint id, uint groupId) {

            var groupsIndex = (StaticTypesGroupsBurst.maxId + 1u) * id;
            JobUtils.Increment(ref this.versionsGroup[in state->allocator, groupsIndex + groupId]);
            
        }

    }
    
}