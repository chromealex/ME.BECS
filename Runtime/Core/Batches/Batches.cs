using Unity.Collections;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using static Cuts;
    using Jobs;
    using System.Runtime.InteropServices;
    using Unity.Jobs.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
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
    
    #if !ENABLE_BECS_FLAT_QUERIES
    [IgnoreProfiler]
    public struct BatchList {

        public TempBitArray list;
        public uint Count;
        public uint hash;
        public uint maxId;

        public bool IsCreated => this.list.IsCreated;

        [INLINE(256)]
        public void Add(uint value, ushort worldId) {

            if (this.list.IsCreated == false) {
                var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(worldId).Allocator.ToAllocator;
                this.list = new TempBitArray(StaticTypes.counter + 1u, ClearOptions.ClearMemory, allocator);
            }
            
            ++this.Count;
            if (value > this.maxId) this.maxId = value;
            this.hash ^= value;
            this.list.Set((int)value, true);

        }

        [INLINE(256)]
        public bool Remove(uint value) {

            if (this.list.IsCreated == false) return false;

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

        }

    }
    
    [IgnoreProfiler]
    public unsafe struct BatchItem {

        private BatchList addItems;
        private BatchList removeItems;
        public LockSpinner lockIndex;
        public ushort entGen;
        public uint Count;
        public bool IsCreated => this.addItems.IsCreated == true || this.removeItems.IsCreated == true;

        [INLINE(256)]
        public void Apply(safe_ptr<State> state, uint entId) {

            if (Ents.IsAlive(state, entId, out var gen) == false || gen != this.entGen) {
                this.addItems.Clear();
                this.removeItems.Clear();
                return;
            }

            {
                var addItems = ComponentsFastTrack.Create(this.addItems);
                var removeItems = ComponentsFastTrack.Create(this.removeItems);
                MemoryAllocator.ValidateConsistency(ref state.ptr->allocator);
                Archetypes.ApplyBatch(state, entId, in addItems, in removeItems);
                this.addItems.Clear();
                this.removeItems.Clear();
                this.Count = 0u;
            }

        }
        
        [INLINE(256)]
        public void Add(uint typeId, ushort worldId) {

            var removed = false;
            if (this.removeItems.Count > 0u) {
                removed = this.removeItems.Remove(typeId);
            }
            if (removed == false) this.addItems.Add(typeId, worldId);
            this.Count = this.addItems.Count + this.removeItems.Count;

        }

        [INLINE(256)]
        public void Remove(uint typeId, ushort worldId) {

            var removed = false;
            if (this.addItems.Count > 0u) {
                removed = this.addItems.Remove(typeId);
            }
            if (removed == false) this.removeItems.Add(typeId, worldId);
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
    #endif
    
    [IgnoreProfiler]
    [BURST]
    public unsafe partial struct Batches {

        #if !ENABLE_BECS_FLAT_QUERIES
        [StructLayout(LayoutKind.Sequential)]
        public struct ThreadItem {

            public UnsafeList<uint> items;
            public LockSpinner lockSpinner;

            [INLINE(256)]
            public void Dispose() {
                this.items.Dispose();
            }

            public uint GetReservedSizeInBytes() {
                return (uint)this.items.Length * TSize<uint>.size;
            }

        }
        
        public ThreadCacheLine<ThreadItem> items;
        public NativeArray<BatchItem> arr;
        public uint openIndex;
        public ReadWriteNativeSpinner workingLock;
        internal ReadWriteNativeSpinner lockReadWrite;

        public static uint GetReservedSizeInBytes(ushort worldId) {

            var batches = WorldBatches.storage.Data.Get(worldId);
            if (batches.items.IsCreated == false) return 0u;

            var size = TSize<Batches>.size;
            for (uint i = 0u; i < batches.items.Count; ++i) {
                ref var item = ref batches.items[i];
                size += item.GetReservedSizeInBytes();
            }
            for (uint i = 0u; i < batches.arr.Length; ++i) {
                var item = batches.arr[(int)i];
                size += item.GetReservedSizeInBytes();
            }
            
            return size;
            
        }

        [INLINE(256)]
        public static Batches Create(ushort worldId, uint entitiesCapacity) {
            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(worldId).Allocator.ToAllocator;
            var batches = new Batches() {
                items = new ThreadCacheLine<ThreadItem>(allocator),
                arr = CollectionHelper.CreateNativeArray<BatchItem>((int)entitiesCapacity, allocator),
                lockReadWrite = ReadWriteNativeSpinner.Create(allocator),
                openIndex = 0u,
                workingLock = ReadWriteNativeSpinner.Create(allocator),
            };
            for (uint i = 0u; i < batches.items.Count; ++i) {
                ref var item = ref batches.items[i];
                item.items = new UnsafeList<uint>((int)entitiesCapacity, allocator);
            }
            return batches;
        }

        [INLINE(256)]
        public void Dispose() {
            for (uint i = 0u; i < this.items.Count; ++i) {
                this.items[i].Dispose();
            }
            this.items.Dispose();
            this.workingLock.Dispose();
            this.lockReadWrite.Dispose();
        }

        [INLINE(256)]
        public static void SetCapacity(ushort worldId, uint capacity) {
            Batches.OnEntityAdd(worldId, capacity, growFactor: 1);
        }

        [INLINE(256)]
        public static void OnEntityAddThreadItem(ushort worldId, uint entId) {

            var size = (int)(entId + 1u);
            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            for (uint i = 0u; i < batches.items.Count; ++i) {
                ref var threadItem = ref batches.items[i];
                if (entId >= threadItem.items.Capacity) {
                    threadItem.lockSpinner.Lock();
                    if (entId >= threadItem.items.Capacity) {
                        threadItem.items.Capacity = size;
                    }
                    threadItem.lockSpinner.Unlock();
                }
            }
            
        }

        [INLINE(256)]
        public ref BatchItem GetBatchItem(uint index) {
            return ref *((BatchItem*)this.arr.GetUnsafePtr() + index);
        }
        
        [INLINE(256)]
        public static void Clear(ushort worldId, in Ent ent) {

            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            batches.lockReadWrite.ReadBegin();
            if (ent.id >= batches.arr.Length) {
                batches.lockReadWrite.ReadEnd();
                return;
            }

            ref var item = ref batches.GetBatchItem(ent.id);
            item.lockIndex.Lock();
            //UnityEngine.Debug.Log("Destroy: " + ent.id + " :: " + ent + ", stored: " + item.ent);
            item.Clear();
            item.lockIndex.Unlock();
            batches.lockReadWrite.ReadEnd();

        }

        [INLINE(256)]
        internal static void ApplyFromJob(ushort worldId, safe_ptr<State> state) {

            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            if (batches.openIndex > 0u) return;
            if (batches.items.Count == 0u) return;

            JobUtils.Increment(ref batches.openIndex);
            batches.workingLock.WriteBegin();

            // Collect
            var temp = new UnsafeList<uint>((int)batches.items.Count, Constants.ALLOCATOR_TEMP);
            for (uint i = 0u; i < batches.items.Count; ++i) {

                ref var threadItem = ref batches.items[i];
                if (threadItem.items.Length == 0u) {
                    continue;
                }
            
                threadItem.lockSpinner.Lock();
            
                var count = threadItem.items.Length;
                if (count == 0u) {
                    threadItem.lockSpinner.Unlock();
                    continue;
                }

                temp.AddRange(threadItem.items.Ptr, threadItem.items.Length);
                
                threadItem.items.Clear();

                threadItem.lockSpinner.Unlock();

            }
            // Sort
            {
                temp.Sort();
            }
            // Apply
            {
                batches.lockReadWrite.ReadBegin();
                for (int i = 0; i < temp.Length; ++i) {
                    var entId = temp[i];
                    ref var element = ref batches.GetBatchItem(entId);
                    if (element.Count > 0u) {
                        element.lockIndex.Lock();
                        if (element.Count > 0u) {
                            element.Apply(state, entId);
                        }
                        element.lockIndex.Unlock();
                    }
                }
                batches.lockReadWrite.ReadEnd();
            }
            temp.Dispose();
            
            batches.workingLock.WriteEnd();
            JobUtils.Decrement(ref batches.openIndex);

        }
        #endif
        
        [BURST]
        [INLINE(256)]
        public static void Apply(in World world) {
            Apply(world.id, world.state);
        }

        [BURST]
        [INLINE(256)]
        public static void Apply(ushort worldId, in safe_ptr<State> state) {
            #if !ENABLE_BECS_FLAT_QUERIES
            new ApplyJob() {
                worldId = worldId,
                state = state,
            }.Execute();
            #endif
            new ApplyFreeJob() {
                state = state,
            }.Execute();
            new ApplyDestroyedJob() {
                state = state,
            }.Execute();
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, ushort worldId, safe_ptr<State> state) {
            #if !ENABLE_BECS_FLAT_QUERIES
            var handle1 = new ApplyJob() { 
                state = state,
                worldId = worldId,
                #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                safety = new SafetyComponentContainerRW<TNull>(state, Context.world.id),
                #endif
            }.ScheduleSingle(jobHandle);
            #endif
            var handle2 = new ApplyFreeJob() { 
                state = state,
            }.ScheduleSingle(jobHandle);
            var handle3 = new ApplyDestroyedJob() { 
                state = state,
            }.ScheduleSingle(jobHandle);
            #if ENABLE_BECS_FLAT_QUERIES
            var handle = JobHandle.CombineDependencies(handle2, handle3);
            #else
            var handle = JobHandle.CombineDependencies(handle1, handle2, handle3);
            #endif
            state.ptr->lastApplyHandle = JobHandle.CombineDependencies(state.ptr->lastApplyHandle, handle);
            return handle;
        }

        [INLINE(256)]
        public static JobHandle Apply(JobHandle jobHandle, in World world) {
            #if ENABLE_BECS_FLAT_QUERIES
            var state = world.state;
            state.ptr->lastApplyHandle = JobHandle.CombineDependencies(state.ptr->lastApplyHandle, jobHandle);
            return jobHandle;
            #else
            return Apply(jobHandle, world.id, world.state);
            #endif
        }

    }
    
    #if !ENABLE_BECS_FLAT_QUERIES
    public struct WorldBatches {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<Batches>> storage = Unity.Burst.SharedStatic<Internal.Array<Batches>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldBatches>(TAlign<Internal.Array<Batches>>.align, 110L);

        [INLINE(256)]
        public static void AddWorld(in World world) {
            
            storage.Data.Resize(world.id + 1u);
            storage.Data.Get(world.id) = Batches.Create(world.id, 100u);

        }

        [INLINE(256)]
        public static void DisposeWorld(in World world) {

            if (world.id >= storage.Data.Length) return;
            storage.Data.Get(world.id).Dispose();

        }

    }
    #endif

    public unsafe partial struct Batches {

        [INLINE(256)]
        public static void OnEntityAdd(ushort worldId, uint entId, byte growFactor = 2) {

            #if !ENABLE_BECS_FLAT_QUERIES
            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            if (entId >= batches.arr.Length) {
                batches.lockReadWrite.WriteBegin();
                if (entId >= batches.arr.Length) {
                    var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(worldId).Allocator.ToAllocator;
                    var newArr = CollectionHelper.CreateNativeArray<BatchItem>((int)(entId + 1u) * growFactor, allocator);
                    _memmove((safe_ptr)batches.arr.GetUnsafePtr(), (safe_ptr)newArr.GetUnsafePtr(), TSize<BatchItem>.size * (uint)batches.arr.Length);
                    batches.arr.Dispose();
                    batches.arr = newArr;
                }
                batches.lockReadWrite.WriteEnd();
            }
            Batches.OnEntityAddThreadItem(worldId, entId);
            #endif

        }

        [INLINE(256)]
        internal static void Set_INTERNAL(uint typeId, in Ent ent) {
            
            if (ent.IsAlive() == false) return;

            #if ENABLE_BECS_FLAT_QUERIES
            {
                var state = ent.World.state;
                state.ptr->entities.OnAddComponent(state, ent.id, typeId);
                var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->SetBit(state, ent.id, true, typeId);
            }
            #else
            var worldId = ent.worldId;
            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            batches.lockReadWrite.ReadBegin();
            ref var threadItem = ref batches.items[(uint)JobsUtility.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref batches.GetBatchItem(ent.id);
                item.lockIndex.Lock();
                item.entGen = ent.gen;
                {
                    var wasCount = item.Count;
                    {
                        item.Add(typeId, ent.worldId);
                    }
                    if (wasCount == 0u && item.Count > 0u) {
                        threadItem.items.Add(ent.id);
                    }
                }
                item.lockIndex.Unlock();
            }
            threadItem.lockSpinner.Unlock();
            batches.lockReadWrite.ReadEnd();
            #endif

        }

        [INLINE(256)]
        internal static void Remove_INTERNAL(uint typeId, in Ent ent) {
            
            if (ent.IsAlive() == false) return;
            
            #if ENABLE_BECS_FLAT_QUERIES
            {
                var state = ent.World.state;
                state.ptr->entities.OnRemoveComponent(state, ent.id, typeId);
                var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
                storage.ptr->SetBit(state, ent.id, false, typeId);
            }
            #else
            var worldId = ent.worldId;
            ref var batches = ref WorldBatches.storage.Data.Get(worldId);
            batches.lockReadWrite.ReadBegin();
            ref var threadItem = ref batches.items[(uint)JobsUtility.ThreadIndex];
            threadItem.lockSpinner.Lock();
            {
                ref var item = ref batches.GetBatchItem(ent.id);
                item.lockIndex.Lock();
                item.entGen = ent.gen;
                {
                    var wasCount = item.Count;
                    {
                        item.Remove(typeId, ent.worldId);
                    }
                    if (wasCount == 0u && item.Count > 0u) {
                        threadItem.items.Add(ent.id);
                    }
                }
                item.lockIndex.Unlock();
            }
            threadItem.lockSpinner.Unlock();
            batches.lockReadWrite.ReadEnd();
            #endif
            
        }

    }

}