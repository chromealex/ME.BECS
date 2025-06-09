#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

using ME.BECS.Transforms;

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Can Fire system")]
    [RequiredDependencies(typeof(ReloadSystem))]
    public struct CanFireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<AttackAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                if (aspect.componentRuntimeFire.fireTimer <= 0f && aspect.IsAnyTargetInSector() == false) {
                    return;
                }

                if (aspect.componentRuntimeFire.fireTimer <= 0f) {
                    var bullet = aspect.readComponentVisual.bulletConfig;
                    bullet.UnsafeConfig.TryRead(out ME.BECS.Bullets.BulletConfigComponent bulletConfig);
                    if (bulletConfig.autoTarget == false) {
                        var sourceUnit = ent.GetParent();
                        if (aspect.componentRuntimeFire.targets.IsCreated == true) aspect.componentRuntimeFire.targets.Dispose();
                        aspect.componentRuntimeFire.targets = new MemArrayAuto<float3>(in ent, aspect.targets.Count > 0u ? aspect.targets.Count : 1u);
                        if (aspect.targets.Count > 0u) {
                            for (uint i = 0u; i < aspect.targets.Count; ++i) {
                                aspect.componentRuntimeFire.targets[i] = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in sourceUnit, in aspect.targets[i]);
                            }
                        } else {
                            aspect.componentRuntimeFire.targets[0u] = ME.BECS.Units.UnitUtils.GetTargetBulletPosition(in sourceUnit, aspect.target);
                        }
                    } else {
                        if (aspect.componentRuntimeFire.targets.IsCreated == true) aspect.componentRuntimeFire.targets.Dispose();
                    }
                }
                
                aspect.componentRuntimeFire.fireTimer += this.dt;
                if (aspect.readComponentRuntimeFire.fireTimer >= aspect.readComponent.fireTime) {

                    // time to attack is up
                    // fire - reset target and reload
                    aspect.IsReloaded = false;
                    aspect.CanFire = false;

                }
                
                if (aspect.IsFireUsed() == false) {

                    // use default attack time
                    if (aspect.readComponentRuntimeFire.fireTimer >= aspect.readComponent.attackTime) {
                        // Fire
                        var parent = ent.Read<ME.BECS.Transforms.ParentComponent>().value.GetAspect<ME.BECS.Units.UnitAspect>();
                        if (ent.HasTag<CanFireWhileMovesTag>(true) == false && parent is { IsHold: false, IsStatic: false }) {
                            aspect.CanFire = false;
                        } else {
                            aspect.CanFire = true;
                        }
                    }

                }
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().With<ReloadedComponent>().WithAny<AttackTargetComponent, AttackTargetsComponent>().Schedule<Job, AttackAspect>(new Job() {
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);

        }

    }

}