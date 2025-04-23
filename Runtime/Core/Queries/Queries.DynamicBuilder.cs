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

    [BURST(CompileSynchronously = true)]
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
        internal bool isCreated;
        
        public ushort WorldId => this.commandBuffer.ptr->worldId;

        [BURST(CompileSynchronously = true)]
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
        public QueryBuilder AsParallel() {
            E.IS_CREATED(this);
            this.scheduleMode = ScheduleMode.Parallel;
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
            this.compose.WithAny<T1, T2>();
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

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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
        [INLINE(256)]
        public JobHandle ScheduleParallelFor<T>(T job) where T : struct, IJobParallelForCommandBuffer {

            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
            this.builderDependsOn = job.Schedule(in this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
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
            this.builderDependsOn = job.Schedule(this.commandBuffer.ptr, this.parallelForBatch, this.builderDependsOn);
            return this.builderDependsOn;

        }
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public Unity.Collections.NativeArray<Ent> ToArray(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMP) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        public uint Count() {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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
                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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
        [INLINE(256)]
        public JobHandle ForEach(QueryDelegate forEach) {

            JobHandle handle = default;
            
            if (this.parallelForBatch > 0u) {

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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

                this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn);
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

        [BURST(CompileSynchronously = true)]
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
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.builderDependsOn, fromStaticQuery: true);
            //this.builderDependsOn.Complete();
            return new QueryBuilderDisposable(this);

        }

        [BURST(CompileSynchronously = true)]
        public struct FillTrueBitsJob : IJobParallelForDefer {

            public safe_ptr<QueryData> queryData;
            public NativeParallelList<uint> list;

            public void Execute(int index) {
                var hasBit = this.queryData.ptr->archetypesBits.IsSet(index);
                if (hasBit == true) this.list.Add((uint)index);
            }

        }

        [BURST(CompileSynchronously = true)]
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

        [BURST(CompileSynchronously = true)]
        private struct SetEntitiesJob : IJob {

            public safe_ptr<State> state;
            public safe_ptr<CommandBuffer> buffer;
            public safe_ptr<QueryData> queryData;
            public Allocator allocator;

            public void Execute() {

                {
                    var archCount = this.queryData.ptr->archetypesCount;
                    // queryData.ptr->archetypes are set with static queries only
                    // so we need to check if it is null - we have a dynamic query
                    // build archetypes
                    if (this.queryData.ptr->archetypes.ptr == null) {
                        var tempListBits = this.queryData.ptr->archetypesBits.GetTrueBitsTemp();
                        this.queryData.ptr->archetypes = new safe_ptr<uint>(tempListBits.Ptr, (byte*)tempListBits.Ptr, (byte*)(tempListBits.Ptr + tempListBits.Length));
                        archCount = (uint)tempListBits.Length;
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

                        /*elementsCount = archCount * 10u;
                        if (elementsCount > 0u) arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                        var k = 0u;
                        for (uint i = 0u; i < archCount; ++i) {
                            var archIdx = archs[i];
                            ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                            var prevCount = k;
                            k += arch.entitiesList.Count;
                            if (k > elementsCount) {
                                var oldArr = arrPtr;
                                var cnt = elementsCount;
                                elementsCount = math.max(elementsCount * 2u, k);
                                arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                                _memcpy(oldArr, arrPtr, TSize<uint>.size * cnt);
                                _free(oldArr, this.allocator);
                            }
                            _memcpy(arch.entitiesList.GetUnsafePtr(in this.state.ptr->allocator), arrPtr + prevCount, TSize<uint>.size * arch.entitiesList.Count);
                        }

                        elementsCount = k;*/

                        for (uint i = 0u; i < archCount; ++i) {
                            var archIdx = archs[i];
                            ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                            elementsCount += arch.entitiesList.Count;
                        }

                        if (elementsCount > 0u) {
                            arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                            var k = 0u;
                            for (uint i = 0u; i < archCount; ++i) {
                                var archIdx = archs[i];
                                ref var arch = ref this.state.ptr->archetypes.list[in this.state.ptr->allocator, archIdx];
                                if (arch.entitiesList.Count > 0u) _memcpy(arch.entitiesList.GetUnsafePtr(in this.state.ptr->allocator), arrPtr + k, TSize<uint>.size * arch.entitiesList.Count);
                                k += arch.entitiesList.Count;
                            }
                        }

                    }

                    if (elementsCount > 0u) Unity.Collections.NativeSortExtension.Sort(arrPtr.ptr, (int)elementsCount);
                    this.buffer.ptr->entities = arrPtr.ptr;
                    this.buffer.ptr->count = elementsCount;
                }
                
            }

        }
        
        [INLINE(256)]
        internal JobHandle SetEntities(safe_ptr<CommandBuffer> buffer, JobHandle dependsOn, bool fromStaticQuery = false) {

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
            dependsOn = this.compose.Build(ref this, counter, dependsOn);
            var job = new SetEntitiesJob() {
                buffer = buffer,
                queryData = this.queryData,
                state = buffer.ptr->state,
                allocator = allocator,
            };
            var handle = job.ScheduleByRef(dependsOn);
            JobHandle.ScheduleBatchedJobs();
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