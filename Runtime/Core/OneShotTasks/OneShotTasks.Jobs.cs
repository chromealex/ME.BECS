namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;

    public partial struct OneShotTasks {

        [BURST(CompileSynchronously = true)]
        private struct ResolveTasksParallelJob : IJobParallelFor {

            public safe_ptr<State> state;
            public OneShotType type;
            public ushort updateType;

            public void Execute(int index) {

                ResolveThread(this.state, this.type, this.updateType, (uint)index);
                
            }

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static JobHandle ScheduleJobs(safe_ptr<State> state, OneShotType type, ushort updateType, JobHandle dependsOn) {
            return OneShotTasks.Schedule(state, type, updateType, dependsOn);
        }

    }

}