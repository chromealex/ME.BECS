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
        public static void Apply(this ref SystemContext context) {
            context.SetDependency(Batches.Apply(context.dependsOn, in context.world));
        } 

        [INLINE(256)]
        public static JobHandle Apply(this in SystemContext context, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in context.world);
        }

        [INLINE(256)]
        public static JobHandle Apply(this in World world, JobHandle dependsOn) {
            return Batches.Apply(dependsOn, in world);
        }

    }
    
    public struct BatchList : IIsCreated {

        public TempBitArray list;
        public uint Count;
        public uint hash;
        public uint maxId;

        public bool IsCreated => this.list.IsCreated;

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
        public void Clear() {

            if (this.list.IsCreated == true) this.list.Clear();
            this.Count = 0u;
            this.hash = 0u;
            this.maxId = 0u;
            //if (this.list.IsCreated == true) this.list.Dispose();
            //this = default;

        }

    }
    
    public unsafe struct BatchItem : IIsCreated {

        private BatchList addItems;
        private BatchList removeItems;
        public LockSpinner lockIndex;
        public ushort entGen;
        public uint Count;
        public bool IsCreated => this.addItems.IsCreated == true || this.removeItems.IsCreated == true;

        [INLINE(256)]
        public void Apply(safe_ptr<State> state, uint entId) {

            this.lockIndex.Lock();
            if (Ents.IsAlive(state, entId, out var gen) == false || gen != this.entGen) {
                this.Clear();
                this.lockIndex.Unlock();
                return;
            }

            {
                var addItems = ComponentsFastTrack.Create(this.addItems);
                var removeItems = ComponentsFastTrack.Create(this.removeItems);
                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                UnityEngine.Debug.Log("Apply Ent: " + entId + ":" + addItems.ToString() + ":" + removeItems.ToString());
                Archetypes.ApplyBatch(state, entId, in addItems, in removeItems);
                this.Clear();
            }
            this.lockIndex.Unlock();

        }
        
        [INLINE(256)]
        public void Add(uint typeId) {

            var removed = false;
            if (this.removeItems.Count > 0u) {
                removed = this.removeItems.Remove(typeId);
            }
            if (removed == false) this.addItems.Add(typeId);
            UnityEngine.Debug.Log("ADDED tid: " + typeId + ", this.removeItems.Count: " + this.removeItems.Count + ", this.addItems.Count: " + this.addItems.Count + ", removed: " + removed);
            this.Count = this.addItems.Count + this.removeItems.Count;
            
        }

        [INLINE(256)]
        public void Remove(uint typeId) {

            var removed = false;
            if (this.addItems.Count > 0u) {
                removed = this.addItems.Remove(typeId);
            }
            if (removed == false) this.removeItems.Add(typeId);
            UnityEngine.Debug.Log("REMOVED tid: " + typeId + ", this.removeItems.Count: " + this.removeItems.Count + ", this.addItems.Count: " + this.addItems.Count + ", removed: " + removed);
            this.Count = this.addItems.Count + this.removeItems.Count;
            
        }

        [INLINE(256)]
        public void Clear() {
            
            this.addItems.Clear();
            this.removeItems.Clear();
            this.Count = 0u;

        }

        public uint GetReservedSizeInBytes() {
            var size = TSize<BatchItem>.size;
            size += this.addItems.list.GetReservedSizeInBytes();
            size += this.removeItems.list.GetReservedSizeInBytes();
            return size;
        }

    }
    
    [BURST]
    public unsafe partial struct Batches {

        [StructLayout(LayoutKind.Sequential)]
        public struct ThreadItem {

            public List<uint> items;
            public LockSpinner lockSpinner;
            public MemArray<BatchItem> batches;

            [INLINE(256)]
            public void Add(safe_ptr<State> state, in Ent ent, uint typeId) {
                this.batches.Resize(ref state.ptr->allocator, ent.id + 1u, 2, ClearOptions.ClearMemory);
                this.items.Add(ref state.ptr->allocator, ent.id);
                var marker = new Unity.Profiling.ProfilerMarker("ThreadItem: ADD " + ent.id);
                marker.Begin();
                ref var batchItem = ref this.batches[state, ent.id];
                batchItem.lockIndex.Lock();
                if (batchItem.entGen != ent.gen) batchItem.Clear();
                batchItem.entGen = ent.gen;
                batchItem.Add(typeId);
                batchItem.lockIndex.Unlock();
                marker.End();
            }

            [INLINE(256)]
            public void Remove(safe_ptr<State> state, in Ent ent, uint typeId) {
                if (ent.id >= this.batches.Length) return;
                var marker = new Unity.Profiling.ProfilerMarker("ThreadItem: REMOVE " + ent.id);
                marker.Begin();
                this.items.Add(ref state.ptr->allocator, ent.id);
                ref var batchItem = ref this.batches[state, ent.id];
                batchItem.lockIndex.Lock();
                batchItem.Remove(typeId);
                batchItem.lockIndex.Unlock();
                marker.End();
            }

            public uint GetReservedSizeInBytes() {
                return this.items.GetReservedSizeInBytes();
            }

        }
        
        public MemArrayThreadCacheLine<ThreadItem> items;
        public MemArray<BatchItem> arr;
        public uint openIndex;
        public ReadWriteSpinner workingLock;
        internal ReadWriteSpinner lockReadWrite;

        public static uint GetReservedSizeInBytes(safe_ptr<State> state) {

            if (state.ptr->batches.items.IsCreated == false) return 0u;

            var size = TSize<Batches>.size;
            for (uint i = 0u; i < state.ptr->batches.items.Length; ++i) {
                ref var item = ref state.ptr->batches.items[in state.ptr->allocator, i];
                size += item.GetReservedSizeInBytes();
            }
            for (uint i = 0u; i < state.ptr->batches.arr.Length; ++i) {
                ref var item = ref state.ptr->batches.arr[in state.ptr->allocator, i];
                size += item.GetReservedSizeInBytes();
            }
            
            return size;
            
        }

        [INLINE(256)]
        public static Batches Create(safe_ptr<State> state, uint entitiesCapacity) {
            var batches = new Batches() {
                items = new MemArrayThreadCacheLine<ThreadItem>(ref state.ptr->allocator),
                arr = new MemArray<BatchItem>(ref state.ptr->allocator, entitiesCapacity),
                lockReadWrite = ReadWriteSpinner.Create(state),
                openIndex = 0u,
                workingLock = ReadWriteSpinner.Create(state),
            };
            for (uint i = 0u; i < batches.items.Length; ++i) {
                ref var item = ref batches.items[state, i];
                item.items = new List<uint>(ref state.ptr->allocator, entitiesCapacity);
            }
            return batches;
        }

        [INLINE(256)]
        public static void OnEntityAddThreadItem(safe_ptr<State> state, uint entId) {
            
            for (uint i = 0u; i < state.ptr->batches.items.Length; ++i) {
                ref var threadItem = ref state.ptr->batches.items[state, i];
                if (entId >= threadItem.items.Capacity) {
                    JobUtils.Lock(ref threadItem.lockSpinner);
                    if (entId >= threadItem.items.Capacity) {
                        threadItem.items.Resize(ref state.ptr->allocator, entId + 1u);
                    }
                    JobUtils.Unlock(ref threadItem.lockSpinner);
                }
            }
            
        }

        [INLINE(256)]
        public static void Clear(safe_ptr<State> state, in Ent ent) {

            state.ptr->batches.lockReadWrite.ReadBegin(state);
            if (ent.id >= state.ptr->batches.arr.Length) {
                state.ptr->batches.lockReadWrite.ReadEnd(state);
                return;
            }
            ref var item = ref state.ptr->batches.arr[state, ent.id];
            item.lockIndex.Lock();
            //UnityEngine.Debug.Log("Destroy: " + ent.id + " :: " + ent + ", stored: " + item.ent);
            item.Clear();
            item.lockIndex.Unlock();
            state.ptr->batches.lockReadWrite.ReadEnd(state);

        }

        [INLINE(256)]
        public static void ApplyThreads(safe_ptr<State> state) {

            for (uint i = 0u; i < JobUtils.ThreadsCount; ++i) {
                ApplyThread(state, i);
            }
            
        }

        [INLINE(256)]
        public static void ApplyThread(safe_ptr<State> state) {
            ApplyThread(state, JobUtils.ThreadIndex);
        }

        [INLINE(256)]
        public static void ApplyThread(safe_ptr<State> state, uint threadIndex) {

            UnityEngine.Debug.Log("Batches.ApplyThread");
            ref var threadItem = ref state.ptr->batches.items[state, threadIndex];
            threadItem.lockSpinner.Lock();
            for (uint i = 0; i < threadItem.items.Count; ++i) {
                var entId = threadItem.items[state, i];
                threadItem.batches[state, entId].Apply(state, entId);
            }
            threadItem.items.Clear();
            threadItem.lockSpinner.Unlock();
            
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, in World world) {
            var job = new ApplyJob() {
                state = world.state,
            };
            return job.ScheduleSingle(jobHandle);
        }

        [INLINE(256)]
        public static void Apply(safe_ptr<State> state) {
            Batches.ApplyThread(state, JobUtils.ThreadIndex);
            var job = new ApplyEntJob() {
                state = state,
            };
            job.Execute();
        }

        [INLINE(256)]
        public static void Prepare<T>(in World world) where T : struct {
            SystemsRuntimeStaticInfo.AddEntCreation(in world, JobStaticInfo<T>.IsEntCreation);
            SystemsRuntimeStaticInfo.AddEntDestroyed(in world, JobStaticInfo<T>.IsEntDestroyed);
        }

        [INLINE(256)]
        public static JobHandle ApplySystems(JobHandle jobHandle, in World world) {
            if (SystemsRuntimeStaticInfo.IsEntCreation(in world) == true && SystemsRuntimeStaticInfo.IsEntDestroyed(in world) == true) {
                var job = new ApplyEntJob() {
                    state = world.state,
                };
                jobHandle = job.ScheduleSingle(jobHandle);
            } else if (SystemsRuntimeStaticInfo.IsEntCreation(in world) == true) {
                var job = new ApplyEntCreateJob() {
                    state = world.state,
                };
                jobHandle = job.ScheduleSingle(jobHandle);
            } else if (SystemsRuntimeStaticInfo.IsEntDestroyed(in world) == true) {
                var job = new ApplyDestroyedJob() {
                    state = world.state,
                };
                jobHandle = job.ScheduleSingle(jobHandle);
            }
            SystemsRuntimeStaticInfo.SetEntCreation(in world, false);
            SystemsRuntimeStaticInfo.SetEntDestroyed(in world, false);
            return jobHandle;
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
        public static void OnEntityAdd(safe_ptr<State> state, uint entId) {

            if (entId >= state.ptr->batches.arr.Length) {
                state.ptr->batches.lockReadWrite.WriteBegin(state);
                if (entId >= state.ptr->batches.arr.Length) {
                    state.ptr->batches.arr.Resize(ref state.ptr->allocator, entId + 1u, 2);
                }
                state.ptr->batches.lockReadWrite.WriteEnd();
            }
            Batches.OnEntityAddThreadItem(state, entId);

        }

        [INLINE(256)]
        internal static void Set_INTERNAL(uint typeId, in Ent ent, safe_ptr<State> state) {
            
            E.IS_IN_TICK(state);

            if (ent.IsAlive() == false) return;
            
            ref var threadItem = ref state.ptr->batches.items[state, JobUtils.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                threadItem.Add(state, in ent, typeId);
            }
            threadItem.lockSpinner.Unlock();
            
        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(uint typeId, in Ent ent, safe_ptr<State> state) {
            
            E.IS_IN_TICK(state);
            
            if (ent.IsAlive() == false) return;
            
            ref var threadItem = ref state.ptr->batches.items[state, JobUtils.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                threadItem.Remove(state, in ent, typeId);
            }
            threadItem.lockSpinner.Unlock();
            
        }

    }

}