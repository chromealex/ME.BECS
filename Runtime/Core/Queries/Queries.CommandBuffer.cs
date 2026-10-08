namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;

    /// <summary>
    /// Defines command buffer job parallel state and operations.
    /// </summary>
    public readonly unsafe struct CommandBufferJobParallel {

        /// <summary>
        /// Buffer used to exchange or store the associated data.
        /// </summary>
        public readonly safe_ptr<CommandBuffer> buffer;
        /// <summary>
        /// Index of this entry within its containing storage.
        /// </summary>
        public readonly uint index;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => this.buffer.ptr->count;
        /// <summary>
        /// Ent id used to locate the associated entry.
        /// </summary>
        public readonly uint entId;
        /// <summary>
        /// Ent gen used by <c>CommandBufferJobParallel</c>.
        /// </summary>
        public readonly ushort entGen;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent => new Ent(this.entId, this.entGen, this.buffer.ptr->worldId);

        /// <summary>
        /// Initializes <c>CommandBufferJobParallel</c> from the supplied buffer, index.
        /// </summary>
        [INLINE(256)]
        public CommandBufferJobParallel(safe_ptr<CommandBuffer> buffer, uint index) {
            this.buffer = buffer;
            this.index = index;
            this.entId = this.buffer.ptr->entities[index];
            this.entGen = Ents.GetGeneration(this.buffer.ptr->state, this.entId);
        }

        /// <summary>
        /// Reads the requested value from command buffer job parallel.
        /// </summary>
        [INLINE(256)]
        public ref readonly T Read<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Read<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Returns the requested entry from command buffer job parallel.
        /// </summary>
        [INLINE(256)]
        public ref T Get<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Get<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Stores the supplied value in command buffer job parallel.
        /// </summary>
        [INLINE(256)]
        public bool Set<T>(in T data) where T : unmanaged, IComponent {

            return this.buffer.ptr->Set<T>(this.entId, this.entGen, in data);

        }

        /// <summary>
        /// Removes the specified entry from command buffer job parallel.
        /// </summary>
        [INLINE(256)]
        public bool Remove<T>() where T : unmanaged, IComponent {

            return this.buffer.ptr->Remove<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Tests whether the requested entry is present.
        /// </summary>
        [INLINE(256)]
        public bool Has<T>(bool checkEnabled = true) where T : unmanaged, IComponent {

            return this.buffer.ptr->Has<T>(this.entId, this.entGen, checkEnabled);

        }

    }

    /// <summary>
    /// Defines command buffer job batch state and operations.
    /// </summary>
    public readonly unsafe struct CommandBufferJobBatch {

        private readonly safe_ptr<CommandBuffer> buffer;
        /// <summary>
        /// From index used to locate the associated entry.
        /// </summary>
        public readonly uint fromIndex;
        /// <summary>
        /// To index used to locate the associated entry.
        /// </summary>
        public readonly uint toIndex;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => this.buffer.ptr->count;

        /// <summary>
        /// Initializes <c>CommandBufferJobBatch</c> from the supplied buffer, from index, to index.
        /// </summary>
        [INLINE(256)]
        public CommandBufferJobBatch(safe_ptr<CommandBuffer> buffer, uint fromIndex, uint toIndex) {
            this.buffer = buffer;
            this.fromIndex = fromIndex;
            this.toIndex = toIndex;
        }

        /// <summary>
        /// Reads the requested value from command buffer job batch.
        /// </summary>
        [INLINE(256)]
        public ref readonly T Read<T>(uint index) where T : unmanaged, IComponent {

            var entId = this.buffer.ptr->entities[index];
            return ref this.buffer.ptr->Read<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        /// <summary>
        /// Returns the requested entry from command buffer job batch.
        /// </summary>
        [INLINE(256)]
        public ref T Get<T>(uint index) where T : unmanaged, IComponent {

            var entId = this.buffer.ptr->entities[index];
            return ref this.buffer.ptr->Get<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        /// <summary>
        /// Stores the supplied value in command buffer job batch.
        /// </summary>
        [INLINE(256)]
        public bool Set<T>(uint index, in T data) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Set<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId), in data);

        }

        /// <summary>
        /// Removes the specified entry from command buffer job batch.
        /// </summary>
        [INLINE(256)]
        public bool Remove<T>(uint index) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Remove<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        /// <summary>
        /// Tests whether the requested entry is present.
        /// </summary>
        [INLINE(256)]
        public bool Has<T>(uint index, bool checkEnabled = true) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Has<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId), checkEnabled);

        }

    }
    
    /// <summary>
    /// Executes command buffer work through the job scheduler.
    /// </summary>
    public readonly unsafe struct CommandBufferJob {

        private readonly uint entId;
        private readonly ushort entGen;
        /// <summary>
        /// Buffer used to exchange or store the associated data.
        /// </summary>
        public readonly safe_ptr<CommandBuffer> buffer;
        /// <summary>
        /// Number of entries currently tracked by this value.
        /// </summary>
        public uint Count => this.buffer.ptr->count;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent => new Ent(this.entId, this.entGen, this.buffer.ptr->worldId);
        
        /// <summary>
        /// Initializes <c>CommandBufferJob</c> from the supplied ent ID, gen, buffer.
        /// </summary>
        [INLINE(256)]
        public CommandBufferJob(in uint entId, ushort gen, safe_ptr<CommandBuffer> buffer) {
            this.entId = entId;
            this.entGen = gen;
            this.buffer = buffer;
        }

        /// <summary>
        /// Reads the requested value from command buffer job.
        /// </summary>
        [INLINE(256)]
        public ref readonly T Read<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Read<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Returns the requested entry from command buffer job.
        /// </summary>
        [INLINE(256)]
        public ref T Get<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Get<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Stores the supplied value in command buffer job.
        /// </summary>
        [INLINE(256)]
        public bool Set<T>(in T data) where T : unmanaged, IComponent {

            return this.buffer.ptr->Set<T>(this.entId, this.entGen, in data);

        }

        /// <summary>
        /// Removes the specified entry from command buffer job.
        /// </summary>
        [INLINE(256)]
        public bool Remove<T>() where T : unmanaged, IComponent {

            return this.buffer.ptr->Remove<T>(this.entId, this.entGen);

        }

        /// <summary>
        /// Tests whether the requested entry is present.
        /// </summary>
        [INLINE(256)]
        public bool Has<T>(bool checkEnabled = true) where T : unmanaged, IComponent {

            return this.buffer.ptr->Has<T>(this.entId, this.entGen, checkEnabled);

        }

    }

    /// <summary>
    /// Stores entities and execution metadata for deferred query processing.
    /// </summary>
    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct CommandBuffer {

        // [!] For some reason ScheduleParallelForDeferArraySize needs ptr first
        // so that's why we need LayoutKind.Sequential and first void* must be here
        // the second must be uint count
        /// <summary>
        /// Entity handles processed or stored by this operation.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public uint* entities;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count;
        
        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;

        /// <summary>
        /// Whether sync behavior or state is selected.
        /// </summary>
        public bbool sync;
        /// <summary>
        /// Whether builder sort behavior or state is selected.
        /// </summary>
        public bbool builderSort;

        /// <summary>
        /// Builder query data used by <c>CommandBuffer</c>.
        /// </summary>
        public safe_ptr<QueryData> builderQueryData;
        /// <summary>
        /// Builder compose used by <c>CommandBuffer</c>.
        /// </summary>
        public FlatQueries.QueryCompose builderCompose;

        /// <summary>
        /// Sets builder.
        /// </summary>
        [INLINE(256)]
        public void SetBuilder(ref QueryBuilder builder) {
            if (builder.scheduleMode == Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Parallel) {
                builder.builderDependsOn = JobHandle.CombineDependencies(builder.builderDependsOn, builder.SetEntities(builder.commandBuffer, builder.useSort, HandleStorage.lastApplyHandleBurst.Data));
            } else {
                builder.builderDependsOn = JobHandle.CombineDependencies(builder.builderDependsOn, HandleStorage.lastApplyHandleBurst.Data);
                this.builderCompose = builder.compose;
                this.builderQueryData = builder.queryData;
                this.builderSort = builder.useSort;
            }
        }
        
        /// <summary>
        /// Sets entities.
        /// </summary>
        [INLINE(256)]
        public void SetEntities(CommandBuffer* ptr) {
            var job = new QueryBuilder.SetEntitiesJob() {
                compose = this.builderCompose,
                buffer = new safe_ptr<CommandBuffer>(ptr),
                queryData = this.builderQueryData,
                state = this.state,
                allocator = Constants.ALLOCATOR_TEMP,
                useSort = this.builderSort,
            };
            job.Execute();
        }
        
        /// <summary>
        /// Provides the <c>BeginForEachRange</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void BeginForEachRange(uint fromIndex, uint toIndex) {
            
        }

        /// <summary>
        /// Provides the <c>EndForEachRange</c> callback; this implementation performs no work.
        /// </summary>
        [INLINE(256)]
        public void EndForEachRange() {
            
        }

        /// <summary>
        /// Releases the resources owned by this command buffer instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {

            //if (this.entities != null) _freeArray(this.entities, this.count, Constants.ALLOCATOR_TEMP_ST.ToAllocator);
            this = default;
            
        }

        /// <summary>
        /// Reads the requested value from command buffer.
        /// </summary>
        [INLINE(256)]
        public ref readonly T Read<T>(uint id, ushort gen) where T : unmanaged, IComponent {

            return ref Components.Read<T>(this.state, id, gen);

        }

        /// <summary>
        /// Returns the requested entry from command buffer.
        /// </summary>
        [INLINE(256)]
        public ref T Get<T>(uint id, ushort gen) where T : unmanaged, IComponent {
            
            if (this.sync == false && this.Has<T>(id, gen, checkEnabled: true) == false) {
                E.THREAD_CHECK(nameof(this.Get));
            }
            E.IS_IN_TICK(this.state);
            var ent = new Ent(id, gen, this.worldId);
            return ref Components.Get<T>(this.state, ent);

        }

        /// <summary>
        /// Stores the supplied value in command buffer.
        /// </summary>
        [INLINE(256)]
        public bool Set<T>(uint id, ushort gen, in T data) where T : unmanaged, IComponent {

            if (this.sync == false && this.Has<T>(id, gen, checkEnabled: true) == false) {
                E.THREAD_CHECK(nameof(this.Set));
                return false;
            }
            E.IS_IN_TICK(this.state);
            var ent = new Ent(id, gen, this.worldId);
            return Components.SetUnknownType(this.state, StaticTypes<T>.typeId, StaticTypes<T>.trackerIndex, in ent, in data);

        }

        /// <summary>
        /// Removes the specified entry from command buffer.
        /// </summary>
        [INLINE(256)]
        public bool Remove<T>(uint id, ushort gen) where T : unmanaged, IComponent {

            if (this.sync == false) {
                E.THREAD_CHECK(nameof(this.Remove));
                return false;
            } else {
                E.IS_IN_TICK(this.state);
                var ent = new Ent(id, gen, this.worldId);
                return Components.RemoveUnknownType(this.state, StaticTypes<T>.typeId, StaticTypes<T>.trackerIndex, in ent);
            }

        }

        /// <summary>
        /// Tests whether the requested entry is present.
        /// </summary>
        [INLINE(256)]
        public bool Has<T>(uint id, ushort gen, bool checkEnabled) where T : unmanaged, IComponent {

            return Components.Has<T>(this.state, id, gen, checkEnabled);

        }

    }

}