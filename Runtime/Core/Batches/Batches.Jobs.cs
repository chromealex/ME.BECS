namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [BURST]
    public unsafe struct ApplyJob : IJobSingle {

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        public SafetyComponentContainerRW<TNull> safety;
        #endif
        
        public ushort worldId;
        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            Batches.ApplyFromJob(this.worldId, this.state);

        }

    }

    [BURST]
    public unsafe struct ApplyFreeJob : IJobSingle {

        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            this.state.ptr->entities.free.Apply(in this.state.ptr->allocator);

        }

    }

    [BURST]
    public unsafe struct ApplyDestroyedJob : IJobSingle {

        public safe_ptr<State> state;
            
        [INLINE(256)]
        public void Execute() {

            Ents.ApplyDestroyed(this.state);

        }

    }

    [BURST]
    public unsafe struct StartParallelJob : IJobSingle {
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        public static readonly Unity.Burst.SharedStatic<AtomicSafetyHandle> safetyHandler = Unity.Burst.SharedStatic<AtomicSafetyHandle>.GetOrCreate<StartParallelJob>();

        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #endif
        
        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        public JobInfo jobInfo;

        public StartParallelJob(CommandBuffer* buffer, in JobInfo jobInfo) {
            this.buffer = buffer;
            this.jobInfo = jobInfo;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_Safety = safetyHandler.Data;
            this.m_Length = (int)this.buffer->count;
            this.m_MinIndex = 0;
            this.m_MaxIndex = (int)this.buffer->count;
            #endif
        }

        [INLINE(256)]
        public void Execute() {

            Ents.EnsureFree(this.buffer->state, this.buffer->worldId, this.buffer->count * this.jobInfo.itemsPerCall);
            
        }

    }

}