using ME.BECS.Views;

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(ShadowCopySystem))]
    public struct ShadowCopyUpdateSystem : IUpdate {

        private static void UpdateShadowCopy(in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy, bool isVisible) {
            
            // Update only visible objects
            if (isVisible == true) {
                
                if (shadowCopy.original.IsAlive() == false) {
                    // if object is already destroyed - just destroy the shadow copy
                    ent.DestroyHierarchy();
                    return;
                }
                    
                // if object is still alive - update properties
                var origTr = shadowCopy.original.GetAspect<TransformAspect>();
                var pos = origTr.position;
                var rot = origTr.rotation;
                ent.CopyFrom<ParentComponent, ChildrenComponent>(in shadowCopy.original);
                var tr = ent.GetAspect<TransformAspect>();
                tr.position = pos;
                tr.rotation = rot;
                ent.SetTag<IsViewRequested>(false);
                ent.SetTag<FogOfWarShadowCopyWasVisible>(true);
                //ent.Remove<FogOfWarHasShadowCopyComponent>();
                
            } else {
                if (ent.Has<FogOfWarShadowCopyWasVisible>() == true) {
                    ent.SetTag<IsViewRequested>(true);
                }
            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct UpdatePointsJob : IJobParallelForComponents<FogOfWarShadowCopyComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy) {

                var isVisible = this.fow.IsVisible(in shadowCopy.forTeam, ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                UpdateShadowCopy(in ent, ref shadowCopy, isVisible);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct UpdateRectJob : IJobParallelForComponents<FogOfWarShadowCopyComponent, FogOfWarShadowCopyPointsComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy, ref FogOfWarShadowCopyPointsComponent points) {

                var isVisible = this.fow.IsVisibleAny(in shadowCopy.forTeam, in points.points);
                UpdateShadowCopy(in ent, ref shadowCopy, isVisible);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;
            
            // Update all shadow copies
            var fow = logicWorld.GetSystem<CreateSystem>();
            var dependsOnPoints = context.Query().Without<FogOfWarShadowCopyPointsComponent>().Schedule<UpdatePointsJob, FogOfWarShadowCopyComponent>(new UpdatePointsJob() {
                fow = fow,
            });
            var dependsOnRect = context.Query().With<FogOfWarShadowCopyPointsComponent>().Schedule<UpdateRectJob, FogOfWarShadowCopyComponent, FogOfWarShadowCopyPointsComponent>(new UpdateRectJob() {
                fow = fow,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(dependsOnPoints, dependsOnRect));
            
        }

    }

}