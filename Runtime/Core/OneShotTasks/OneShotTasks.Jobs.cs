namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;

    public unsafe partial struct OneShotTasks {

        [BURST(CompileSynchronously = true)]
        private struct ResolveTasksJob : IJobSingle {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            public OneShotType type;
            public ushort updateType;

            public void Execute() {

                this.state->oneShotTasks.ResolveTasks(this.state, this.type, this.updateType);

            }

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static JobHandle ResolveTasks(State* state, OneShotType type, ushort updateType, JobHandle dependsOn) {
            var job = new ResolveTasksJob() {
                state = state,
                type = type,
                updateType = updateType,
            };
            return job.ScheduleSingleByRef(dependsOn);
        }

    }

}