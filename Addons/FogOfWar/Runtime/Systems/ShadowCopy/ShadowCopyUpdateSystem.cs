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
            return true;

        }
        
        private static void SetShadowCopy(in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy, bool isVisible) {
            if (isVisible == true) {
                ent.SetTag<FogOfWarShadowCopyWasVisibleAnytimeTag>(true);
            }
            if (isVisible == false && ent.HasTag<FogOfWarShadowCopyWasVisibleAnytimeTag>(true) == false) return;
            var shouldBeVisible = isVisible == false;
            var currentVisibilityState = shouldBeVisible == true && shadowCopy.original.IsAlive() == true && shadowCopy.original.IsActive() == true;
                
            bool isVisibilityStateJustChanged;
            bool wasVisible = ent.HasTag<FogOfWarShadowCopyWasVisibleTag>(true);

            if (currentVisibilityState == true) {
                isVisibilityStateJustChanged = wasVisible == false;
            } else {
                isVisibilityStateJustChanged = wasVisible == true;
            }
            
            //if visibility state has not changed or shadow copy was and currently is visible - do not change anything
            if (isVisibilityStateJustChanged == false || shouldBeVisible == true && wasVisible == true) return;
                
            if (UpdateShadowCopy(in ent, ref shadowCopy) == true) {
                ent.SetTag<IsViewRequested>(currentVisibilityState);
                ent.SetTag<FogOfWarShadowCopyWasVisibleTag>(currentVisibilityState);
            }
            
        }

        [BURST(CompileSynchronously = true)]
        public struct UpdatePointsJob : IJobForComponents<FogOfWarShadowCopyComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy) {

                var isPointVisible = this.fow.IsVisible(in shadowCopy.forTeam, ent.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                SetShadowCopy(ent, ref shadowCopy, isPointVisible);

            }
            
        }

        [BURST(CompileSynchronously = true)]
        public struct UpdateRectJob : IJobForComponents<FogOfWarShadowCopyComponent, FogOfWarShadowCopyPointsComponent> {

            public CreateSystem fow;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarShadowCopyComponent shadowCopy, ref FogOfWarShadowCopyPointsComponent points) {

                var isVisible = this.fow.IsVisibleAny(in shadowCopy.forTeam, in points.points);
                SetShadowCopy(ent, ref shadowCopy, isVisible);

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