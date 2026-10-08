namespace ME.BECS {

    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    

    /// <summary>
    /// Executes apply destroyed work through the job scheduler.
    /// </summary>
    [BURST]
    public partial struct ApplyDestroyedJob : IJobSingle {

        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;
        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
            
        /// <summary>
        /// Processes apply destroyed using the supplied job inputs.
        /// </summary>
        [INLINE(256)]
        public void Execute() {

            Ents.ApplyDestroyed(this.state, this.worldId);

        }

    }

    /// <summary>
    /// Executes start parallel work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct StartParallelJob : IJobSingle {
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        /// <summary>
        /// Safety handler used by <c>StartParallelJob</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<AtomicSafetyHandle> safetyHandler = Unity.Burst.SharedStatic<AtomicSafetyHandle>.GetOrCreate<StartParallelJob>();

        #pragma warning disable
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore
        #endif
        
        /// <summary>
        /// Buffer used to exchange or store the associated data.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        /// <summary>
        /// World and execution context supplied to the job.
        /// </summary>
        public JobInfo jobInfo;
        /// <summary>
        /// Inline count for the associated storage.
        /// </summary>
        public safe_ptr<uint> inlineCount;

        /// <summary>
        /// Initializes <c>StartParallelJob</c> from the supplied buffer, inline count, job info.
        /// </summary>
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

        /// <summary>
        /// Processes start parallel using the supplied job inputs.
        /// </summary>
        [INLINE(256)]
        public void Execute() {

            this.jobInfo.Prewarm(this.buffer, this.inlineCount);
            
        }

    }

    /// <summary>
    /// Executes finish parallel work through the job scheduler.
    /// </summary>
    [BURST]
    public unsafe partial struct FinishParallelJob : IJobSingle {
        
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        /// <summary>
        /// Safety handler used by <c>FinishParallelJob</c>.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<AtomicSafetyHandle> safetyHandler = Unity.Burst.SharedStatic<AtomicSafetyHandle>.GetOrCreate<StartParallelJob>();

        #pragma warning disable
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore
        #endif
        
        /// <summary>
        /// Buffer used to exchange or store the associated data.
        /// </summary>
        [NativeDisableUnsafePtrRestriction]
        public CommandBuffer* buffer;
        /// <summary>
        /// World and execution context supplied to the job.
        /// </summary>
        public JobInfo jobInfo;
        /// <summary>
        /// Inline count for the associated storage.
        /// </summary>
        public safe_ptr<uint> inlineCount;

        /// <summary>
        /// Initializes <c>FinishParallelJob</c> from the supplied buffer, inline count, job info.
        /// </summary>
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

        /// <summary>
        /// Processes finish parallel using the supplied job inputs.
        /// </summary>
        [INLINE(256)]
        public void Execute() {

            this.jobInfo.Dispose(this.buffer, this.inlineCount);

        }

    }

}
