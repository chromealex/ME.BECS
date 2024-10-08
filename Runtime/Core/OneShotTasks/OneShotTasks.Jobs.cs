namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;

    public unsafe partial struct OneShotTasks {

        [BURST(CompileSynchronously = true)]
        private struct ResolveTasksParallelJob : IJobParallelFor {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            public OneShotType type;
            public ushort updateType;
            public NativeList<Task>.ParallelWriter results;

            public void Execute(int index) {

                this.state->oneShotTasks.ResolveThread(this.state, this.type, this.updateType, (uint)index, this.results);
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct ResolveTasksComplete : IJobParallelForDefer {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            public OneShotType type;
            [ReadOnly]
            public NativeArray<Task>.ReadOnly items;
            
            public void Execute(int index) {
                
                this.state->oneShotTasks.ResolveCompleteThread(this.state, this.type, this.items, index);
                
            }

        }

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
            return state->oneShotTasks.ResolveTasksJobs(state, type, updateType, dependsOn);
        }

    }

}