namespace ME.BECS {

    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    #if !ENABLE_BECS_FLAT_QUERIES
    [BURST]
    public struct ApplyJob : IJobSingle {

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
    #endif

    [BURST]
    public struct ApplyDestroyedJob : IJobSingle {

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

        #pragma warning disable 0649
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore 0649
        #endif
        
        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        public JobInfo jobInfo;
        public safe_ptr<uint> inlineCount;

        public StartParallelJob(CommandBuffer* buffer, safe_ptr<uint> inlineCount, in JobInfo jobInfo) {
            this.buffer = buffer;
            this.jobInfo = jobInfo;
            this.inlineCount = inlineCount;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_Safety = safetyHandler.Data;
            this.m_Length = (int)this.buffer->count;
            this.m_MinIndex = 0;
            this.m_MaxIndex = (int)this.buffer->count;
            #endif
        }

        [INLINE(256)]
        public void Execute() {

            this.jobInfo.Prewarm(this.buffer, this.inlineCount);
            
        }

    }

    [BURST]
    public unsafe struct FinishParallelJob : IJobSingle {
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        public static readonly Unity.Burst.SharedStatic<AtomicSafetyHandle> safetyHandler = Unity.Burst.SharedStatic<AtomicSafetyHandle>.GetOrCreate<StartParallelJob>();

        #pragma warning disable 0649
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore 0649
        #endif
        
        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        public JobInfo jobInfo;
        public safe_ptr<uint> inlineCount;

        public FinishParallelJob(CommandBuffer* buffer, safe_ptr<uint> inlineCount, in JobInfo jobInfo) {
            this.jobInfo = jobInfo;
            this.buffer = buffer;
            this.inlineCount = inlineCount;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_Safety = safetyHandler.Data;
            this.m_Length = (int)this.buffer->count;
            this.m_MinIndex = 0;
            this.m_MaxIndex = (int)this.buffer->count;
            #endif
        }

        [INLINE(256)]
        public void Execute() {

            this.jobInfo.Dispose(this.buffer, this.inlineCount);

        }

    }

}