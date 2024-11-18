namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    public unsafe struct QueryData {

        internal TempBitArray archetypesBits;
        internal uint* archetypes;
        internal uint archetypesCount;
        internal uint steps;
        internal uint minElementsPerStep;

        public void Dispose() {
            if (this.archetypesBits.IsCreated == true) this.archetypesBits.Dispose();
            this = default;
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe ref struct QueryBuilder {
        
        internal CommandBuffer* commandBuffer;
        internal QueryData* queryData;
        internal uint parallelForBatch;
        private bool withBurst;
        private bool asJob;
        internal JobHandle builderDependsOn;
        internal bool isCreated;
        public ushort WorldId => this.commandBuffer->worldId;

        [BURST(CompileSynchronously = true)]
        private struct DisposeJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public QueryData* queryData;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* commandBuffer;

            public void Execute() {
                
                this.queryData->Dispose();
                _free(ref this.queryData);
                
                this.commandBuffer->Dispose();
                _free(ref this.commandBuffer);

            }

        }
        
        [INLINE(256)]
        public QueryBuilder WaitForAllJobs() {
            this.builderDependsOn.Complete();
            return this;
        }
        
        [INLINE(256)]
        public void Dispose() {
            E.IS_CREATED(this);
            this.builderDependsOn.Complete();
            this.queryData->Dispose();
            _free(ref this.queryData);
            this.commandBuffer->Dispose();
            _free(ref this.commandBuffer);
            this = default;
        }

        [INLINE(256)]
        public JobHandle Dispose(JobHandle handle) {
            E.IS_CREATED(this);
            var job = new DisposeJob() {
                queryData = this.queryData,
                commandBuffer = this.commandBuffer,
            };
            return job.Schedule(handle);
        }

        [INLINE(256)]
        public QueryBuilder Step(uint steps, uint minElementsPerStep) {
            E.IS_CREATED(this);
            this.queryData->steps = steps;
            this.queryData->minElementsPerStep = minElementsPerStep;
            return this;
        }
        
        [INLINE(256)]
        public QueryBuilder ParallelFor(uint batch) {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_AS_JOB(this.asJob);
            this.parallelForBatch = batch;
            return this;
        }

        [INLINE(256)]
        public QueryBuilder AsJob() {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_PARALLEL_FOR(this.parallelForBatch);
            this.asJob = true;
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithBurst() {
            E.IS_CREATED(this);
            this.withBurst = true;
            return this;
        }
        
        
        [INLINE(256)]
        public QueryBuilder WithAll<T0, T1>() where T0 : unmanaged, IComponentBase
                                              where T1 : unmanaged, IComponentBase {
            this.With<T0>();
            this.With<T1>();
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAll<T0, T1, T2>() where T0 : unmanaged, IComponentBase
                                                  where T1 : unmanaged, IComponentBase
                                                  where T2 : unmanaged, IComponentBase {
            this.With<T0>();
            this.With<T1>();
            this.With<T2>();
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAll<T0, T1, T2, T3>() where T0 : unmanaged, IComponentBase
                                                      where T1 : unmanaged, IComponentBase
                                                      where T2 : unmanaged, IComponentBase
                                                      where T3 : unmanaged, IComponentBase {
            this.With<T0>();
            this.With<T1>();
            this.With<T2>();
            this.With<T3>();
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1>() where T0 : unmanaged, IComponentBase
                                              where T1 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, TNull, TNull>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2>() where T0 : unmanaged, IComponentBase
                                                  where T1 : unmanaged, IComponentBase
                                                  where T2 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, T2, TNull>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2, T3>() where T0 : unmanaged, IComponentBase 
                                                      where T1 : unmanaged, IComponentBase 
                                                      where T2 : unmanaged, IComponentBase 
                                                      where T3 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, T2, T3>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder With<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.With<T>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder Without<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.Without<T>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAspect<T>() where T : unmanaged, IAspect {
            E.IS_CREATED(this);
            this.builderDependsOn = ArchetypeQueries.With(ref this, AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId));
            return this;
        }
        
        private struct Job : IJobCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJob commandBuffer) => this.functionPointer.Invoke(in commandBuffer);
        }

        [BURST(CompileSynchronously = true)]
        private struct JobBurst : IJobCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJob commandBuffer) => this.functionPointer.Invoke(in commandBuffer);
        }

        private struct JobParallelFor : IJobParallelForCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJobParallel commandBuffer) {
                var buffer = new CommandBufferJob(commandBuffer.entId, commandBuffer.entGen, commandBuffer.buffer);
                this.functionPointer.Invoke(in buffer);    
            }
        }

        [BURST(CompileSynchronously = true)]
        private struct JobParallelForBurst : IJobParallelForCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJobParallel commandBuffer) {
                var buffer = new CommandBufferJob(commandBuffer.entId, commandBuffer.entGen, commandBuffer.buffer);
                this.functionPointer.Invoke(in buffer);
            }

        }

        private readonly struct CallbackBurst {

            private readonly System.Runtime.InteropServices.GCHandle gcHandle;
            private readonly Unity.Burst.FunctionPointer<QueryDelegate> functionPointer;

            public CallbackBurst(QueryDelegate del, bool withBurst) {

                this = default;
                if (withBurst == true && del.Method.IsStatic == true) {

                    this.functionPointer = Unity.Burst.BurstCompiler.CompileFunctionPointer(del);
                    
                } else {

                    if (withBurst == true) {
                        // [!] We need to use static method to be able to use BurstCompiler
                        // So, we can throw an exception here or use IL (in roadmap for now) or just use delegate pointer to run this delegate out of burst
                        Logger.Core.Warning("[ME.BECS] ForEach method run with Burst flag on, but delegate must be static. Delegate will be run without burst.");
                    }

                    var ptr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del);
                    this.gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(del, System.Runtime.InteropServices.GCHandleType.Pinned);
                    this.functionPointer = new Unity.Burst.FunctionPointer<QueryDelegate>(ptr);
                    
                }

            }
            
            public void Invoke(in CommandBufferJob buffer) {

                this.functionPointer.Invoke(in buffer);

            }

            public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {

                if (this.gcHandle.IsAllocated == false) return inputDeps;
                
                var job = new DisposeHandleJob() {
                    gcHandle = this.gcHandle,
                };
                return job.Schedule(inputDeps);

            }

        }

        private struct Callback {

            public static CallbackBurst Create(QueryDelegate del, bool withBurst) {

                return new CallbackBurst(del, withBurst);

            }

        }

        public delegate void QueryDelegate(in CommandBufferJob commandBuffer);

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle Schedule<T>(T job) where T : struct, IJobCommandBuffer {

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(in this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle Schedule<T>() where T : struct, IJobCommandBuffer {

            T job = default;
            return this.Schedule(job);

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle ScheduleParallelFor<T>(T job) where T : struct, IJobParallelForCommandBuffer {

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(in this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle ScheduleParallelFor<T>() where T : struct, IJobParallelForCommandBuffer {
            
            T job = default;
            return this.ScheduleParallelFor(job);

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle ScheduleParallelForBatch<T>() where T : struct, IJobParallelForCommandBufferBatch {
            
            T job = default;
            return this.ScheduleParallelForBatch(job);

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle ScheduleParallelForBatch<T>(T job) where T : struct, IJobParallelForCommandBufferBatch {

            // Need to complete previous job and run SetEntities in sync mode
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
            this.builderDependsOn = this.Dispose(this.builderDependsOn);
            return this.builderDependsOn;

        }
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public Unity.Collections.NativeArray<Ent> ToArray(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMP) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn.Complete();
            var cnt = (int)this.commandBuffer->count;
            var result = new Unity.Collections.NativeArray<Ent>(cnt, allocator);
            for (int i = 0; i < cnt; ++i) {
                var entId = this.commandBuffer->entities[i];
                result[i] = new Ent(entId, in Worlds.GetWorld(this.commandBuffer->worldId));
            }
            this.Dispose();
            return result;

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public Enumerator GetEnumerator() {

            Enumerator e = default;
            e.queryBuilder = new QueryBuilderDispose(this);
            e.commandBuffer = this.commandBuffer;
            e.index = 0u;
            e.worldId = this.WorldId;
            
            if (this.parallelForBatch > 0u) {
                // wtf?
                // TODO: Exception
            } else {
                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
                this.commandBuffer->sync = true;
                if (this.withBurst == true) {
                    // wtf?
                    // TODO: Exception
                } else if (this.asJob == true) {
                    // wtf?
                    // TODO: Exception
                } else {
                    
                    // [!] We need to sync at this point
                    // May be we can defer enumerator?
                    this.builderDependsOn.Complete();
                    
                }
            }
            
            return e;

        }
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        [INLINE(256)]
        public JobHandle ForEach(QueryDelegate forEach) {

            JobHandle handle = default;
            
            if (this.parallelForBatch > 0u) {

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
                this.commandBuffer->sync = false;
                JobHandle jobHandle;
                if (this.withBurst == true) {

                    var job = new JobParallelForBurst() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    jobHandle = job.Schedule(this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                } else {

                    var job = new JobParallelFor() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    jobHandle = job.Schedule(this.commandBuffer, this.parallelForBatch, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                }

            } else {

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
                this.commandBuffer->sync = true;
                if (this.withBurst == true) {

                    var job = new JobBurst() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    var jobHandle = job.Schedule(this.commandBuffer, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                } else if (this.asJob == true) {
                    
                    var job = new Job() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    var jobHandle = job.Schedule(this.commandBuffer, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                } else {
                    
                    this.WaitForAllJobs();
                    
                    for (uint i = 0u; i < this.commandBuffer->count; ++i) {

                        var entId = this.commandBuffer->entities[i];
                        var entGen = Ents.GetGeneration(this.commandBuffer->state, entId);
                        var buffer = new CommandBufferJob(entId, entGen, this.commandBuffer);
                        forEach.Invoke(in buffer);

                    }

                }

            }

            handle = this.Dispose(handle);

            this.builderDependsOn = handle;
            return handle;
        }

        [BURST(CompileSynchronously = true)]
        private struct FromQueryDataJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public Queries.QueryDataStatic* queryDataStatic;
            [NativeDisableUnsafePtrRestriction]
            public QueryData* queryData;
            
            public void Execute() {
                
                this.queryData->archetypes = (uint*)this.queryDataStatic->archetypes.GetUnsafePtr(in this.state->allocator);
                this.queryData->archetypesCount = this.queryDataStatic->archetypes.Count;
                
            }

        }
        
        [INLINE(256)]
        internal QueryBuilderDisposable FromQueryData(State* state, ushort worldId, Queries.QueryDataStatic* queryDataStatic) {

            //this.queryData = queryDataStatic->GetQueryData(state);
            //this.commandBuffer = queryDataStatic->GetCommandBuffer(state, worldId);
            
            this.builderDependsOn = new FromQueryDataJob() {
                queryDataStatic = queryDataStatic,
                state = state,
                queryData = this.queryData,
            }.Schedule(this.builderDependsOn);
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            //this.builderDependsOn.Complete();
            return new QueryBuilderDisposable(this);

        }

        [BURST(CompileSynchronously = true)]
        private struct SetEntitiesJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public CommandBuffer* buffer;
            [NativeDisableUnsafePtrRestriction]
            public QueryData* queryData;

            public void Execute() {

                {
                    var archCount = this.queryData->archetypesCount;
                    // queryData->archetypes are set with static queries only
                    // so we need to check if it is null - we have a dynamic query
                    // build archetypes
                    if (this.queryData->archetypes == null) {
                        var tempListBits = this.queryData->archetypesBits.GetTrueBitsTemp();
                        this.queryData->archetypes = tempListBits.Ptr;
                        archCount = (uint)tempListBits.Length;
                    }

                    if (archCount == 0u) {
                        this.buffer->entities = null;
                        this.buffer->count = 0u;
                        return;
                    }

                    var archs = this.queryData->archetypes;
                    uint* arrPtr = null;
                    var elementsCount = 0u;
                    if (this.queryData->steps > 0u) {

                        var currentStep = this.state->tick;
                        var temp = new UnsafeList<uint>((int)archCount, Constants.ALLOCATOR_TEMP);

                        for (uint i = 0u; i < archCount; ++i) {
                            var archIdx = archs[i];
                            ref var arch = ref this.state->archetypes.list[in this.state->allocator, archIdx];
                            temp.AddRange(arch.entitiesList.GetUnsafePtr(in this.state->allocator), (int)arch.entitiesList.Count);
                        }

                        var steps = this.queryData->steps;
                        var count = (uint)temp.Length;
                        if (count > 0u) {
                            var elementsPerStep = count / steps;
                            if (elementsPerStep < this.queryData->minElementsPerStep) elementsPerStep = this.queryData->minElementsPerStep;
                            steps = (uint)System.Math.Ceiling((count / (double)elementsPerStep));
                            var fromIdx = (uint)(currentStep % steps) * elementsPerStep;
                            var toIdx = fromIdx + elementsPerStep;
                            if (toIdx > count) toIdx = count;
                            // Add range fromIdx..toIdx
                            var size = toIdx - fromIdx;
                            arrPtr = _makeArray<uint>(size);
                            _memcpy(temp.Ptr + fromIdx, arrPtr, TSize<uint>.sizeInt * (int)size);
                            elementsCount += size;
                        }

                    } else {

                        for (uint i = 0u; i < archCount; ++i) {
                            var archIdx = archs[i];
                            ref var arch = ref this.state->archetypes.list[in this.state->allocator, archIdx];
                            elementsCount += arch.entitiesList.Count;
                        }

                        if (elementsCount > 0u) {
                            arrPtr = _makeArray<uint>(elementsCount);
                            var k = 0u;
                            for (uint i = 0u; i < archCount; ++i) {
                                var archIdx = archs[i];
                                ref var arch = ref this.state->archetypes.list[in this.state->allocator, archIdx];
                                _memcpy(arch.entitiesList.GetUnsafePtr(in this.state->allocator), arrPtr + k, TSize<uint>.size * arch.entitiesList.Count);
                                k += arch.entitiesList.Count;
                            }
                        }

                    }

                    if (elementsCount > 0u) Unity.Collections.NativeSortExtension.Sort(arrPtr, (int)elementsCount);
                    this.buffer->entities = arrPtr;
                    this.buffer->count = elementsCount;
                }
                
            }

        }
        
        [INLINE(256)]
        internal JobHandle SetEntities(CommandBuffer* buffer, JobHandle dependsOn) {

            var job = new SetEntitiesJob() {
                buffer = buffer,
                queryData = this.queryData,
                state = buffer->state,
            };
            return job.Schedule(dependsOn);
            
        }

        public struct Enumerator : System.Collections.Generic.IEnumerator<Ent> {

            public QueryBuilderDispose queryBuilder;
            public CommandBuffer* commandBuffer;
            public uint index;
            public ushort worldId;
            
            public bool MoveNext() => this.index++ < this.commandBuffer->count;

            public Ent Current => new Ent(this.commandBuffer->entities[this.index - 1u], this.commandBuffer->state, this.worldId);

            object System.Collections.IEnumerator.Current => this.Current;

            public void Reset() { }

            [INLINE(256)]
            public void Dispose() {
                this.queryBuilder.Dispose();
                this = default;
            }

        }

    }

    public unsafe struct QueryBuilderDispose {

        private CommandBuffer* commandBuffer;
        private QueryData* queryData;
        private JobHandle builderDependsOn;
        internal readonly bool isCreated;

        [INLINE(256)]
        public QueryBuilderDispose(in QueryBuilder queryBuilder) {
            this.commandBuffer = queryBuilder.commandBuffer;
            this.queryData = queryBuilder.queryData;
            this.builderDependsOn = queryBuilder.builderDependsOn;
            this.isCreated = queryBuilder.isCreated;
        }
        
        [INLINE(256)]
        public void Dispose() {
            E.IS_CREATED(this);
            this.builderDependsOn.Complete();
            this.queryData->Dispose();
            _free(ref this.queryData);
            this.commandBuffer->Dispose();
            _free(ref this.commandBuffer);
            this = default;
        }

    }

}