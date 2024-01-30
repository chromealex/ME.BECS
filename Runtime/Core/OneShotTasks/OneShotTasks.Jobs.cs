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

            public void Execute() {

                this.state->oneShotTasks.ResolveTasks(this.state, this.type);

            }

        }

        [INLINE(256)]
        [NotThreadSafe]
        public static JobHandle ResolveTasks(State* state, OneShotType type, JobHandle dependsOn) {
            if (dependsOn.IsCompleted == true) {
                new ResolveTasksJob() {
                    state = state,
                    type = type,
                }.Execute();
                return dependsOn;
            } else {
                var job = new ResolveTasksJob() {
                    state = state,
                    type = type,
                };
                return job.ScheduleSingleByRef(dependsOn);
            }
        }

    }

}