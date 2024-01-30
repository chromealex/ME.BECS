namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;

    public unsafe struct QueryBuilderDisposable {

        [BURST(CompileSynchronously = true)]
        public struct DisposeJob : IJob {

            public QueryBuilderDisposable builder;
            public void Execute() {
                this.builder.Dispose();
            }

        }

        [NativeDisableUnsafePtrRestriction]
        internal CommandBuffer* commandBuffer;
        [NativeDisableUnsafePtrRestriction]
        internal QueryData* queryData;
        internal JobHandle builderDependsOn;
        internal uint parallelForBatch;
        public ushort worldId => this.commandBuffer->worldId;

        [INLINE(256)]
        public QueryBuilderDisposable(QueryBuilder builder) {
            this.commandBuffer = builder.commandBuffer;
            this.queryData = builder.queryData;
            this.builderDependsOn = builder.builderDependsOn;
            this.parallelForBatch = builder.parallelForBatch;
        }
        
        [INLINE(256)]
        public void WaitForAllJobs() {
            this.builderDependsOn.Complete();
        }

        public JobHandle Schedule<T>(in T job) where T : struct, IJobCommandBuffer {
            this.builderDependsOn = job.Schedule(in this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;
        }

        public JobHandle ScheduleParallelFor<T>(in T job) where T : struct, IJobParallelForCommandBuffer {
            this.builderDependsOn = job.Schedule(in this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;
        }

        public JobHandle ScheduleParallelForBatch<T>(in T job) where T : struct, IJobParallelForCommandBufferBatch {
            this.builderDependsOn = job.Schedule(in this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;
        }

        [INLINE(256)]
        public JobHandle Dispose(JobHandle dependsOn) {
            return new DisposeJob() {
                builder = this,
            }.Schedule(dependsOn);
        }

        [INLINE(256)]
        public void Dispose() {
            this.queryData->Dispose();
            _free(ref this.queryData);
            this.commandBuffer->Dispose();
            _free(ref this.commandBuffer);
            this = default;
        }

    }

    public unsafe struct QueryBuilderStatic {

        internal readonly State* state;
        internal readonly Queries.QueryDataStatic* queryDataStatic;
        internal readonly bool isCreated;
        internal JobHandle dependsOn;
        
        [INLINE(256)]
        public QueryBuilderStatic(in World world, JobHandle dependsOn) {

            this = default;
            this.dependsOn = dependsOn;
            this.state = world.state;
            this.queryDataStatic = _make(new Queries.QueryDataStatic(world.state));
            this.isCreated = true;

        }
        
        [INLINE(256)]
        public QueryBuilderStatic WithAll<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            E.IS_CREATED(this);
            this.With<T0>();
            this.With<T1>();
            return this;
        }

        [INLINE(256)]
        public QueryBuilderStatic WithAny<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            E.IS_CREATED(this);
            this.queryDataStatic->withAny.Add(ref this.state->allocator, new UIntPair(StaticTypes<T0>.typeId, StaticTypes<T1>.typeId));
            return this;
        }

        [INLINE(256)]
        public QueryBuilderStatic With<T>() where T : unmanaged, IComponent {
            E.IS_CREATED(this);
            this.queryDataStatic->with.Add(ref this.state->allocator, StaticTypes<T>.typeId);
            return this;
        }

        [INLINE(256)]
        public QueryBuilderStatic Without<T>() where T : unmanaged, IComponent {
            E.IS_CREATED(this);
            this.queryDataStatic->without.Add(ref this.state->allocator, StaticTypes<T>.typeId);
            return this;
        }

        /*
        [INLINE(256)]
        public QueryBuilderStatic WithAspect<T>() where T : unmanaged, IAspect {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            T value = default;
            var aspectBuilder = new AspectQueryBuilder(in this);
            value.Query(ref aspectBuilder);
            return this;
        }
        */

        [INLINE(256)]
        public QueryBuilderStatic Step(uint steps, uint minElementsPerStep) {
            E.IS_CREATED(this);
            this.queryDataStatic->steps = steps;
            this.queryDataStatic->minElementsPerStep = minElementsPerStep;
            return this;
        }

        [INLINE(256)]
        public void Dispose() {

            if (this.isCreated == true) {
                this.queryDataStatic->Dispose(this.state);
                _free(this.queryDataStatic);
                this = default;
            }
            
        }

        [INLINE(256)]
        public Query Build() {
            
            // Register query
            return this.state->queries.Add(this.state, in this);

        }

    }

    public readonly unsafe struct QueryForEach {

        public struct Enumerator : System.IDisposable {

            internal QueryBuilderDisposable queryBuilder;
            private uint index;

            public bool MoveNext() {
                return this.index++ < this.queryBuilder.commandBuffer->count;
            }

            public void Reset() {
                
            }

            public Ent Current => new Ent(this.queryBuilder.commandBuffer->entities[this.index - 1u], this.queryBuilder.commandBuffer->state, this.queryBuilder.worldId);

            public void Dispose() {
            }

        }

        private readonly Queries.QueryDataStatic* queryData;
        private readonly State* state;
        private readonly JobHandle dependsOn;
        private readonly ushort worldId;

        public QueryForEach(in World world, JobHandle dependsOn, uint queryId) {
            this.queryData = world.state->queries.GetPtr(world.state, queryId);
            this.state = world.state;
            this.worldId = world.id;
            this.dependsOn = dependsOn;
        }

        public Enumerator GetEnumerator() {
            var query = API.MakeStaticQuery(QueryContext.Create(this.state, this.worldId), this.dependsOn).FromQueryData(this.state, this.worldId, this.queryData);
            query.WaitForAllJobs();
            Enumerator e = default;
            e.queryBuilder = query;
            return e;
        }

    }
    
    public unsafe partial struct Query {

        public uint id;

        public bool isCreated => this.id > 0u;

        public QueryForEach ForEach(in World world, JobHandle dependsOn) {
            E.IS_CREATED(this);
            return new QueryForEach(in world, dependsOn, this.id);
        }

        public QueryForEach ForEach(in SystemContext systemContext) {
            E.IS_CREATED(this);
            return new QueryForEach(in systemContext.world, systemContext.dependsOn, this.id);
        }

        public JobHandle Schedule<T>(in World world, JobHandle dependsOn) where T : struct, IJobCommandBuffer {
            T job = default;
            return this.Schedule(in job, in world, dependsOn);
        }

        public JobHandle ScheduleParallelFor<T>(in World world, uint parallelForBatch, JobHandle dependsOn) where T : struct, IJobParallelForCommandBuffer {
            T job = default;
            return this.ScheduleParallelFor(in job, parallelForBatch, in world, dependsOn);
        }

        public JobHandle ScheduleParallelForBatch<T>(in World world, uint parallelForBatch, JobHandle dependsOn) where T : struct, IJobParallelForCommandBufferBatch {
            T job = default;
            return this.ScheduleParallelForBatch(in job, parallelForBatch, in world, dependsOn);
        }

        public JobHandle Schedule<T>(in SystemContext systemContext) where T : struct, IJobCommandBuffer {
            T job = default;
            return this.Schedule(in job, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle ScheduleParallelFor<T>(uint parallelForBatch, in SystemContext systemContext) where T : struct, IJobParallelForCommandBuffer {
            T job = default;
            return this.ScheduleParallelFor(in job, parallelForBatch, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle ScheduleParallelForBatch<T>(uint parallelForBatch, in SystemContext systemContext) where T : struct, IJobParallelForCommandBufferBatch {
            T job = default;
            return this.ScheduleParallelForBatch(in job, parallelForBatch, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle Schedule<T>(in T job, in SystemContext systemContext) where T : struct, IJobCommandBuffer {
            return this.Schedule(in job, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle ScheduleParallelFor<T>(in T job, uint parallelForBatch, in SystemContext systemContext) where T : struct, IJobParallelForCommandBuffer {
            return this.ScheduleParallelFor(in job, parallelForBatch, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle ScheduleParallelForBatch<T>(in T job, uint parallelForBatch, in SystemContext systemContext) where T : struct, IJobParallelForCommandBufferBatch {
            return this.ScheduleParallelForBatch(in job, parallelForBatch, in systemContext.world, systemContext.dependsOn);
        }

        public JobHandle Schedule<T>(in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobCommandBuffer {
            T job = default;
            return this.Schedule(in job, in systemContext.world, dependsOn);
        }

        public JobHandle ScheduleParallelFor<T>(uint parallelForBatch, in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobParallelForCommandBuffer {
            T job = default;
            return this.ScheduleParallelFor(in job, parallelForBatch, in systemContext.world, dependsOn);
        }

        public JobHandle ScheduleParallelForBatch<T>(uint parallelForBatch, in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobParallelForCommandBufferBatch {
            T job = default;
            return this.ScheduleParallelForBatch(in job, parallelForBatch, in systemContext.world, dependsOn);
        }

        public JobHandle Schedule<T>(in T job, in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobCommandBuffer {
            return this.Schedule(in job, in systemContext.world, dependsOn);
        }

        public JobHandle ScheduleParallelFor<T>(in T job, uint parallelForBatch, in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobParallelForCommandBuffer {
            return this.ScheduleParallelFor(in job, parallelForBatch, in systemContext.world, dependsOn);
        }

        public JobHandle ScheduleParallelForBatch<T>(in T job, uint parallelForBatch, in SystemContext systemContext, JobHandle dependsOn) where T : struct, IJobParallelForCommandBufferBatch {
            return this.ScheduleParallelForBatch(in job, parallelForBatch, in systemContext.world, dependsOn);
        }

        public JobHandle Schedule<T>(in T job, in World world, JobHandle dependsOn) where T : struct, IJobCommandBuffer {
            E.IS_CREATED(this);
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).FromQueryData(state, world.id, state->queries.GetPtr(state, this.id));
            return query.Schedule(in job);
        }

        public JobHandle ScheduleParallelFor<T>(in T job, uint parallelForBatch, in World world, JobHandle dependsOn) where T : struct, IJobParallelForCommandBuffer {
            E.IS_CREATED(this);
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).ParallelFor(parallelForBatch).FromQueryData(state, world.id, state->queries.GetPtr(state, this.id));
            return query.ScheduleParallelFor(in job);
        }

        public JobHandle ScheduleParallelForBatch<T>(in T job, uint parallelForBatch, in World world, JobHandle dependsOn) where T : struct, IJobParallelForCommandBufferBatch {
            E.IS_CREATED(this);
            var state = world.state;
            var query = API.MakeStaticQuery(QueryContext.Create(state, world.id), dependsOn).ParallelFor(parallelForBatch).FromQueryData(state, world.id, state->queries.GetPtr(state, this.id));
            return query.ScheduleParallelForBatch(in job);
        }

    }

    public unsafe struct Queries {

        internal struct QueryDataStatic {
            
            internal uint steps;
            internal uint minElementsPerStep;
            internal UIntHashSet with;
            internal UIntHashSet without;
            internal UIntPairHashSet withAny;
            internal UIntListHash archetypes;
            //internal CachedPtr<QueryData> queryData;
            //internal CachedPtr<CommandBuffer> commandBuffer;
            
            [INLINE(256)]
            public QueryDataStatic(State* state) {
                
                this = default;
                this.with = new UIntHashSet(ref state->allocator, 1u);
                this.without = new UIntHashSet(ref state->allocator, 1u);
                this.withAny = new UIntPairHashSet(ref state->allocator, 1u);
                //this.GetQueryData(state);
                //this.GetCommandBuffer(state, 0);
                
            }

            [INLINE(256)]
            public QueryDataStatic(in QueryBuilderStatic builder) {

                this = default;
                this.steps = builder.queryDataStatic->steps;
                this.minElementsPerStep = builder.queryDataStatic->minElementsPerStep;
                this.with = new UIntHashSet(ref builder.state->allocator, builder.queryDataStatic->with);
                this.without = new UIntHashSet(ref builder.state->allocator, builder.queryDataStatic->without);
                this.withAny = new UIntPairHashSet(ref builder.state->allocator, builder.queryDataStatic->withAny);
                //this.GetQueryData(builder.state);
                //this.GetCommandBuffer(builder.state, 0);

            }

            /*
            [INLINE(256)]
            public QueryData* GetQueryData(State* state) {

                var ptr = this.queryData.ReadPtr(in state->allocator);
                if (ptr == null) {
                    ptr = _make(new QueryData());
                    this.queryData = new CachedPtr<QueryData>(in state->allocator, ptr);
                }
                return ptr;

            }

            [INLINE(256)]
            public CommandBuffer* GetCommandBuffer(State* state, ushort worldId) {
                
                var ptr = this.commandBuffer.ReadPtr(in state->allocator);
                if (ptr == null) {
                    ptr = _make(new CommandBuffer() {
                        state = state,
                        worldId = worldId,
                    });
                    this.commandBuffer = new CachedPtr<CommandBuffer>(in state->allocator, ptr);
                }

                ptr->worldId = worldId;
                return ptr;
                
            }
            */
            
            [INLINE(256)]
            public bool Equals(State* state, in QueryBuilderStatic builder) {

                if (this.steps != builder.queryDataStatic->steps) return false;
                if (this.minElementsPerStep != builder.queryDataStatic->minElementsPerStep) return false;
                
                if (builder.queryDataStatic->with.Equals(in state->allocator, this.with) == false) {
                    return false;
                }
                if (builder.queryDataStatic->without.Equals(in state->allocator, this.without) == false) {
                    return false;
                }
                if (builder.queryDataStatic->withAny.Equals(in state->allocator, this.withAny) == false) {
                    return false;
                }

                return true;

            }

            [INLINE(256)]
            public void Dispose(State* state) {
                
                if (this.archetypes.isCreated == true) this.archetypes.Dispose(ref state->allocator);
                this.with.Dispose(ref state->allocator);
                this.without.Dispose(ref state->allocator);
                this.withAny.Dispose(ref state->allocator);
                //if (this.queryData.IsValid(in state->allocator) == true) _free(this.queryData.ReadPtr(in state->allocator));
                //if (this.commandBuffer.IsValid(in state->allocator) == true) _free(this.commandBuffer.ReadPtr(in state->allocator));
                this = default;

            }

            [INLINE(256)]
            public void Validate(State* state) {

                var queryData = new QueryData() {
                    archetypesBits = new TempBitArray(in state->allocator, state->archetypes.allArchetypesForQuery, Constants.ALLOCATOR_TEMP),
                    minElementsPerStep = this.minElementsPerStep,
                    steps = this.steps,
                };
                var queryRef = _address(ref queryData);
                if (this.with.Count > 0u) {
                    var e = this.with.GetEnumerator(state);
                    while (e.MoveNext() == true) {
                        ArchetypeQueries.With(state, queryRef, e.Current);
                    }
                }
                if (this.without.Count > 0u) {
                    var e = this.without.GetEnumerator(state);
                    while (e.MoveNext() == true) {
                        ArchetypeQueries.Without(state, queryRef, e.Current);
                    }
                }
                if (this.withAny.Count > 0u) {
                    var e = this.withAny.GetEnumerator(state);
                    while (e.MoveNext() == true) {
                        var cur = e.Current;
                        ArchetypeQueries.WithAny(state, queryRef, cur.typeId1, cur.typeId2);
                    }
                }

                if (this.archetypes.isCreated == true) this.archetypes.Dispose(ref state->allocator);
                this.archetypes = queryRef->archetypesBits.GetTrueBitsPersistent(ref state->allocator);
                queryRef->Dispose();
                
            }

        }
        
        internal MemArray<QueryDataStatic> queryData;
        internal uint nextId;

        [INLINE(256)]
        public static Queries Create(State* state, uint capacity) {
            return new Queries() {
                queryData = new MemArray<QueryDataStatic>(ref state->allocator, capacity),
            };
        }

        [INLINE(256)]
        internal QueryDataStatic* GetPtr(State* state, uint id) {

            var ptr = ((QueryDataStatic*)this.queryData.GetUnsafePtr(in state->allocator)) + (id - 1u);
            return ptr;

        }

        [INLINE(256)]
        public Query Add(State* state, in QueryBuilderStatic builder) {

            for (uint i = 0u; i < this.nextId; ++i) {
                var queryData = this.queryData[in state->allocator, i];
                if (queryData.Equals(state, in builder) == true) {
                    return new Query() { id = i + 1u };
                }
            }

            E.IS_IN_TICK(state);
            
            {
                this.queryData.Resize(ref state->allocator, this.nextId + 1u);
            }

            var data = new QueryDataStatic(in builder);
            data.Validate(state);
            this.queryData[in state->allocator, this.nextId] = data;
            return new Query() { id = ++this.nextId };
            
        }

    }

}