#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    using Transforms;
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(BuildGraphSystem), typeof(CommandBuildSystem))]
    public struct CommandBuildUpdateSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct UpdateProgressJob : IJobForComponents<BuildInProgress> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref BuildInProgress buildInProgress) {

                if (buildInProgress.building.IsAlive() == false) {
                    ent.Remove<BuildInProgress>();
                    return;
                }
                ref var progress = ref buildInProgress.building.Get<BuildingInProgress>();
                if (progress.value < 1f) {
                    progress.lockSpinner.Lock();
                    if (progress.value < 1f) {
                        JobUtils.Increment(ref progress.value, this.dt / progress.timeToBuild);
                        if (progress.value >= 1f) {
                            // Building is complete
                            //UnityEngine.Debug.Log("Complete Building: " + buildInProgress.building);
                            // Complete building
                            buildInProgress.building.SetActiveHierarchy(true);
                            // Move all builders to the next target
                            for (uint i = 0u; i < progress.builders.Count; ++i) {
                                var builderEnt = progress.builders[i];
                                if (builderEnt.IsAlive() == false) continue;
                                builderEnt.Remove<BuildInProgress>();
                                var builder = builderEnt.GetAspect<UnitAspect>();
                                //UnityEngine.Debug.Log("Builder move next: " + builder.ent);
                                UnitUtils.SetNextTargetIfAvailable(in builder);
                            }

                            progress.builders.Clear();
                        }
                    }

                    progress.lockSpinner.Unlock();
                }
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct CompleteJob : IJobForComponents<BuildingInProgress> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref BuildingInProgress building) {

                if (building.value >= 1f) {

                    //UnityEngine.Debug.Log("Complete Job Building: " + ent);
                    ent.Remove<BuildingInProgress>();

                }
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var handle = context.Query().AsParallel().Schedule<UpdateProgressJob, BuildInProgress>(new UpdateProgressJob() {
                dt = context.deltaTime,
            });
            handle = context.Query(handle).AsParallel().Schedule<CompleteJob, BuildingInProgress>();
            context.SetDependency(handle);
            
        }

    }

}