using ME.BECS.Jobs;

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule update a pathfinding graph.")]
    public struct UpdateGraphSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct ResetPathJob : ME.BECS.Jobs.IJobParallelForComponents<TargetPathComponent> {

            public Unity.Collections.NativeArray<bool> invalidateChunks;
            public World world;

            public void Execute(in Ent ent, ref TargetPathComponent path) {
                
                for (uint i = 0; i < this.invalidateChunks.Length; ++i) {
                    if (this.invalidateChunks[(int)i] == false) continue;
                    ref var chunk = ref path.path.chunks[this.world.state, i];
                    ref var flowField = ref chunk.flowField;
                    if (flowField.isCreated == true) flowField.Dispose(ref this.world.state->allocator);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct ResetDirtyJob : IJobParallelForComponents<GraphMaskComponent> {

            public void Execute(in Ent ent, ref GraphMaskComponent mask) {
                mask.isDirty = false;
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var graphSystem = context.world.GetSystem<BuildGraphSystem>();
            var dependencies = new NativeArray<Unity.Jobs.JobHandle>(graphSystem.graphs.Length, Allocator.Temp);
            for (var i = 0; i < graphSystem.graphs.Length; ++i) {
                var graphEnt = graphSystem.graphs[i];
                var root = graphEnt.Read<RootGraphComponent>();
                var changedChunks = new Unity.Collections.NativeArray<bool>((int)root.chunks.Length, Unity.Collections.Allocator.TempJob);
                var dependsOn = Graph.UpdateObstacles(in context.world, in graphEnt, changedChunks, context.dependsOn);
                // reset all changed chunks in all existing paths
                dependsOn = API.Query(in context.world, dependsOn).ScheduleParallelFor<ResetPathJob, TargetPathComponent>(new ResetPathJob() {
                    world = context.world,
                    invalidateChunks = changedChunks,
                });
                dependsOn = changedChunks.Dispose(dependsOn);
                dependencies[i] = dependsOn;
            }

            var resultDep = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            resultDep = API.Query(in context, resultDep).With<GraphMaskComponent>().ScheduleParallelFor<ResetDirtyJob, GraphMaskComponent>();

            context.SetDependency(resultDep);

        }

    }

}