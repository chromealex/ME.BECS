namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;

    /// <summary>
    /// Tracks one-shot component work scheduled for lifecycle cleanup.
    /// </summary>
    public partial struct OneShotTasks {

        [BURST]
        private partial struct ResolveTasksParallelJob : IJobParallelFor {

            public safe_ptr<State> state;
            public OneShotType type;
            public ushort updateType;

            public void Execute(int index) {

                ResolveThread(this.state, this.type, this.updateType, (uint)index);
                
            }

        }

        /// <summary>
        /// Schedules jobs.
        /// </summary>
        [INLINE(256)]
        [NotThreadSafe]
        public static JobHandle ScheduleJobs(safe_ptr<State> state, OneShotType type, ushort updateType, JobHandle dependsOn) {
            return OneShotTasks.Schedule(state, type, updateType, dependsOn);
        }

    }

}