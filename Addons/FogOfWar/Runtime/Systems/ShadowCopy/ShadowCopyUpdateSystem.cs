using ME.BECS.Views;

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(ShadowCopySystem))]
    public struct ShadowCopyUpdateSystem : IUpdate {

        private static bool UpdateShadowCopy(in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy) {
            
            // Update only visible objects
            if (shadowCopy.original.IsAlive() == false) {
                // if object is already destroyed - just destroy the shadow copy
                ent.DestroyHierarchy();
                return false;
            }
            
            // if object is still alive - update properties
            var origTr = shadowCopy.original.GetAspect<TransformAspect>();
            var pos = origTr.position;
            var rot = origTr.rotation;
            var marker = new Unity.Profiling.ProfilerMarker("ent.CopyFrom");
            marker.Begin();
            ent.CopyFrom<ParentComponent, ChildrenComponent>(in shadowCopy.original);
            ent.SetActive(true);
            marker.End();
            FogOfWarUtils.ClearQuadTree(in ent);
            var tr = ent.GetAspect<TransformAspect>();
            tr.position = pos;
            tr.rotation = rot;
            ent.SetTag<IsViewRequested>(false);
            ent.SetTag<FogOfWarShadowCopyWasVisibleAnytimeTag>(true);
            return true;

        }
        
        [BURST(CompileSynchronously = true)]
        public struct UpdatePointsJob : IJobForComponents<FogOfWarShadowCopyComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy) {

                var isVisible = this.fow.IsVisible(in shadowCopy.forTeam, ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                if (isVisible == true && shadowCopy.original.IsAlive() == true) {
                    ent.SetTag<FogOfWarShadowCopyWasVisibleTag>(true);
                    return;
                }
                if (ent.Has<FogOfWarShadowCopyWasVisibleTag>() == true) {
                    ent.SetTag<FogOfWarShadowCopyWasVisibleTag>(false);
                    if (UpdateShadowCopy(in ent, ref shadowCopy) == true) {
                        ent.SetTag<IsViewRequested>(true);
                    }
                }
                
                if (shadowCopy.original.IsAlive() == false && ent.IsAlive() == true) {
                    ent.DestroyHierarchy();
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct UpdateRectJob : IJobForComponents<FogOfWarShadowCopyComponent, FogOfWarShadowCopyPointsComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy, ref FogOfWarShadowCopyPointsComponent points) {

                var isVisible = this.fow.IsVisibleAny(in shadowCopy.forTeam, in points.points);
                if (isVisible == true && shadowCopy.original.IsAlive() == true) {
                    ent.SetTag<FogOfWarShadowCopyWasVisibleTag>(true);
                    return;
                }
                if (ent.Has<FogOfWarShadowCopyWasVisibleTag>() == true) {
                    ent.SetTag<FogOfWarShadowCopyWasVisibleTag>(false);
                    if (UpdateShadowCopy(in ent, ref shadowCopy) == true) {
                        ent.SetTag<IsViewRequested>(true);
                    }
                }

                if (shadowCopy.original.IsAlive() == false && ent.IsAlive() == true) {
                    ent.DestroyHierarchy();
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var logicWorld = context.world.parent;
            
            // Update all shadow copies
            var fow = logicWorld.GetSystem<CreateSystem>();
            var dependsOnPoints = context.Query().Without<FogOfWarShadowCopyPointsComponent>().AsUnsafe().AsParallel().Schedule<UpdatePointsJob, FogOfWarShadowCopyComponent>(new UpdatePointsJob() {
                fow = fow,
            });
            var dependsOnRect = context.Query().With<FogOfWarShadowCopyPointsComponent>().AsUnsafe().AsParallel().Schedule<UpdateRectJob, FogOfWarShadowCopyComponent, FogOfWarShadowCopyPointsComponent>(new UpdateRectJob() {
                fow = fow,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(dependsOnPoints, dependsOnRect));
            
        }

    }

}