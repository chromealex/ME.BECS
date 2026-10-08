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
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Unity.Jobs.LowLevel.Unsafe;
    using Unity.Collections;
    using ME.BECS.NativeCollections;
    
    /// <summary>
    /// Stores additional query configuration alongside the command buffer.
    /// </summary>
    public struct QueryData {

        internal uint steps;
        internal uint minElementsPerStep;

        /// <summary>
        /// Resets this value to its default state.
        /// </summary>
        public void Dispose() {
            this = default;
        }

    }

    /// <summary>
    /// Owns a deferred query count and the job dependency required to read it.
    /// </summary>
    public struct OnDemandCount : IIsCreated {

        internal struct Data {

            public int results;

        }

        /// <summary>
        /// Job dependency that must complete before the associated work can access its inputs.
        /// </summary>
        public JobHandle dependsOn {
            set => this.jobHandle.Value = value;
            get => this.jobHandle.Value;
        }

        internal NativeReference<Data> data;
        internal NativeReference<JobHandle> jobHandle;
        internal Allocator allocator;

        /// <summary>
        /// Completes the producing job and returns the resulting element count.
        /// </summary>
        public int Length {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                this.dependsOn.Complete();
                return this.data.Value.results;
            }
        }

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.data.IsCreated;

        /// <summary>
        /// Completes the producing job and frees this result's native storage.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            if (this.allocator == Allocator.Invalid) return;
            this.dependsOn.Complete();
            this.data.Dispose();
            this.jobHandle.Dispose();
            this = default;
        }

        /// <summary>
        /// Clears the current on demand count contents.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            var value = this.data.Value;
            value.results = 0;
            this.data.Value = value;
        }

    }

    /// <summary>
    /// Owns native query results that become readable after the producing job completes.
    /// </summary>
    public unsafe struct OnDemandArray : IIsCreated {
        
        internal struct Data {

            public UnsafeList<Ent> results;

        }
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        internal static readonly Unity.Burst.SharedStatic<int> s_staticSafetyId = Unity.Burst.SharedStatic<int>.GetOrCreate<OnDemandArray>();
        #endif
        
        internal NativeReference<Data> data;
        internal NativeReference<JobHandle> jobHandle;
        internal Allocator allocator;

        /// <summary>
        /// Job dependency that must complete before the associated work can access its inputs.
        /// </summary>
        public JobHandle dependsOn {
            set => this.jobHandle.Value = value;
            get => this.jobHandle.Value;
        }

        /// <summary>
        /// Initializes <c>OnDemandArray</c> from the supplied depends on, allocator.
        /// </summary>
        public OnDemandArray(JobHandle dependsOn, AllocatorManager.AllocatorHandle allocator) {
            
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            this.m_Safety = CollectionHelper.CreateSafetyHandle(allocator.Handle);
            if (UnsafeUtility.IsNativeContainerType<Ent>()) AtomicSafetyHandle.SetNestedContainer(this.m_Safety, true);
            CollectionHelper.SetStaticSafetyId<OnDemandArray>(ref this.m_Safety, ref s_staticSafetyId.Data);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(this.m_Safety, true);
            #endif

            this.jobHandle = new NativeReference<JobHandle>(dependsOn, allocator);
            this.data = new NativeReference<Data>(new OnDemandArray.Data() {
                results = new UnsafeList<Ent>(4, allocator),
            }, allocator.ToAllocator);
            this.allocator = allocator.ToAllocator;

        }
        
        /// <summary>
        /// Completes the producing job and returns the resulting element count.
        /// </summary>
        public int Length {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                this.dependsOn.Complete();
                return this.data.Value.results.Length;
            }
        }

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.data.IsCreated;

        /// <summary>
        /// Completes the producing job and returns a borrowed array view; do not dispose it separately or use it after the result is cleared or disposed.
        /// </summary>
        [INLINE(256)]
        public NativeArray<Ent> GetResults() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(this.m_Safety);
            var arraySafety = this.m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
            #endif
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Ent>(this.data.Value.results.Ptr, this.data.Value.results.Length, Allocator.None);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
            #endif
            return array;
        }

        /// <summary>
        /// Provides indexed access to the requested entry.
        /// </summary>
        public Ent this[int index] {
            [INLINE(256)]
            get {
                E.IS_CREATED(this);
                this.dependsOn.Complete();
                return this.data.Value.results[index];
            }
        }

        /// <summary>
        /// Completes the producing job and returns an enumerator over its result storage.
        /// </summary>
        public UnsafeList<Ent>.Enumerator GetEnumerator() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            return this.data.Value.results.GetEnumerator();
        }

        /// <summary>
        /// Completes the producing job and frees this result's native storage.
        /// </summary>
        [INLINE(256)]
        public void Dispose() {
            if (this.allocator == Allocator.Invalid) return;
            this.dependsOn.Complete();
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckDeallocateAndThrow(this.m_Safety);
            CollectionHelper.DisposeSafetyHandle(ref this.m_Safety);
            #endif
            this.data.Value.results.Dispose();
            this.data.Dispose();
            this.jobHandle.Dispose();
            this = default;
        }

        /// <summary>
        /// Clears the current on demand array contents.
        /// </summary>
        [INLINE(256)]
        public void Clear() {
            E.IS_CREATED(this);
            this.dependsOn.Complete();
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(this.m_Safety);
            #endif
            var value = this.data.Value;
            value.results.Clear();
            this.data.Value = value;
        }

    }

    /// <summary>
    /// Builds an entity query and carries the dependencies needed to evaluate or schedule it.
    /// </summary>
    [BURST]
    public unsafe ref partial struct QueryBuilder {
        
        internal safe_ptr<CommandBuffer> commandBuffer;
        internal safe_ptr<QueryData> queryData;
        internal FlatQueries.QueryCompose compose;
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
        
        /// <summary>
        /// World id used to locate the associated entry.
        /// </summary>
        public ushort WorldId => this.commandBuffer.ptr->worldId;

        [BURST]
        private partial struct DisposeJob : IJob {

            public safe_ptr<QueryData> queryData;
            public safe_ptr<CommandBuffer> commandBuffer;
            public FlatQueries.QueryCompose compose;
            public Allocator allocator;

            public void Execute() {
                
                this.compose.Dispose();
                
                this.queryData.ptr->Dispose();
                _free(this.queryData, this.allocator);
                
                this.commandBuffer.ptr->Dispose();
                _free(this.commandBuffer, this.allocator);

            }

        }
        
        /// <summary>
        /// Completes the dependency currently stored by this query builder.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WaitForAllJobs() {
            this.builderDependsOn.Complete();
            return this;
        }
        
        /// <summary>
        /// Releases query storage after the supplied dependency; combine the builder dependency explicitly and do not reuse or dispose copied builder handles.
        /// </summary>
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

        /// <summary>
        /// Releases query storage after the supplied dependency; combine the builder dependency explicitly and do not reuse or dispose copied builder handles.
        /// </summary>
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

        /// <summary>
        /// Configures the step count and minimum number of elements per step for query execution.
        /// </summary>
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

        /// <summary>
        /// Enables sorting for subsequent query execution.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder Sort() {
            this.useSort = true;
            return this;
        }

        /// <summary>
        /// Selects parallel execution for the query using the requested batch size.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder AsParallel(uint batch = 0u) {
            E.IS_CREATED(this);
            this.parallelForBatch = batch;
            this.scheduleMode = ScheduleMode.Parallel;
            return this;
        }

        /// <summary>
        /// Schedules or configures work over parallel iteration ranges.
        /// </summary>
        [INLINE(256)][System.Obsolete("ParallelFor is obsolete, use AsParallel(batch) instead.")]
        public QueryBuilder ParallelFor(uint batch) {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_AS_JOB(this.asJob);
            this.parallelForBatch = batch;
            this.scheduleMode = ScheduleMode.Parallel;
            return this;
        }

        /// <summary>
        /// Selects job execution for the query.
        /// </summary>
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public QueryBuilder AsJob() {
            E.IS_CREATED(this);
            E.QUERY_BUILDER_PARALLEL_FOR(this.parallelForBatch);
            this.asJob = true;
            return this;
        }

        /// <summary>
        /// Selects Burst execution for the associated operation.
        /// </summary>
        [INLINE(256)][System.ObsoleteAttribute("ForEach methods is obsolete and will be removed in a future version. Please use Schedule instead.")]
        public QueryBuilder WithBurst() {
            E.IS_CREATED(this);
            this.withBurst = true;
            return this;
        }
        
        
        /// <summary>
        /// Requires all specified component types in the query.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAll<T0, T1>() where T0 : unmanaged, IComponentBase
                                              where T1 : unmanaged, IComponentBase {
            this.With<T0>();
            this.With<T1>();
            return this;
        }

        /// <summary>
        /// Requires all specified component types in the query.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAll<T0, T1, T2>() where T0 : unmanaged, IComponentBase
                                                  where T1 : unmanaged, IComponentBase
                                                  where T2 : unmanaged, IComponentBase {
            this.With<T0>();
            this.With<T1>();
            this.With<T2>();
            return this;
        }

        /// <summary>
        /// Requires all specified component types in the query.
        /// </summary>
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

        /// <summary>
        /// Accepts entities matching at least one of the specified component types.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1>() where T0 : unmanaged, IComponentBase
                                              where T1 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.WithAny<T0, T1>();
            return this;
        }

        /// <summary>
        /// Accepts entities matching at least one of the specified component types.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2>() where T0 : unmanaged, IComponentBase
                                                  where T1 : unmanaged, IComponentBase
                                                  where T2 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.WithAny<T0, T1>();
            this.compose.WithAny<T2, TNull>();
            return this;
        }

        /// <summary>
        /// Accepts entities matching at least one of the specified component types.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAny<T0, T1, T2, T3>() where T0 : unmanaged, IComponentBase 
                                                      where T1 : unmanaged, IComponentBase 
                                                      where T2 : unmanaged, IComponentBase 
                                                      where T3 : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.WithAny<T0, T1>();
            this.compose.WithAny<T2, T3>();
            return this;
        }

        /// <summary>
        /// Requires the specified component or filter in the query.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder With<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.With<T>();
            return this;
        }

        /// <summary>
        /// Excludes entities matching the specified component or filter.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder Without<T>() where T : unmanaged, IComponentBase {
            E.IS_CREATED(this);
            this.compose.Without<T>();
            return this;
        }

        /// <summary>
        /// Adds the component requirements of the specified aspect to the query.
        /// </summary>
        [INLINE(256)]
        public QueryBuilder WithAspect<T>() where T : unmanaged, IAspect {
            E.IS_CREATED(this);
            this.compose.WithAspect<T>();
            return this;
        }
        
        private partial struct Job : IJobCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJob commandBuffer) => this.functionPointer.Invoke(in commandBuffer);
        }

        [BURST]
        private partial struct JobBurst : IJobCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJob commandBuffer) => this.functionPointer.Invoke(in commandBuffer);
        }

        private partial struct JobParallelFor : IJobParallelForCommandBuffer {
            public CallbackBurst functionPointer;
            public void Execute(in CommandBufferJobParallel commandBuffer) {
                var buffer = new CommandBufferJob(commandBuffer.entId, commandBuffer.entGen, commandBuffer.buffer);
                this.functionPointer.Invoke(in buffer);    
            }
        }

        [BURST]
        private partial struct JobParallelForBurst : IJobParallelForCommandBuffer {
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

        /// <summary>
        /// Defines the callback signature for query delegate.
        /// </summary>
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
        [System.Obsolete("Use ToArrayOnDemand() instead.")]
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

        /// <summary>
        /// Executes on demand work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct OnDemandJob : IJob {

            internal NativeReference<OnDemandArray.Data> handle;
            /// <summary>
            /// Command buffer containing the query or mutation work.
            /// </summary>
            public safe_ptr<CommandBuffer> commandBuffer;
            
            /// <summary>
            /// Processes on demand using the supplied job inputs.
            /// </summary>
            public void Execute() {
                
                var cnt = (int)this.commandBuffer.ptr->count;
                for (int i = 0; i < cnt; ++i) {
                    var entId = this.commandBuffer.ptr->entities[i];
                    var value = this.handle.Value;
                    value.results.Add(new Ent(entId, in Worlds.GetWorld(this.commandBuffer.ptr->worldId)));
                    this.handle.Value = value;
                }
                
            }

        }

        /// <summary>
        /// Executes on demand count work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct OnDemandCountJob : IJob {

            internal NativeReference<OnDemandCount.Data> handle;
            /// <summary>
            /// Command buffer containing the query or mutation work.
            /// </summary>
            public safe_ptr<CommandBuffer> commandBuffer;
            
            /// <summary>
            /// Processes on demand count using the supplied job inputs.
            /// </summary>
            public void Execute() {
                
                var value = this.handle.Value;
                value.results = (int)this.commandBuffer.ptr->count;
                this.handle.Value = value;

            }

        }

        /// <summary>
        /// Schedule query and returns its handle
        /// When you need to get results - call handle.GetResults()
        /// To dispose results use handle.Dispose()
        /// </summary>
        /// <returns>OnDemandArray</returns>
        public OnDemandArray ToArrayOnDemand(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMPJOB) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            var array = new OnDemandArray(this.builderDependsOn, allocator);
            array.dependsOn = new OnDemandJob() {
                handle = array.data,
                commandBuffer = this.commandBuffer,
            }.Schedule(array.dependsOn);
            array.dependsOn = this.Dispose(array.dependsOn);
            return array;
            
        }

        /// <summary>
        /// Schedule query and returns its handle
        /// When you need to get results - call handle.GetResults()
        /// To dispose results use handle.Dispose()
        /// </summary>
        /// <returns>OnDemandArray</returns>
        public OnDemandArray ToArrayOnDemand(ref OnDemandArray array, Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMPJOB) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            if (array.IsCreated == false) {
                array = new OnDemandArray(this.builderDependsOn, allocator);
            } else {
                array.Clear();
                array.dependsOn = this.builderDependsOn;
            }

            array.dependsOn = new OnDemandJob() {
                handle = array.data,
                commandBuffer = this.commandBuffer,
            }.Schedule(array.dependsOn);
            array.dependsOn = this.Dispose(array.dependsOn);
            return array;
            
        }

        /// <summary>
        /// Schedule query and returns its handle
        /// call handle.Length to get results length
        /// To dispose results use handle.Dispose()
        /// </summary>
        /// <returns>OnDemandCount</returns>
        public OnDemandCount CountOnDemand(Unity.Collections.Allocator allocator = Constants.ALLOCATOR_TEMPJOB) {
            
            this.builderDependsOn = this.SetEntities(this.commandBuffer, this.useSort, this.builderDependsOn);
            var array = new OnDemandCount() {
                jobHandle = new NativeReference<JobHandle>(this.builderDependsOn, allocator),
                data = new NativeReference<OnDemandCount.Data>(new OnDemandCount.Data(), allocator),
                allocator = allocator,
            };
            array.dependsOn = new OnDemandCountJob() {
                handle = array.data,
                commandBuffer = this.commandBuffer,
            }.Schedule(array.dependsOn);
            array.dependsOn = this.Dispose(array.dependsOn);
            return array;
            
        }
        
        /// <summary>
        /// [ QUERY END POINT ]
        /// </summary>
        /// <returns></returns>
        [System.Obsolete("Use CountOnDemand() instead.")]
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
        [System.Obsolete("Use ToArrayOnDemand() instead.")]
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
        

        /// <summary>
        /// Executes set entities work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct SetEntitiesJob : IJob {

            private struct BitWords {

                public safe_ptr<ulong> ptr;
                public uint length;

            }

            [INLINE(256)]
            private readonly BitWords GetComponentBitWords(uint typeId) {
                var ptr = this.state.ptr->components.items.GetUnsafePtr(this.state, typeId);
                var storage = ptr.ptr->AsPtr<DataDenseSet>(in this.state.ptr->allocator);
                var bits = storage.ptr->GetBits();
                return new BitWords() {
                    ptr = (safe_ptr<ulong>)this.state.ptr->allocator.GetUnsafePtr(in bits.ptr),
                    length = Bitwise.GetLength(bits.Length),
                };
            }

            [INLINE(256)]
            private static ulong ComposeWord(uint index,
                                             in BitWords alive,
                                             safe_ptr<BitWords> with,
                                             uint withCount,
                                             safe_ptr<BitWords> withAny,
                                             uint withAnyCount,
                                             bool hasWithAny,
                                             safe_ptr<BitWords> without,
                                             uint withoutCount) {
                if (index >= alive.length) return 0UL;
                var value = alive.ptr[index];
                for (uint i = 0u; i < withCount; ++i) {
                    var bits = with[i];
                    if (index >= bits.length) return 0UL;
                    value &= bits.ptr[index];
                    if (value == 0UL) return 0UL;
                }
                if (hasWithAny == true) {
                    var any = 0UL;
                    for (uint i = 0u; i < withAnyCount; ++i) {
                        var bits = withAny[i];
                        if (index < bits.length) any |= bits.ptr[index];
                    }
                    value &= any;
                    if (value == 0UL) return 0UL;
                }
                for (uint i = 0u; i < withoutCount; ++i) {
                    var bits = without[i];
                    if (index < bits.length) value &= ~bits.ptr[index];
                    if (value == 0UL) return 0UL;
                }
                return value;
            }

            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            /// <summary>
            /// Safety used by <c>QueryBuilder.SetEntitiesJob</c>.
            /// </summary>
            public SafetyComponentContainerRO<TNull> safety;
            #endif

            /// <summary>
            /// Compose used by <c>QueryBuilder.SetEntitiesJob</c>.
            /// </summary>
            public FlatQueries.QueryCompose compose;
            /// <summary>
            /// State accessed by the containing operation.
            /// </summary>
            public safe_ptr<State> state;
            /// <summary>
            /// Buffer used to exchange or store the associated data.
            /// </summary>
            public safe_ptr<CommandBuffer> buffer;
            /// <summary>
            /// Query data used by <c>QueryBuilder.SetEntitiesJob</c>.
            /// </summary>
            public safe_ptr<QueryData> queryData;
            /// <summary>
            /// Allocator used to access or manage the associated native storage.
            /// </summary>
            public Allocator allocator;
            /// <summary>
            /// Whether use sort behavior or state is selected.
            /// </summary>
            public bool useSort;

            /// <summary>
            /// Processes set entities using the supplied job inputs.
            /// </summary>
            public void Execute() {

                var allCount = (this.state.ptr->entities.Capacity + DataDenseSet.ENTITIES_PER_PAGE_MASK) / DataDenseSet.ENTITIES_PER_PAGE * DataDenseSet.ENTITIES_PER_PAGE;
                var wordCount = Bitwise.GetLength(allCount);
                var aliveBits = this.state.ptr->entities.aliveBits;
                var alive = new BitWords() {
                    ptr = (safe_ptr<ulong>)this.state.ptr->allocator.GetUnsafePtr(in aliveBits.ptr),
                    length = Bitwise.GetLength(aliveBits.Length),
                };

                var withCount = (uint)this.compose.with.Length;
                var with = withCount > 0u ? _makeArray<BitWords>(withCount, Constants.ALLOCATOR_TEMP, false) : default;
                for (uint i = 0u; i < withCount; ++i) {
                    with[i] = this.GetComponentBitWords(this.compose.with[(int)i]);
                }

                var hasWithAny = this.compose.withAny.Length > 0;
                var withAnyCapacity = (uint)this.compose.withAny.Length * 2u;
                var withAny = withAnyCapacity > 0u ? _makeArray<BitWords>(withAnyCapacity, Constants.ALLOCATOR_TEMP, false) : default;
                var withAnyCount = 0u;
                for (int i = 0; i < this.compose.withAny.Length; ++i) {
                    var typeIdPair = this.compose.withAny[i];
                    if (typeIdPair.Key > 0u) withAny[withAnyCount++] = this.GetComponentBitWords(typeIdPair.Key);
                    if (typeIdPair.Value > 0u) withAny[withAnyCount++] = this.GetComponentBitWords(typeIdPair.Value);
                }

                var withoutCount = (uint)this.compose.without.Length;
                var without = withoutCount > 0u ? _makeArray<BitWords>(withoutCount, Constants.ALLOCATOR_TEMP, false) : default;
                for (uint i = 0u; i < withoutCount; ++i) {
                    without[i] = this.GetComponentBitWords(this.compose.without[(int)i]);
                }

                var composedWords = _makeArray<ulong>(wordCount, Constants.ALLOCATOR_TEMP, false);
                var totalElementsCount = 0u;
                {
                    var marker = new Unity.Profiling.ProfilerMarker("Query");
                    marker.Begin();
                    for (uint i = 0u; i < wordCount; ++i) {
                        var value = ComposeWord(i, in alive, with, withCount, withAny, withAnyCount, hasWithAny, without, withoutCount);
                        composedWords[i] = value;
                        totalElementsCount += (uint)math.countbits(value);
                    }
                    marker.End();
                }

                if (totalElementsCount == 0u) {
                    this.buffer.ptr->entities = null;
                    this.buffer.ptr->count = 0u;
                    return;
                }

                var fromIdx = 0u;
                var elementsCount = totalElementsCount;
                if (this.queryData.ptr->steps > 0u) {
                    var currentStep = this.state.ptr->tick;
                    var steps = this.queryData.ptr->steps;
                    var elementsPerStep = totalElementsCount / steps;
                    if (elementsPerStep < this.queryData.ptr->minElementsPerStep) elementsPerStep = this.queryData.ptr->minElementsPerStep;
                    steps = (uint)math.ceil((totalElementsCount / (tfloat)elementsPerStep));
                    fromIdx = (uint)(currentStep % steps) * elementsPerStep;
                    var toIdx = fromIdx + elementsPerStep;
                    if (toIdx > totalElementsCount) toIdx = totalElementsCount;
                    elementsCount = toIdx - fromIdx;
                }

                var arrPtr = _makeArray<uint>(elementsCount, this.allocator);
                var sourceIndex = 0u;
                var destinationIndex = 0u;
                for (uint i = 0u; i < wordCount; ++i) {
                    var value = composedWords[i];
                    if (value == 0UL) continue;

                    var bitsCount = (uint)math.countbits(value);
                    if (sourceIndex + bitsCount <= fromIdx) {
                        sourceIndex += bitsCount;
                        continue;
                    }

                    var offset = i * 64u;
                    while (value != 0UL) {
                        var bit = math.tzcnt(value);
                        if (sourceIndex >= fromIdx) {
                            arrPtr[destinationIndex++] = offset + (uint)bit;
                            if (destinationIndex == elementsCount) break;
                        }
                        ++sourceIndex;
                        value &= value - 1UL;
                    }
                    if (destinationIndex == elementsCount) break;
                }
                this.buffer.ptr->entities = arrPtr.ptr;
                this.buffer.ptr->count = elementsCount;
                
            }

        }
        
        [INLINE(256)]
        internal JobHandle SetEntities(safe_ptr<CommandBuffer> buffer, bool useSort, JobHandle dependsOn) {

            var allocator = WorldsTempAllocator.allocatorTemp.Get(this.WorldId).Allocator.ToAllocator;
            var job = new SetEntitiesJob() {
                compose = this.compose,
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

        /// <summary>
        /// Traverses the entries exposed by <c>QueryBuilder</c>.
        /// </summary>
        public struct Enumerator : System.Collections.Generic.IEnumerator<Ent> {

            /// <summary>
            /// Query builder used by <c>QueryBuilder.Enumerator</c>.
            /// </summary>
            public QueryBuilderDispose queryBuilder;
            /// <summary>
            /// Command buffer containing the query or mutation work.
            /// </summary>
            public safe_ptr<CommandBuffer> commandBuffer;
            /// <summary>
            /// Index of this entry within its containing storage.
            /// </summary>
            public uint index;
            /// <summary>
            /// Identifier of the world whose state this value addresses.
            /// </summary>
            public ushort worldId;
            
            /// <summary>
            /// Advances the enumerator and reports whether a current element is available.
            /// </summary>
            public bool MoveNext() => this.index++ < this.commandBuffer.ptr->count;

            /// <summary>
            /// Element at the enumerator's current position.
            /// </summary>
            public Ent Current => new Ent(this.commandBuffer.ptr->entities[this.index - 1u], this.commandBuffer.ptr->state, this.worldId);

            object System.Collections.IEnumerator.Current => this.Current;

            /// <summary>
            /// Provides the <c>Reset</c> callback; this implementation performs no work.
            /// </summary>
            public void Reset() { }

            /// <summary>
            /// Releases the resources owned by this enumerator instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                this.queryBuilder.Dispose();
                this = default;
            }

        }

    }

    /// <summary>
    /// Provides disposal support for query-builder state.
    /// </summary>
    public unsafe struct QueryBuilderDispose {

        private safe_ptr<CommandBuffer> commandBuffer;
        private safe_ptr<QueryData> queryData;
        private JobHandle builderDependsOn;
        internal readonly bool isCreated;

        /// <summary>
        /// Initializes <c>QueryBuilderDispose</c> from the supplied query builder.
        /// </summary>
        [INLINE(256)]
        public QueryBuilderDispose(in QueryBuilder queryBuilder) {
            this.commandBuffer = queryBuilder.commandBuffer;
            this.queryData = queryBuilder.queryData;
            this.builderDependsOn = queryBuilder.builderDependsOn;
            this.isCreated = queryBuilder.isCreated;
        }
        
        /// <summary>
        /// Releases the resources owned by this query builder dispose instance.
        /// </summary>
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
