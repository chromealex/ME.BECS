#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;
    using ME.BECS.NativeCollections;
    
    public unsafe struct QueryData {

        internal TempBitArray archetypesBits;
        internal safe_ptr<uint> archetypes;
        internal uint archetypesCount;
        internal uint steps;
        internal uint minElementsPerStep;

        public void Dispose() {
            if (this.archetypesBits.IsCreated == true) this.archetypesBits.Dispose();
            this = default;
        }

    }

    public unsafe struct OnDemandArray : IIsCreated {

        internal struct Data {

            public Unity.Collections.NativeList<Ent> results;

        }
            
        internal JobHandle dependsOn;
        internal safe_ptr<Data> data;
        internal Allocator allocator;

        public int Length {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                this.dependsOn.Complete();
                return this.data.ptr->results.Length;
            }
        }

        public bool IsCreated => this.allocator != Allocator.Invalid;

        [INLINE(256)]
        public NativeArray<Ent> GetResults() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            return this.data.ptr->results.AsArray();
        }

        public Ent this[int index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                this.dependsOn.Complete();
                return this.data.ptr->results[index];
            }
        }

        public NativeArray<Ent>.Enumerator GetEnumerator() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            return this.data.ptr->results.GetEnumerator();
        }

        [INLINE(256)]
        public void Dispose() {
            if (this.allocator == Allocator.Invalid) return;
            this.dependsOn.Complete();
            this.data.ptr->results.Dispose();
            _free(this.data, this.allocator);
            this = default;
        }

        [INLINE(256)]
        public void Clear() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            this.data.ptr->results.Clear();
        }

    }

    [BURST]
    public unsafe ref struct QueryBuilder {
        
        internal safe_ptr<CommandBuffer> commandBuffer;
        internal safe_ptr<QueryData> queryData;
        internal ArchetypeQueries.QueryCompose compose;
        internal uint parallelForBatch;
        internal JobHandle builderDependsOn;
        internal Allocator allocator;
        private bool withBurst;
        private bool asJob;
        internal ScheduleMode scheduleMode;
        internal bool isUnsafe;
        internal bool isReadonly;
        internal bool useSort;
        internal bool isCreated;
        
        public ushort WorldId => this.commandBuffer.ptr->worldId;

        [BURST]
        private struct DisposeJob : IJob {

            public safe_ptr<QueryData> queryData;
            public safe_ptr<CommandBuffer> commandBuffer;
            public ArchetypeQueries.QueryCompose compose;
            public Allocator allocator;

            public void Execute() {
                
                this.compose.Dispose();
                
                this.queryData.ptr->Dispose();
                _free(this.queryData, this.allocator);
                
                this.commandBuffer.ptr->Dispose();
                _free(this.commandBuffer, this.allocator);

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
            this.compose.Dispose();
            this.queryData.ptr->Dispose();
            _free(this.queryData, this.allocator);
            this.commandBuffer.ptr->Dispose();
            _free(this.commandBuffer, this.allocator);
            this = default;
        }

        [INLINE(256)]
        public JobHandle Dispose(JobHandle handle) {
            E.IS_CREATED(this);
            var job = new DisposeJob() {
                compose = this.compose,
                queryData = this.queryData,
                commandBuffer = this.commandBuffer,
                allocator = this.allocator,
            };
            return job.Schedule(handle);
        }

        [INLINE(256)]
        public QueryBuilder Step(uint steps, uint minElementsPerStep) {
            E.IS_CREATED(this);
            this.queryData.ptr->steps = steps;
            this.queryData.ptr->minElementsPerStep = minElementsPerStep;
            return this;
        }

        /// <summary>
        /// Disable components safety restriction
        /// </summary>
        /// <returns></returns>
        [INLINE(256)]
        public QueryBuilder AsUnsafe() {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_IS_UNSAFE(this.isUnsafe);
            this.isUnsafe = true;
            return this;
        }

        /// <summary>
        /// Run readonly filter schedule
        /// </summary>
        /// <returns></returns>
        [INLINE(256)]
        public QueryBuilder AsReadonly() {
            E.IS_CREATED(this);
            this.isReadonly = true;
            return this;
        }

        [INLINE(256)]
        public QueryBuilder Sort() {
            this.useSort = true;
            return this;
        }

        [INLINE(256)]
        public QueryBuilder AsParallel(uint batch = 0u) {
            E.IS_CREATED(this);
            this.parallelForBatch = batch;
            this.scheduleMode = ScheduleMode.Parallel;
            return this;
        }

        [INLINE(256)][System.Obsolete("ParallelFor is obsolete, use AsParallel(batch) instead.")]
        public QueryBuilder ParallelFor(uint batch) {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_AS_JOB(this.asJob);
            this.parallelForBatch = batch;
            this.scheduleMode = ScheduleMode.Parallel;
            return this;
        }

        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public QueryBuilder AsJob() {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_PARALLEL_FOR(this.parallelForBatch);
            this.asJob = true;
            return this;
        }

        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
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
            this.compose.WithAny<T0, T1>();
            //this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, TNull, TNull>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2>() where T0 : unmanaged, IComponentBase
                                                  where T1 : unmanaged, IComponentBase
                                                  where T2 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.WithAny<T0, T1>();
            this.compose.WithAny<T2, TNull>();
            //this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, T2, TNull>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2, T3>() where T0 : unmanaged, IComponentBase 
                                                      where T1 : unmanaged, IComponentBase 
                                                      where T2 : unmanaged, IComponentBase 
                                                      where T3 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.WithAny<T0, T1>();
            this.compose.WithAny<T2, T3>();
            //this.builderDependsOn = ArchetypeQueries.WithAny<T0, T1, T2, T3>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder With<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.With<T>();
            //this.builderDependsOn = ArchetypeQueries.With<T>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder Without<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.Without<T>();
            //this.builderDependsOn = ArchetypeQueries.Without<T>(ref this);
            return this;
        }

        [INLINE(256)]
        public QueryBuilder WithAspect<T>() where T : unmanaged, IAspect {
            E.IS_CREATED(this);
            this.compose.WithAspect<T>();
            //this.builderDependsOn = ArchetypeQueries.With(ref this, AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId));
            return this;
        }
        
        private struct Job : IJobCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJob commandBuffer) => this.functionPointer.Invoke(in commandBuffer);
        }

        [BURST]
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

        [BURST]
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

                    this.gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(del);
                    var ptr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(del);
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

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(in this.commandBuffer.ptr, this.builderDependsOn);
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
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public JobHandle ScheduleParallelFor<T>(T job) where T : struct, IJobParallelForCommandBuffer {

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(in this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
            return this.builderDependsOn;

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public JobHandle ScheduleParallelFor<T>() where T : struct, IJobParallelForCommandBuffer {
            
            T job = default;
            return this.ScheduleParallelFor(job);

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public JobHandle ScheduleParallelForBatch<T>() where T : struct, IJobParallelForCommandBufferBatch {
            
            T job = default;
            return this.ScheduleParallelForBatch(job);

        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public JobHandle ScheduleParallelForBatch<T>(T job) where T : struct, IJobParallelForCommandBufferBatch {

            // Need to complete previous job and run SetEntities in sync mode
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
            return this.builderDependsOn;

        }
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public Unity.Collections.NativeArray<Ent> ToArray(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMP) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            this.builderDependsOn.Complete();
            var cnt = (int)this.commandBuffer.ptr->count;
            var result = new Unity.Collections.NativeArray<Ent>(cnt, allocator);
            for (int i = 0; i < cnt; ++i) {
                var entId = this.commandBuffer.ptr->entities[i];
                result[i] = new Ent(entId, in Worlds.GetWorld(this.commandBuffer.ptr->worldId));
            }
            this.Dispose();
            return result;

        }

        [BURST]
        public struct OnDemandJob : IJob {

            public OnDemandArray handle;
            public safe_ptr<CommandBuffer> commandBuffer;
            
            public void Execute() {
                
                var cnt = (int)this.commandBuffer.ptr->count;
                for (int i = 0; i < cnt; ++i) {
                    var entId = this.commandBuffer.ptr->entities[i];
                    this.handle.data.ptr->results.Add(new Ent(entId, in Worlds.GetWorld(this.commandBuffer.ptr->worldId)));
                }
                
            }

        }
        
        /// <summary>
        /// Schedule query and returns its handle
        /// When you need to get results - call handle.GetResults()
        /// To dispose results use handle.Dispose()
        /// </summary>
        /// <returns>OnDemandHandle</returns>
        public OnDemandArray ToArrayOnDemand(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMP) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            var handle = new OnDemandArray() {
                dependsOn = this.builderDependsOn,
                data = _makeDefault(new OnDemandArray.Data() {
                    results = new Unity.Collections.NativeList<Ent>(allocator),
                }, allocator),
                allocator = allocator,
            };
            handle.dependsOn = new OnDemandJob() {
                handle = handle,
                commandBuffer = this.commandBuffer,
            }.Schedule(handle.dependsOn);
            return handle;
            
        }

        /// <summary>
        /// Schedule query and returns its handle
        /// When you need to get results - call handle.GetResults()
        /// To dispose results use handle.Dispose()
        /// </summary>
        /// <returns>OnDemandHandle</returns>
        public OnDemandArray ToArrayOnDemand(ref OnDemandArray array, Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMP) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            if (array.IsCreated == false) {
                array = new OnDemandArray() {
                    dependsOn = this.builderDependsOn,
                    data = _makeDefault(new OnDemandArray.Data() {
                        results = new Unity.Collections.NativeList<Ent>(allocator),
                    }, allocator),
                    allocator = allocator,
                };
            } else {
                array.dependsOn = this.builderDependsOn;
                array.Clear();
            }

            array.dependsOn = new OnDemandJob() {
                handle = array,
                commandBuffer = this.commandBuffer,
            }.Schedule(array.dependsOn);
            return array;
            
        }

        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public uint Count() {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, false, this.builderDependsOn);
            this.builderDependsOn.Complete();
            var cnt = (int)this.commandBuffer.ptr->count;
            return (uint)cnt;

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
                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
                this.commandBuffer.ptr->sync = true;
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
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public JobHandle ForEach(QueryDelegate forEach) {

            JobHandle handle = default;
            
            if (this.parallelForBatch > 0u) {

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
                this.commandBuffer.ptr->sync = false;
                JobHandle jobHandle;
                if (this.withBurst == true) {

                    var job = new JobParallelForBurst() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    jobHandle = job.Schedule(this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                } else {

                    var job = new JobParallelFor() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    jobHandle = job.Schedule(this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);

                }

                handle = this.Dispose(handle);

            } else {

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
                this.commandBuffer.ptr->sync = true;
                if (this.withBurst == true) {

                    var job = new JobBurst() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    var jobHandle = job.Schedule(this.commandBuffer.ptr, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);
                    handle = this.Dispose(handle);

                } else if (this.asJob == true) {
                    
                    var job = new Job() {
                        functionPointer = Callback.Create(forEach, this.withBurst),
                    };
                    var jobHandle = job.Schedule(this.commandBuffer.ptr, this.builderDependsOn);
                    handle = job.functionPointer.Dispose(jobHandle);
                    handle = this.Dispose(handle);

                } else {
                    
                    this.WaitForAllJobs();
                    
                    for (uint i = 0u; i < this.commandBuffer.ptr->count; ++i) {

                        var entId = this.commandBuffer.ptr->entities[i];
                        var entGen = Ents.GetGeneration(this.commandBuffer.ptr->state, entId);
                        var buffer = new CommandBufferJob(entId, entGen, this.commandBuffer);
                        forEach.Invoke(in buffer);

                    }

                    this.Dispose();

                }

            }

            this.builderDependsOn = handle;
            return handle;
        }
        
        #if !ENABLE_BECS_FLAT_QUERIES
        [BURST]
        private struct FromQueryDataJob : IJob {

            public safe_ptr<State> state;
            public safe_ptr<Queries.QueryDataStatic> queryDataStatic;
            public safe_ptr<QueryData> queryData;
            
            public void Execute() {
                
                this.queryData.ptr->archetypes = this.queryDataStatic.ptr->archetypes.GetUnsafePtr(in this.state.ptr->allocator);
                this.queryData.ptr->archetypesCount = this.queryDataStatic.ptr->archetypes.Count;
                
            }

        }
        
        [INLINE(256)]
        internal QueryBuilderDisposable FromQueryData(safe_ptr<State> state, ushort worldId, safe_ptr<Queries.QueryDataStatic> queryDataStatic) {

            //this.queryData = queryDataStatic->GetQueryData(state);
            //this.commandBuffer = queryDataStatic->GetCommandBuffer(state, worldId);
            
            this.builderDependsOn = new FromQueryDataJob() {
                queryDataStatic = queryDataStatic,
                state = state,
                queryData = this.queryData,
            }.Schedule(this.builderDependsOn);
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn, fromStaticQuery: true);
            //this.builderDependsOn.Complete();
            return new QueryBuilderDisposable(this);

        }
        #endif

        [BURST]
        public struct FillTrueBitsJob : IJobParallelForDefer {

            public safe_ptr<QueryData> queryData;
            public NativeParallelList<uint> list;

            public void Execute(int index) {
                var hasBit = this.queryData.ptr->archetypesBits.IsSet(index);
                if (hasBit == true) this.list.Add((uint)index);
            }

        }

        [BURST]
        public struct FillArchetypesJob : IJob {

            public safe_ptr<QueryData> queryData;
            public ME.BECS.NativeCollections.NativeParallelList<uint> list;

            public void Execute() {
                var list = this.list.ToList(Constants.ALLOCATOR_TEMP);
                var ptr = list.Ptr;
                this.queryData.ptr->archetypes = new safe_ptr<uint>(ptr, (byte*)ptr, (byte*)(ptr + list.Length));
                this.queryData.ptr->archetypesCount = (uint)list.Length;
            }

        }

        [BURST]
        public struct SetEntitiesJob : IJob {

            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            public SafetyComponentContainerRO<TNull> safety;
            #endif

            public ArchetypeQueries.ComposeJob composeJob;
            public safe_ptr<State> state;
            public safe_ptr<CommandBuffer> buffer;
            public safe_ptr<QueryData> queryData;
            public Allocator allocator;
            public bool useSort;

            public void Execute() {

                #if ENABLE_BECS_FLAT_QUERIES
                var allCount = (this.state.ptr->entities.Capacity + DataDenseSet.ENTITIES_PER_PAGE_MASK) / DataDenseSet.ENTITIES_PER_PAGE * DataDenseSet.ENTITIES_PER_PAGE;
                var tempBits = new TempBitArray(allCount, ClearOptions.ClearMemory, Constants.ALLOCATOR_TEMP);
                tempBits.Union(in this.state.ptr->allocator, in this.state.ptr->entities.aliveBits);
                
                {
                    var marker = new Unity.Profiling.ProfilerMarker("Query");
                    marker.Begin();
                    if (this.composeJob.query.with.Length > 0) {
                        for (int i = 0; i < this.composeJob.query.with.Length; ++i) {
                            var typeId = this.composeJob.query.with[i];
                            ref var ptr = ref this.state.ptr->components.items[this.state, typeId];
                            ref var storage = ref ptr.As<DataDenseSet>(in this.state.ptr->allocator);
                            var bits = storage.bits;
                            tempBits.Intersect(in this.state.ptr->allocator, in bits, allCount);
                        }
                    }
                    if (this.composeJob.query.withAny.Length > 0) {
                        var temp = new TempBitArray(allCount, allocator: Constants.ALLOCATOR_TEMP);
                        for (int i = 0; i < this.composeJob.query.withAny.Length; ++i) {
                            var typeIdPair = this.composeJob.query.withAny[i];
                            if (typeIdPair.Key > 0u) {
                                ref var ptr = ref this.state.ptr->components.items[this.state, typeIdPair.Key];
                                ref var storage = ref ptr.As<DataDenseSet>(in this.state.ptr->allocator);
                                var bits = storage.bits;
                                temp.Union(in this.state.ptr->allocator, in bits);
                            }
                            if (typeIdPair.Value > 0u) {
                                ref var ptr = ref this.state.ptr->components.items[this.state, typeIdPair.Value];
                                ref var storage = ref ptr.As<DataDenseSet>(in this.state.ptr->allocator);
                                var bits = storage.bits;
                                temp.Union(in this.state.ptr->allocator, in bits);
                            }
                        }
                        tempBits.Intersect(in temp, allCount);
                    }
                    for (int i = 0; i < this.composeJob.query.without.Length; ++i) {
                        var typeId = this.composeJob.query.without[i];
                        ref var ptr = ref this.state.ptr->components.items[this.state, typeId];
                        ref var storage = ref ptr.As<DataDenseSet>(in this.state.ptr->allocator);
                        var bits = storage.bits;
                        tempBits.Remove(in this.state.ptr->allocator, bits);
                    }
                    marker.End();
                }
                {
                    var marker = new Unity.Profiling.ProfilerMarker("GetTrueBits");
                    marker.Begin();
                    var trueBitsTemp = tempBits.GetTrueBitsTemp();
                    marker.End();
                    var elementsCount = (uint)trueBitsTemp.Length;
                    if (elementsCount == 0u) {
                        this.buffer.ptr->entities = null;
                        this.buffer.ptr->count = 0u;
                        return;
                    }

                    safe_ptr<uint> arrPtr;
                    if (this.queryData.ptr->steps > 0u) {

                        var currentStep = this.state.ptr->tick;
                        var steps = this.queryData.ptr->steps;
                        {
                            var elementsPerStep = elementsCount / steps;
                            if (elementsPerStep < this.queryData.ptr->minElementsPerStep) elementsPerStep = this.queryData.ptr->minElementsPerStep;
                            steps = (uint)math.ceil((elementsCount / (tfloat)elementsPerStep));
                            var fromIdx = (uint)(currentStep % steps) * elementsPerStep;
                            var toIdx = fromIdx + elementsPerStep;
                            if (toIdx > elementsCount) toIdx = elementsCount;
                            // Add range fromIdx..toIdx
                            var size = toIdx - fromIdx;
                            arrPtr = _makeArray<uint>(size, this.allocator);
                            if (size > 0u) _memcpy((safe_ptr)(trueBitsTemp.Ptr + fromIdx), arrPtr, TSize<uint>.size * size);
                            elementsCount = size;
                        }

                    } else {
                        arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                        _memcpy(new safe_ptr(trueBitsTemp.Ptr), arrPtr, elementsCount * TSize<uint>.size);
                    }

                    if (this.useSort == true) {
                        var markerSort = new Unity.Profiling.ProfilerMarker("Sort");
                        markerSort.Begin();
                        Unity.Collections.NativeSortExtension.Sort(trueBitsTemp.Ptr, (int)elementsCount);
                        markerSort.End();
                    }
                    this.buffer.ptr->entities = arrPtr.ptr;
                    this.buffer.ptr->count = elementsCount;
                }
                #else
                {
                    var marker = new Unity.Profiling.ProfilerMarker("Compose");
                    marker.Begin();
                    this.composeJob.allocator = this.allocator;
                    this.composeJob.Execute();
                    marker.End();
                }

                {
                    var archCount = this.queryData.ptr->archetypesCount;
                    // queryData.ptr->archetypes are set with static queries only
                    // so we need to check if it is null - we have a dynamic query
                    // build archetypes
                    if (this.queryData.ptr->archetypes.ptr == null) {
                        var marker = new Unity.Profiling.ProfilerMarker("GetTrueBitsTemp");
                        marker.Begin();
                        var tempListBits = this.queryData.ptr->archetypesBits.GetTrueBitsTemp();
                        #if MEMORY_ALLOCATOR_BOUNDS_CHECK || LEAK_DETECTION
                        this.queryData.ptr->archetypes = new safe_ptr<uint>(tempListBits.Ptr, (byte*)tempListBits.Ptr, (byte*)(tempListBits.Ptr + tempListBits.Length));
                        #else
                        this.queryData.ptr->archetypes = new safe_ptr<uint>(tempListBits.Ptr);
                        #endif
                        archCount = (uint)tempListBits.Length;
                        marker.End();
                    }

                    if (archCount == 0u) {
                        this.buffer.ptr->entities = null;
                        this.buffer.ptr->count = 0u;
                        return;
                    }

                    var archs = this.queryData.ptr->archetypes;
                    safe_ptr<uint> arrPtr = default;
                    var elementsCount = 0u;
                    if (this.queryData.ptr->steps > 0u) {

                        var currentStep = this.state.ptr->tick;
                        var temp = new UnsafeList<uint>((int)archCount, Constants.ALLOCATOR_TEMP);

                        for (uint i = 0u; i < archCount; ++i) {
                            var archIdx = archs[i];
                            ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                            temp.AddRange(arch.entitiesList.GetUnsafePtr(in this.state.ptr->allocator).ptr, (int)arch.entitiesList.Count);
                        }

                        var steps = this.queryData.ptr->steps;
                        var count = (uint)temp.Length;
                        if (count > 0u) {
                            var elementsPerStep = count / steps;
                            if (elementsPerStep < this.queryData.ptr->minElementsPerStep) elementsPerStep = this.queryData.ptr->minElementsPerStep;
                            steps = (uint)math.ceil((count / (tfloat)elementsPerStep));
                            var fromIdx = (uint)(currentStep % steps) * elementsPerStep;
                            var toIdx = fromIdx + elementsPerStep;
                            if (toIdx > count) toIdx = count;
                            // Add range fromIdx..toIdx
                            var size = toIdx - fromIdx;
                            arrPtr = _makeArray<uint>(size, this.allocator);
                            if (size > 0u) _memcpy((safe_ptr)(temp.Ptr + fromIdx), (safe_ptr)arrPtr, TSize<uint>.size * size);
                            elementsCount += size;
                        }

                        temp.Dispose();

                    } else {

                        {
                            var marker = new Unity.Profiling.ProfilerMarker("Count");
                            marker.Begin();
                            for (uint i = 0u; i < archCount; ++i) {
                                var archIdx = archs[i];
                                ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                                elementsCount += arch.entitiesList.Count;
                            }
                            marker.End();
                        }

                        if (elementsCount > 0u) {
                            var marker = new Unity.Profiling.ProfilerMarker("Copy");
                            marker.Begin();
                            arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                            var k = 0u;
                            for (uint i = 0u; i < archCount; ++i) {
                                var archIdx = archs[i];
                                ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                                if (arch.entitiesList.Count > 0u) _memcpy(arch.entitiesList.GetUnsafePtr(in this.state.ptr->allocator), arrPtr + k, TSize<uint>.size * arch.entitiesList.Count);
                                k += arch.entitiesList.Count;
                            }
                            marker.End();
                        }

                    }

                    if (this.useSort == true && elementsCount > 0u) {
                        var marker = new Unity.Profiling.ProfilerMarker("Sort");
                        marker.Begin();
                        Unity.Collections.NativeSortExtension.Sort(arrPtr.ptr, (int)elementsCount);
                        marker.End();
                    }
                    this.buffer.ptr->entities = arrPtr.ptr;
                    this.buffer.ptr->count = elementsCount;
                }
                #endif
                
            }

        }
        
        [INLINE(256)]
        internal JobHandle SetEntities(safe_ptr<CommandBuffer> buffer, bool useSort, JobHandle dependsOn, bool fromStaticQuery = false) {

            var allocator = WorldsTempAllocator.allocatorTemp.Get(this.WorldId).Allocator.ToAllocator;
            /*if (fromStaticQuery == false) {
                var counter = _makeDefault(new ME.BECS.NativeCollections.DeferJobCounter(), allocator);
                dependsOn = this.compose.Build(ref this, counter, dependsOn);
                var list = new ME.BECS.NativeCollections.NativeParallelList<uint>(10, allocator);
                dependsOn = new FillTrueBitsJob() {
                    queryData = this.queryData,
                    list = list,
                }.Schedule(&counter.ptr->count, 64, dependsOn);
                dependsOn = new FillArchetypesJob() {
                    queryData = this.queryData,
                    list = list,
                }.Schedule(dependsOn);
                Worlds.GetWorld(buffer.ptr->worldId).AddEndTickHandle(list.Dispose(dependsOn));
            }*/
            var counter = _makeDefault(new ME.BECS.NativeCollections.DeferJobCounter(), allocator);
            var composeJob = this.compose.Build(ref this, counter);
            var job = new SetEntitiesJob() {
                composeJob = composeJob,
                buffer = buffer,
                queryData = this.queryData,
                state = buffer.ptr->state,
                allocator = allocator,
                useSort = useSort,
                #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
                safety = new SafetyComponentContainerRO<TNull>(buffer.ptr->state, Context.world.id),
                #endif
            };
            var handle = job.ScheduleByRef(dependsOn);
            return handle;

        }

        public struct Enumerator : System.Collections.Generic.IEnumerator<Ent> {

            public QueryBuilderDispose queryBuilder;
            public safe_ptr<CommandBuffer> commandBuffer;
            public uint index;
            public ushort worldId;
            
            public bool MoveNext() => this.index++ < this.commandBuffer.ptr->count;

            public Ent Current => new Ent(this.commandBuffer.ptr->entities[this.index - 1u], this.commandBuffer.ptr->state, this.worldId);

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

        private safe_ptr<CommandBuffer> commandBuffer;
        private safe_ptr<QueryData> queryData;
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
            this.queryData.ptr->Dispose();
            _free(ref this.queryData);
            this.commandBuffer.ptr->Dispose();
            _free(ref this.commandBuffer);
            this = default;
        }

    }

}