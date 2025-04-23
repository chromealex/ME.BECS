#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.FogOfWar;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("If target is shadow copy - we need to change it to original if it is visible")]
    public struct ChangeAttackTargetFromShadowCopySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct TargetJob : IJobForAspects<AttackAspect> {

            public CreateSystem createSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                if (aspect.target.IsAlive() == true) {
                    if (aspect.target.TryRead(out FogOfWarShadowCopyComponent shadowCopy) == true) {
                        if (this.createSystem.IsVisible(aspect.ent.GetAspect<TransformAspect>().parent.GetAspect<UnitAspect>().readOwner.GetAspect<PlayerAspect>(), shadowCopy.original) == true) {
                            aspect.SetTarget(shadowCopy.original);
                        }
                    }
                }
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct TargetsJob : IJobForAspects<AttackAspect> {

            public CreateSystem createSystem;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                if (aspect.targets.Count > 0u) {
                    var owner = aspect.ent.GetAspect<TransformAspect>().parent.GetAspect<UnitAspect>().readOwner.GetAspect<PlayerAspect>();
                    for (uint i = 0u; i < aspect.targets.Count; ++i) {
                        var target = aspect.targets[i];
                        if (target.TryRead(out FogOfWarShadowCopyComponent shadowCopy) == true) {
                            if (this.createSystem.IsVisible(owner, shadowCopy.original) == true) {
                                aspect.SetTargetsAt(i, shadowCopy.original);
                            }
                        }
                    }
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var system = context.world.GetSystem<CreateSystem>();
            var dependsOnTarget = context.Query().AsParallel().With<AttackTargetComponent>().Schedule<TargetJob, AttackAspect>(new TargetJob() {
                createSystem = system,
            });
            var dependsOnTargets = context.Query().AsParallel().With<AttackTargetsComponent>().Schedule<TargetsJob, AttackAspect>(new TargetsJob() {
                createSystem = system,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(dependsOnTarget, dependsOnTargets));

        }

    }

}