using ME.BECS.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Jobs;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule update a pathfinding graph.")]
    [RequiredDependencies(typeof(BuildGraphSystem), typeof(QuadTreeInsertSystem))]
    public struct UpdateGraphSystem : IAwake, IUpdate {

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

        [BURST(CompileSynchronously = true)]
        public unsafe struct CopyJob : Unity.Jobs.IJob {

            [ReadOnly]
            public NativeArray<bool> tempDirty;
            public NativeArray<bool> target;

            public void Execute() {

                _memcpy(this.tempDirty.GetUnsafeReadOnlyPtr(), this.target.GetUnsafePtr(), this.target.Length * TSize<bool>.size);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct SetObstaclesTreeMaskJob : IJobParallelForAspect<QuadTreeQueryAspect> {

            public int obstaclesTreeIndex;

            public void Execute(ref QuadTreeQueryAspect aspect) {
                aspect.query.treeMask = 1 << this.obstaclesTreeIndex;
            }

        }

        public void OnAwake(ref SystemContext context) {
            
            ref var trees = ref context.world.GetSystem<QuadTreeInsertSystem>();
            ref var buildGraphSystem = ref context.world.GetSystem<BuildGraphSystem>();
            buildGraphSystem.obstaclesTreeIndex = trees.AddTree();

            var dependsOn = context.Query().With<ChunkObstacleQuery>().ScheduleParallelFor<SetObstaclesTreeMaskJob, QuadTreeQueryAspect>(new SetObstaclesTreeMaskJob() {
                obstaclesTreeIndex = buildGraphSystem.obstaclesTreeIndex,
            });
            context.SetDependency(dependsOn);
            
        }
        
        public void OnUpdate(ref SystemContext context) {

            var graphSystem = context.world.GetSystem<BuildGraphSystem>();
            var dependencies = new NativeArray<Unity.Jobs.JobHandle>(graphSystem.graphs.Length, Allocator.Temp);
            for (var i = 0; i < graphSystem.graphs.Length; ++i) {
                var graphEnt = graphSystem.graphs[i];
                var tempDirty = new NativeArray<bool>(graphSystem.changedChunks.Length, Allocator.TempJob);
                var dependsOn = Graph.UpdateObstacles(in context.world, in graphEnt, tempDirty, context.dependsOn);
                // reset all changed chunks in all existing paths
                dependsOn = API.Query(in context.world, dependsOn).ScheduleParallelFor<ResetPathJob, TargetPathComponent>(new ResetPathJob() {
                    world = context.world,
                    invalidateChunks = tempDirty,
                });
                if (i == 0) {
                    dependsOn = new CopyJob() {
                        tempDirty = tempDirty,
                        target = graphSystem.changedChunks,
                    }.Schedule(dependsOn);
                }
                dependsOn = tempDirty.Dispose(dependsOn);
                dependencies[i] = dependsOn;
            }

            var resultDep = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            resultDep = API.Query(in context, resultDep).With<GraphMaskComponent>().ScheduleParallelFor<ResetDirtyJob, GraphMaskComponent>();

            context.SetDependency(resultDep);

        }

    }

}