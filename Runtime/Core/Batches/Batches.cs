using Unity.Collections;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    using System.Runtime.InteropServices;
    
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

        public Ent ent;
        private BatchList addItems;
        private BatchList removeItems;
        public LockSpinner lockIndex;
        public uint Count;
        public bool isCreated => this.addItems.isCreated == true || this.removeItems.isCreated == true;

        [INLINE(256)]
        public void Apply(State* state, uint entId, ref Archetypes archetypes) {

            if (this.ent.IsAlive() == false) {
                this.addItems.Dispose();
                this.removeItems.Dispose();
                return;
            }

            {
                var addItems = ComponentsFastTrack.Create(this.addItems);
                var removeItems = ComponentsFastTrack.Create(this.removeItems);
                MemoryAllocator.ValidateConsistency(ref state->allocator);
                archetypes.ApplyBatch(state, entId, in addItems, in removeItems);
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
            var size = 0u;
            size += this.addItems.list.GetReservedSizeInBytes();
            size += this.removeItems.list.GetReservedSizeInBytes();
            return size;
        }

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct Batches {

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

        public uint GetReservedSizeInBytes(State* state) {

            if (this.items.IsCreated == false) return 0u;

            var size = 0u;
            for (uint i = 0u; i < this.items.Length; ++i) {
                ref var item = ref this.items[in state->allocator, i];
                size += item.GetReservedSizeInBytes();
            }
            for (uint i = 0u; i < this.arr.Length; ++i) {
                ref var item = ref this.arr[in state->allocator, i];
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
        public void OnEntityAddThreadItem(State* state, uint entId) {
            
            for (uint i = 0u; i < this.items.Length; ++i) {
                ref var threadItem = ref this.items[state, i];
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
        public void Clear(State* state, in Ent ent) {

            this.lockReadWrite.ReadBegin(state);
            if (ent.id >= this.arr.Length) {
                this.lockReadWrite.ReadEnd(state);
                return;
            }
            ref var item = ref this.arr[state, ent.id];
            item.lockIndex.Lock();
            //UnityEngine.Debug.Log("Destroy: " + ent.id + " :: " + ent + ", stored: " + item.ent);
            item.Clear();
            item.lockIndex.Unlock();
            this.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal void OpenFromJob(State* state) {
            this.workingLock.ReadBegin(state);
            JobUtils.Increment(ref this.openIndex);
            this.workingLock.ReadEnd(state);
        }

        [INLINE(256)]
        internal void CloseFromJob(State* state) {
            this.workingLock.ReadBegin(state);
            JobUtils.Decrement(ref this.openIndex);
            this.workingLock.ReadEnd(state);
        }

        [INLINE(256)]
        internal void ApplyFromJob(State* state) {

            if (this.openIndex > 0u) {
                return;
            }
            if (this.items.Length == 0u) return;

            JobUtils.Increment(ref this.openIndex);
            this.workingLock.WriteBegin(state);

            // Collect
            var temp = new UnsafeList<uint>((int)this.items.Length, Constants.ALLOCATOR_TEMP);
            for (uint i = 0u; i < this.items.Length; ++i) {

                this.ApplyFromJobThread(state, i, ref temp);

            }
            // Sort
            {
                temp.Sort();
            }
            // Apply
            {
                this.lockReadWrite.ReadBegin(state);
                for (int i = 0; i < temp.Length; ++i) {
                    var entId = temp[i];
                    ref var element = ref this.arr[in state->allocator, entId];
                    if (element.Count > 0u) {
                        JobUtils.Lock(ref element.lockIndex);
                        if (element.Count > 0u) {
                            element.Apply(state, entId, ref state->archetypes);
                        }
                        JobUtils.Unlock(ref element.lockIndex);
                    }
                }
                this.lockReadWrite.ReadEnd(state);
            }
            
            this.workingLock.WriteEnd();
            JobUtils.Decrement(ref this.openIndex);

        }
        
        [INLINE(256)]
        private void ApplyFromJobThread(State* state, uint threadIndex, ref UnsafeList<uint> list) {

            ref var threadItem = ref this.items[state, threadIndex];
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

    [BURST(CompileSynchronously = true)]
    public static unsafe partial class BatchesExt {

        [INLINE(256)]
        public static void OnEntityAdd(this ref Batches batches, State* state, uint entId) {

            if (entId >= batches.arr.Length) {
                batches.lockReadWrite.WriteBegin(state);
                if (entId >= batches.arr.Length) {
                    batches.arr.Resize(ref state->allocator, entId + 1u, 2);
                }
                batches.lockReadWrite.WriteEnd();
            }
            batches.OnEntityAddThreadItem(state, entId);

        }
        
        [INLINE(256)]
        internal static void Set_INTERNAL(this ref Batches batches, uint typeId, in Ent ent, State* state) {
            
            E.IS_IN_TICK(state);

            if (ent.IsAlive() == false) return;
            
            batches.lockReadWrite.ReadBegin(state);
            ref var threadItem = ref batches.items[state, (uint)Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref batches.arr[state, ent.id];
                item.lockIndex.Lock();
                item.ent = ent;
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
            batches.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(this ref Batches batches, uint typeId, in Ent ent, State* state) {
            
            E.IS_IN_TICK(state);
            
            if (ent.IsAlive() == false) return;
            
            batches.lockReadWrite.ReadBegin(state);
            ref var threadItem = ref batches.items[state, (uint)Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref batches.arr[state, ent.id];
                item.lockIndex.Lock();
                item.ent = ent;
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
            batches.lockReadWrite.ReadEnd(state);
            
        }

    }

}