#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Attack;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    using ME.BECS.Units;

    /// <summary>
    /// Coordinates change attack target from shadow copy during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("If target is shadow copy - we need to change it to original if it is visible")]
    public partial struct ChangeAttackTargetFromShadowCopySystem : IUpdate {

        /// <summary>
        /// Executes target work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct TargetJob : IJobForAspects<AttackAspect> {

            /// <summary>
            /// Create system used by <c>ChangeAttackTargetFromShadowCopySystem.TargetJob</c>.
            /// </summary>
            public CreateSystem createSystem;
            
            /// <summary>
            /// Processes target using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Executes targets work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct TargetsJob : IJobForAspects<AttackAspect> {

            /// <summary>
            /// Create system used by <c>ChangeAttackTargetFromShadowCopySystem.TargetsJob</c>.
            /// </summary>
            public CreateSystem createSystem;

            /// <summary>
            /// Processes targets using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Updates change attack target from shadow copy system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var system = context.world.GetSystem<CreateSystem>();
            var dependsOnTarget = context.Query().AsParallel().AsUnsafe().With<AttackTargetComponent>().Schedule<TargetJob, AttackAspect>(new TargetJob() {
                createSystem = system,
            });
            var dependsOnTargets = context.Query().AsParallel().AsUnsafe().With<AttackTargetsComponent>().Schedule<TargetsJob, AttackAspect>(new TargetsJob() {
                createSystem = system,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(dependsOnTarget, dependsOnTargets));

        }

    }

}