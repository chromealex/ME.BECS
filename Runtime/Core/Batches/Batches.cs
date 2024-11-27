using Unity.Collections;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    using System.Runtime.InteropServices;

    public static class BatchesExt {

        [INLINE(256)]
        public static JobHandle Apply(this in SystemContext context, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in context.world);
        }

        [INLINE(256)]
        public static JobHandle Apply(this in World world, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in world);
        }

    }
    
    public struct BatchList {

        public TempBitArray list;
        public uint Count;
        public uint hash;
        public uint maxId;

        public bool isCreated => this.list.IsCreated;

        [INLINE(256)]
        public void Add(uint value) {

            if (this.list.IsCreated == false) this.list = new TempBitArray(StaticTypes.counter + 1u, ClearOptions.ClearMemory, Constants.ALLOCATOR_PERSISTENT_ST);
            
            ++this.Count;
            if (value > this.maxId) this.maxId = value;
            this.hash ^= value;
            this.list.Set((int)value, true);

        }

        [INLINE(256)]
        public bool Remove(uint value) {

            var idx = (int)value;
            if (this.list.IsSet(idx) == true) {
                --this.Count;
                this.hash ^= value;
                this.list.Set(idx, false);
                return true;
            }
            
            return false;

        }

        [INLINE(256)]
        public void Dispose() {

            if (this.list.IsCreated == true) this.list.Dispose();
            this = default;

        }

    }
    
    public unsafe struct BatchItem {

        private BatchList addItems;
        private BatchList removeItems;
        public LockSpinner lockIndex;
        public ushort entGen;
        public uint Count;
        public bool isCreated => this.addItems.isCreated == true || this.removeItems.isCreated == true;

        [INLINE(256)]
        public void Apply(State* state, uint entId, ref Archetypes archetypes) {

            if (Ents.IsAlive(state, entId, out var gen) == false || gen != this.entGen) {
                this.addItems.Dispose();
                this.removeItems.Dispose();
                return;
            }

            {
                var addItems = ComponentsFastTrack.Create(this.addItems);
                var removeItems = ComponentsFastTrack.Create(this.removeItems);
                MemoryAllocator.ValidateConsistency(ref state->allocator);
                Archetypes.ApplyBatch(state, entId, in addItems, in removeItems);
                this.addItems.Dispose();
                this.removeItems.Dispose();
                this.Count = 0u;
            }

        }
        
        [INLINE(256)]
        public void Add(uint typeId) {

            var removed = false;
            if (this.removeItems.Count > 0u) {
                removed = this.removeItems.Remove(typeId);
            }
            if (removed == false) this.addItems.Add(typeId);
            this.Count = this.addItems.Count + this.removeItems.Count;

        }

        [INLINE(256)]
        public void Remove(uint typeId) {

            var removed = false;
            if (this.addItems.Count > 0u) {
                removed = this.addItems.Remove(typeId);
            }
            if (removed == false) this.removeItems.Add(typeId);
            this.Count = this.addItems.Count + this.removeItems.Count;
            
        }

        [INLINE(256)]
        public void Clear() {
            
            this.addItems.Dispose();
            this.removeItems.Dispose();
            this.Count = 0;

        }

        public uint GetReservedSizeInBytes() {
            var size = TSize<BatchItem>.size;
            size += this.addItems.list.GetReservedSizeInBytes();
            size += this.removeItems.list.GetReservedSizeInBytes();
            return size;
        }

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe partial struct Batches {

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct ThreadItem {

            [FieldOffset(0)]
            public List<uint> items;
            [FieldOffset(List<uint>.SIZE)]
            public uint Count;
            [FieldOffset(List<uint>.SIZE + sizeof(uint))]
            public LockSpinner lockSpinner;

            public uint GetReservedSizeInBytes() {
                return this.items.GetReservedSizeInBytes();
            }

        }
        
        public MemArrayThreadCacheLine<ThreadItem> items;
        public MemArray<BatchItem> arr;
        public uint openIndex;
        public ReadWriteSpinner workingLock;
        internal ReadWriteSpinner lockReadWrite;

        public static uint GetReservedSizeInBytes(State* state) {

            if (state->batches.items.IsCreated == false) return 0u;

            var size = TSize<Batches>.size;
            for (uint i = 0u; i < state->batches.items.Length; ++i) {
                ref var item = ref state->batches.items[in state->allocator, i];
                size += item.GetReservedSizeInBytes();
            }
            for (uint i = 0u; i < state->batches.arr.Length; ++i) {
                ref var item = ref state->batches.arr[in state->allocator, i];
                size += item.GetReservedSizeInBytes();
            }
            
            return size;
            
        }

        [INLINE(256)]
        public static Batches Create(State* state, uint entitiesCapacity) {
            var batches = new Batches() {
                items = new MemArrayThreadCacheLine<ThreadItem>(ref state->allocator),
                arr = new MemArray<BatchItem>(ref state->allocator, entitiesCapacity),
                lockReadWrite = ReadWriteSpinner.Create(state),
                openIndex = 0u,
                workingLock = ReadWriteSpinner.Create(state),
            };
            for (uint i = 0u; i < batches.items.Length; ++i) {
                ref var item = ref batches.items[state, i];
                item.items = new List<uint>(ref state->allocator, entitiesCapacity);
                item.Count = 0u;
            }
            return batches;
        }

        [INLINE(256)]
        public static void OnEntityAddThreadItem(State* state, uint entId) {
            
            for (uint i = 0u; i < state->batches.items.Length; ++i) {
                ref var threadItem = ref state->batches.items[state, i];
                if (entId >= threadItem.items.Capacity) {
                    JobUtils.Lock(ref threadItem.lockSpinner);
                    if (entId >= threadItem.items.Capacity) {
                        threadItem.items.Resize(ref state->allocator, entId + 1u);
                    }
                    JobUtils.Unlock(ref threadItem.lockSpinner);
                }
            }
            
        }

        [INLINE(256)]
        public static void Clear(State* state, in Ent ent) {

            state->batches.lockReadWrite.ReadBegin(state);
            if (ent.id >= state->batches.arr.Length) {
                state->batches.lockReadWrite.ReadEnd(state);
                return;
            }
            ref var item = ref state->batches.arr[state, ent.id];
            item.lockIndex.Lock();
            //UnityEngine.Debug.Log("Destroy: " + ent.id + " :: " + ent + ", stored: " + item.ent);
            item.Clear();
            item.lockIndex.Unlock();
            state->batches.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal static void OpenFromJob(State* state) {
            state->batches.workingLock.ReadBegin(state);
            JobUtils.Increment(ref state->batches.openIndex);
            state->batches.workingLock.ReadEnd(state);
        }

        [INLINE(256)]
        internal static void CloseFromJob(State* state) {
            state->batches.workingLock.ReadBegin(state);
            JobUtils.Decrement(ref state->batches.openIndex);
            state->batches.workingLock.ReadEnd(state);
        }

        [INLINE(256)]
        internal static void ApplyFromJob(State* state) {

            if (state->batches.openIndex > 0u) {
                return;
            }
            if (state->batches.items.Length == 0u) return;

            JobUtils.Increment(ref state->batches.openIndex);
            state->batches.workingLock.WriteBegin(state);

            // Collect
            var temp = new UnsafeList<uint>((int)state->batches.items.Length, Constants.ALLOCATOR_TEMP);
            for (uint i = 0u; i < state->batches.items.Length; ++i) {

                Batches.ApplyFromJobThread(state, i, ref temp);

            }
            // Sort
            {
                temp.Sort();
            }
            // Apply
            {
                state->batches.lockReadWrite.ReadBegin(state);
                for (int i = 0; i < temp.Length; ++i) {
                    var entId = temp[i];
                    ref var element = ref state->batches.arr[in state->allocator, entId];
                    if (element.Count > 0u) {
                        JobUtils.Lock(ref element.lockIndex);
                        if (element.Count > 0u) {
                            element.Apply(state, entId, ref state->archetypes);
                        }
                        JobUtils.Unlock(ref element.lockIndex);
                    }
                }
                state->batches.lockReadWrite.ReadEnd(state);
            }
            
            state->batches.workingLock.WriteEnd();
            JobUtils.Decrement(ref state->batches.openIndex);

        }
        
        [INLINE(256)]
        private static void ApplyFromJobThread(State* state, uint threadIndex, ref UnsafeList<uint> list) {

            ref var threadItem = ref state->batches.items[state, threadIndex];
            if (threadItem.Count == 0u) {
                return;
            }
            
            JobUtils.Lock(ref threadItem.lockSpinner);
            
            var count = threadItem.Count;
            if (count == 0u) {
                JobUtils.Unlock(ref threadItem.lockSpinner);
                return;
            }

            list.AddRange(threadItem.items.GetUnsafePtr(in state->allocator), (int)threadItem.items.Count);
            /*for (uint j = 0; j < threadItem.items.Count; ++j) {

                var entId = threadItem.items[in state->allocator, j];
                this.lockReadWrite.ReadBegin(state);
                ref var element = ref this.arr[in state->allocator, entId];
                if (element.Count > 0u) {
                    JobUtils.Lock(ref element.lockIndex);
                    if (element.Count > 0u) {
                        element.Apply(state, ref count, entId, ref state->archetypes);
                    }
                    JobUtils.Unlock(ref element.lockIndex);
                }
                this.lockReadWrite.ReadEnd(state);
                if (count == 0u) break;
                
            }*/
            
            threadItem.Count = 0u;
            threadItem.items.Clear();
            
            JobUtils.Unlock(ref threadItem.lockSpinner);

        }
        
        [BURST(CompileSynchronously = true)]
        [INLINE(256)]
        public static void Apply(State* state) {
            new ApplyJob() {
                state = state,
            }.Execute();
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, State* state) {
            var job = new ApplyJob() {
                state = state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, in World world) {
            var job = new ApplyJob() {
                state = world.state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static JobHandle Open(JobHandle jobHandle, State* state) {
            var job = new OpenJob() {
                state = state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static JobHandle Close(JobHandle jobHandle, State* state) {
            var job = new CloseJob() {
                state = state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static JobHandle Open(JobHandle jobHandle, in World world) {
            var job = new OpenJob() {
                state = world.state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static JobHandle Close(JobHandle jobHandle, in World world) {
            var job = new CloseJob() {
                state = world.state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.lockReadWrite.BurstMode(in allocator, state);
            this.items.BurstMode(in allocator, state);
            this.arr.BurstMode(in allocator, state);
        }

    }

    public unsafe partial struct Batches {

        [INLINE(256)]
        public static void OnEntityAdd(State* state, uint entId) {

            if (entId >= state->batches.arr.Length) {
                state->batches.lockReadWrite.WriteBegin(state);
                if (entId >= state->batches.arr.Length) {
                    state->batches.arr.Resize(ref state->allocator, entId + 1u, 2);
                }
                state->batches.lockReadWrite.WriteEnd();
            }
            Batches.OnEntityAddThreadItem(state, entId);

        }
        
        [INLINE(256)]
        internal static void Set_INTERNAL(uint typeId, in Ent ent, State* state) {
            
            E.IS_IN_TICK(state);

            if (ent.IsAlive() == false) return;
            
            state->batches.lockReadWrite.ReadBegin(state);
            ref var threadItem = ref state->batches.items[state, (uint)JobUtils.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref state->batches.arr[state, ent.id];
                item.lockIndex.Lock();
                item.entGen = ent.gen;
                {
                    var wasCount = item.Count;
                    {
                        threadItem.Count -= item.Count;
                        item.Add(typeId);
                        threadItem.Count += item.Count;
                    }
                    if (wasCount == 0u && item.Count > 0u) {
                        threadItem.items.Add(ref state->allocator, ent.id);
                    }
                }
                item.lockIndex.Unlock();
            }
            threadItem.lockSpinner.Unlock();
            state->batches.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(uint typeId, in Ent ent, State* state) {
            
            E.IS_IN_TICK(state);
            
            if (ent.IsAlive() == false) return;
            
            state->batches.lockReadWrite.ReadBegin(state);
            ref var threadItem = ref state->batches.items[state, (uint)JobUtils.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref state->batches.arr[state, ent.id];
                item.lockIndex.Lock();
                item.entGen = ent.gen;
                {
                    var wasCount = item.Count;
                    {
                        threadItem.Count -= item.Count;
                        item.Remove(typeId);
                        threadItem.Count += item.Count;
                    }
                    if (wasCount == 0u && item.Count > 0u) {
                        threadItem.items.Add(ref state->allocator, ent.id);
                    }
                }
                item.lockIndex.Unlock();
            }
            threadItem.lockSpinner.Unlock();
            state->batches.lockReadWrite.ReadEnd(state);
            
        }

    }

}