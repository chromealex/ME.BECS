#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;

    public partial struct SearchTargetSystem : IUpdate {

        [BURST]
        public struct SpatialSearchTargetJob : IJobForAspects<AttackAspect, SpatialQueryAspect, TransformAspect> {

            public World world;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref SpatialQueryAspect query, ref TransformAspect tr) {

                Ent requiredTarget = default;
                if (tr.parent.TryRead(out ME.BECS.Commands.UnitAttackCommandComponent attackCommandComponent) == true) {
                    // We have a manual target
                    if (attackCommandComponent.target.IsAlive() == true) {
                        requiredTarget = attackCommandComponent.target;
                    }
                }

                UnitAspect nearestResult = default;
                for (uint i = 0u; i < query.readResults.results.Count; ++i) {
                    var queryEnt = query.readResults.results[this.world.state, i];
                    if (queryEnt.IsAlive() == false) continue;
                    var result = queryEnt.GetAspect<UnitAspect>();
                    nearestResult = result;
                    if (requiredTarget.IsAlive() == true) {
                        if (result.ent == requiredTarget) break;
                    } else {
                        break;
                    }
                }

                if (nearestResult.IsAlive() == true && nearestResult.readHealth > 0f) {
                    aspect.SetTarget(nearestResult.ent);
                } else {
                    aspect.SetTarget(default);
                }

            }

        }

        [BURST]
        public struct SpatialSearchTargetsJob : IJobFor3Aspects1Components<AttackAspect, SpatialQueryAspect, TransformAspect, AttackTargetsCountComponent> {

            public World world;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref SpatialQueryAspect query, ref TransformAspect tr, ref AttackTargetsCountComponent targetsCountComponent) {

                var hasTarget = true;
                Ent requiredTarget = default;
                if (tr.parent.TryRead(out ME.BECS.Commands.UnitAttackCommandComponent attackCommandComponent) == true) {
                    // We have a manual target
                    if (attackCommandComponent.target.IsAlive() == true) {
                        requiredTarget = attackCommandComponent.target;
                        hasTarget = false;
                    }
                }

                ref var targets = ref ent.Get<AttackTargetsComponent>();
                var count = math.min(query.readResults.results.Count, targetsCountComponent.count);
                if (targets.targets.IsCreated == false) targets.targets = new ListAuto<Ent>(in ent, count);
                targets.targets.Clear();
                for (uint i = 0u; i < count; ++i) {
                    var queryEnt = query.readResults.results[this.world.state, i];
                    if (queryEnt.IsAlive() == false) continue;
                    if (requiredTarget == queryEnt) {
                        hasTarget = true;
                    }
                    targets.targets.Add(queryEnt);
                }

                if (hasTarget == true) {
                    aspect.SetTargets(in targets.targets);
                } else {
                    aspect.SetTargets(default);
                }

            }

        }

        public Unity.Jobs.JobHandle UpdateSpatial(ref SystemContext context) {

            var searchTarget = context.Query().AsParallel().AsUnsafe().Without<AttackTargetsCountComponent>().Schedule<SpatialSearchTargetJob, AttackAspect, SpatialQueryAspect, TransformAspect>(new SpatialSearchTargetJob() {
                world = context.world,
            });
            var searchTargets = context.Query().AsParallel().AsUnsafe().Schedule<SpatialSearchTargetsJob, AttackAspect, SpatialQueryAspect, TransformAspect, AttackTargetsCountComponent>(new SpatialSearchTargetsJob() {
                world = context.world,
            });
            return Unity.Jobs.JobHandle.CombineDependencies(searchTarget, searchTargets);

        }

    }

}