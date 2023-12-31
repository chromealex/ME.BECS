using Unity.Collections;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    
    public struct BatchList {

        public TempBitArray list;
        public uint Count;
        public uint hash;
        public uint maxId;

        public bool isCreated => this.list.isCreated;

        [INLINE(256)]
        public void Add(uint value) {

            if (this.list.isCreated == false) this.list = new TempBitArray(StaticTypes.counter + 1u, ClearOptions.ClearMemory, Constants.ALLOCATOR_TEMP_ST);
            
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
        public void Clear() {

            this.list.Dispose();
            this = default;

        }

    }
    
    public unsafe struct BatchItem {

        private BatchList addItems;
        private BatchList removeItems;
        public LockSpinner lockIndex;
        public uint Count;
        public bool isCreated => this.addItems.isCreated == true || this.removeItems.isCreated == true;

        [INLINE(256)]
        public void Apply(State* state, ref uint count, uint entId, ref Archetypes archetypes) {

            var addItems = ComponentsFastTrack.Create(this.addItems);
            var removeItems = ComponentsFastTrack.Create(this.removeItems);
            MemoryAllocatorExt.ValidateConsistency(state->allocator);
            archetypes.ApplyBatch(state, entId, in addItems, in removeItems);
            if (this.addItems.Count > 0u) this.addItems.Clear();
            if (this.removeItems.Count > 0u) this.removeItems.Clear();
            count -= this.Count;
            this.Count = 0u;
            addItems.Dispose();
            removeItems.Dispose();

        }
        
        [INLINE(256)]
        public void Add(uint typeId) {

            if (this.removeItems.Count > 0u) this.removeItems.Remove(typeId);
            this.addItems.Add(typeId);
            this.Count = this.addItems.Count + this.removeItems.Count;

        }

        [INLINE(256)]
        public void Remove(uint typeId) {

            if (this.addItems.Count > 0u) this.addItems.Remove(typeId);
            this.removeItems.Add(typeId);
            this.Count = this.addItems.Count + this.removeItems.Count;
            
        }

        [INLINE(256)]
        public void Clear() {
            
            if (this.addItems.Count > 0u) this.addItems.Clear();
            if (this.removeItems.Count > 0u) this.removeItems.Clear();
            this.Count = 0;

        }

        public uint GetReservedSizeInBytes() {
            var size = 0u;
            size += this.addItems.list.GetReservedSizeInBytes();
            size += this.removeItems.list.GetReservedSizeInBytes();
            return size;
        }

    }
    
    [BURST]
    public unsafe partial struct Batches {

        public struct ThreadItem {

            public List<uint> items;
            public uint Count;
            public LockSpinner lockIndex;

            public uint GetReservedSizeInBytes() {
                return this.items.GetReservedSizeInBytes();
            }

        }
        
        public MemArrayThreadCacheLine<ThreadItem> items;
        public MemArray<BatchItem> arr;

        internal ReadWriteSpinner lockReadWrite;

        public uint GetReservedSizeInBytes(State* state) {

            if (this.items.isCreated == false) return 0u;

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
                arr = new MemArray<BatchItem>(ref state->allocator, entitiesCapacity, growFactor: 2),
                lockReadWrite = ReadWriteSpinner.Create(state),
            };
            for (uint i = 0u; i < batches.items.Length; ++i) {
                ref var item = ref batches.items[state, i];
                item.items = new List<uint>(ref state->allocator, entitiesCapacity);
                item.Count = 0u;
            }
            batches.InitializeThreadTasks(state);
            return batches;
        }

        [INLINE(256)]
        public void OnEntityAddThreadItem(State* state, uint entId) {
            
            for (uint i = 0u; i < this.items.Length; ++i) {
                ref var item = ref this.items[state, i];
                if (entId >= item.items.Capacity) {
                    JobUtils.Lock(ref item.lockIndex);
                    if (entId >= item.items.Capacity) {
                        item.items.Resize(ref state->allocator, entId + 1u);
                    }
                    JobUtils.Unlock(ref item.lockIndex);
                }
            }
            
        }

        [INLINE(256)]
        public void Clear(State* state, uint entId) {

            this.lockReadWrite.ReadBegin(state);
            if (entId >= this.arr.Length) {
                this.lockReadWrite.ReadEnd(state);
                return;
            }
            ref var item = ref this.arr[state, entId];
            item.Clear();
            this.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal void ApplyFromJob(State* state) {

            if (this.items.Length == 0u) return;
            
            for (uint i = 0u; i < this.items.Length; ++i) {

                this.ApplyFromJobThread(state, i);

            }

        }
        
        [INLINE(256)]
        internal void ApplyFromJobThread(State* state, uint threadIndex) {

            ref var threadItem = ref this.items[state, threadIndex];
            JobUtils.Lock(ref threadItem.lockIndex);
            
            var count = threadItem.Count;
            if (count == 0u) {
                JobUtils.Unlock(ref threadItem.lockIndex);
                return;
            }

            for (uint j = 0; j < threadItem.items.Count; ++j) {

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
                
            }
            
            threadItem.Count = 0u;
            threadItem.items.Clear();
            
            JobUtils.Unlock(ref threadItem.lockIndex);

        }
        
        [BURST]
        [INLINE(256)]
        public static void Apply(State* state) {
            state->batches.ApplyFromJob(state);
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, State* state) {
            var job = new ApplyJobParallel() {
                state = state,
            };
            return job.Schedule((int)JobUtils.ThreadsCount, 1, jobHandle);
        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.lockReadWrite.BurstMode(in allocator, state);
            this.items.BurstMode(in allocator, state);
            this.arr.BurstMode(in allocator, state);
        }

    }

    [BURST]
    public static unsafe partial class BatchesExt {

        [INLINE(256)]
        public static void OnEntityAdd(this ref Batches batches, State* state, uint entId) {

            if (entId >= batches.arr.Length) {
                batches.lockReadWrite.WriteBegin(state);
                if (entId >= batches.arr.Length) {
                    batches.arr.Resize(ref state->allocator, entId + 1u);
                }
                batches.lockReadWrite.WriteEnd();
            }
            batches.OnEntityAddThreadItem(state, entId);

        }
        
        [INLINE(256)]
        internal static void Set_INTERNAL(this ref Batches batches, uint typeId, uint entId, State* state) {
            
            E.IS_IN_TICK(state);
            
            batches.lockReadWrite.ReadBegin(state);
            ref var item = ref batches.arr[state, entId];
            JobUtils.Lock(ref item.lockIndex);
            {
                var wasCount = item.Count;
                ref var threadItem = ref batches.items[state, (uint)Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex];
                JobUtils.Lock(ref threadItem.lockIndex);
                {
                    threadItem.Count -= item.Count;
                    item.Add(typeId);
                    threadItem.Count += item.Count;
                }
                if (wasCount == 0u && item.Count > 0u) {
                    threadItem.items.Add(ref state->allocator, entId);
                }
                JobUtils.Unlock(ref threadItem.lockIndex);
            }
            JobUtils.Unlock(ref item.lockIndex);
            batches.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(this ref Batches batches, uint typeId, uint entId, State* state) {
            
            E.IS_IN_TICK(state);
            
            batches.lockReadWrite.ReadBegin(state);
            ref var item = ref batches.arr[state, entId];
            JobUtils.Lock(ref item.lockIndex);
            {
                var wasCount = item.Count;
                ref var threadItem = ref batches.items[state, (uint)Unity.Jobs.LowLevel.Unsafe.JobsUtility.ThreadIndex];
                JobUtils.Lock(ref threadItem.lockIndex);
                {
                    threadItem.Count -= item.Count;
                    item.Remove(typeId);
                    threadItem.Count += item.Count;
                    if (wasCount == 0u && item.Count > 0u) {
                        threadItem.items.Add(ref state->allocator, entId);
                    }
                }
                JobUtils.Unlock(ref threadItem.lockIndex);
            }
            JobUtils.Unlock(ref item.lockIndex);
            batches.lockReadWrite.ReadEnd(state);
            
        }

    }

}