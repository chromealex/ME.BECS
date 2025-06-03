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
namespace ME.BECS.Attack {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Pathfinding;
    using ME.BECS.Commands;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Move unit if it was damaged and is not attacking and without hold")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct MoveToAttackerSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct MoveToAttackerJob : IJobFor2Aspects1Components<UnitAspect, TransformAspect, DamageTookEvent> {

            public BuildGraphSystem buildGraphSystem;
            public SystemLink<ME.BECS.FogOfWar.CreateSystem> fogOfWarSystem;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit, ref TransformAspect transform, ref DamageTookEvent component) {

                if (component.source.IsAlive() == false) return;
                if (unit.readComponentRuntime.attackSensor.GetAspect<AttackAspect>().HasAnyTarget == true) return;

                // move to attacker
                var result = AttackUtils.GetPositionToAttack(in unit, in component.source, this.buildGraphSystem.GetNodeSize(), out var worldPos, in this.buildGraphSystem, in this.fogOfWarSystem);
                if (result == AttackUtils.ReactionType.RunAway) {
                    CommandsUtils.SetCommand(in this.buildGraphSystem, in unit, new ME.BECS.Commands.CommandMove() {
                        targetPosition = worldPos,
                    }, jobInfo);
                } else if (result == AttackUtils.ReactionType.RotateToTarget) {
                    unit.ent.Set(new UnitLookAtComponent() {
                        target = component.source.GetAspect<TransformAspect>().GetWorldMatrixPosition(),
                    });
                } else if (result == AttackUtils.ReactionType.MoveToTarget) {
                    CommandsUtils.SetCommand(in this.buildGraphSystem, in unit, new ME.BECS.Commands.CommandAttack() {
                        target = component.source,
                    }, jobInfo);
                    if (ent.HasStatic<AttackerFollowDistanceComponent>() == true) {
                        ent.Set(new ComebackAfterAttackComponent() {
                            returnToPosition = transform.position,
                        });
                    }
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct StopOnTargetJob : IJobFor1Aspects1Components<UnitAspect, UnitAttackCommandComponent> {

            public BuildGraphSystem buildGraphSystem;
            public SystemLink<ME.BECS.FogOfWar.CreateSystem> fogOfWarSystem;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit, ref UnitAttackCommandComponent target) {

                var t = unit.readUnitCommandGroup.GetAspect<UnitCommandGroupAspect>().readTargets[unit.readTypeId];
                if (unit.HasCommandGroup() == false || t.IsAlive() == false || t.Read<TargetPathComponent>().path.IsCreated == false) {
                    return;
                }
                if (target.target.IsAlive() == true && AttackUtils.CanAttack(in unit, in target.target) == true) {
                    var result = AttackUtils.GetPositionToAttack(in unit, in target.target, this.buildGraphSystem.GetNodeSize(), out _, in this.buildGraphSystem, in this.fogOfWarSystem);
                    if (result == AttackUtils.ReactionType.RotateToTarget) {
                        // Stop unit to attack
                        unit.ent.Set(new UnitLookAtComponent() {
                            target = target.target.GetAspect<TransformAspect>().GetWorldMatrixPosition(),
                        });
                        unit.IsPathFollow = false;
                        // unit.IsHold = true;
                        ent.Remove<ComebackAfterAttackComponent>();
                        
                    }
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct UpdatePathJob : IJobFor1Aspects1Components<UnitCommandGroupAspect, CommandAttack> {

            public BuildGraphSystem buildGraphSystem;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect group, ref CommandAttack command) {

                if (command.target.IsAlive() == false) return;
                
                var result = AttackUtils.GetPositionToAttack(in group, in command.target, this.buildGraphSystem.GetNodeSize(), out var pos, in this.buildGraphSystem);
                if (result == AttackUtils.ReactionType.MoveToTarget) {
                    PathUtils.UpdateTarget(in this.buildGraphSystem, group, pos, in jobInfo);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RemoveComebackAfterAttackComponentJob : IJobForEntity {
            
            public void Execute(in JobInfo jobInfo, in Ent ent) {
                ent.Remove<ComebackAfterAttackComponent>();
            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct ComebackAfterAttackJob : IJobFor2Aspects1Components<TransformAspect, UnitAspect, ComebackAfterAttackComponent> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit, ref ComebackAfterAttackComponent comeback) {

                var maxDistanceSqr = ent.ReadStatic<AttackerFollowDistanceComponent>();
                if (math.distancesq(tr.position, comeback.returnToPosition) < maxDistanceSqr.maxValueSqr) {
                    return;
                }
                CommandsUtils.SetCommand(in this.buildGraphSystem, in unit, new ME.BECS.Commands.CommandMove() {
                    targetPosition = comeback.returnToPosition,
                }, jobInfo);
                ent.Remove<ComebackAfterAttackComponent>();

            }

        }
        

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var fogOfWarSystem = context.world.GetSystemLink<ME.BECS.FogOfWar.CreateSystem>();
            context.Query()
                   .Without<IsUnitStaticComponent>()
                   .Without<PathFollowComponent>()
                   .Without<UnitHoldComponent>()
                   .Schedule<MoveToAttackerJob, UnitAspect, TransformAspect, DamageTookEvent>(new MoveToAttackerJob() {
                       buildGraphSystem = buildGraphSystem,
                       fogOfWarSystem = fogOfWarSystem,
                   }).AddDependency(ref context);

            context.Query()
                   .Schedule<UpdatePathJob, UnitCommandGroupAspect, CommandAttack>(new UpdatePathJob() {
                       buildGraphSystem = buildGraphSystem,
                   }).AddDependency(ref context);
            
            context.Query()
                   .Schedule<StopOnTargetJob, UnitAspect, UnitAttackCommandComponent>(new StopOnTargetJob() {
                       buildGraphSystem = buildGraphSystem,
                       fogOfWarSystem = fogOfWarSystem,
                   }).AddDependency(ref context);

            context.Query()
                   .With<ComebackAfterAttackComponent>()
                   .With<ReceivedCommandFromUserEvent>()
                   .Schedule<RemoveComebackAfterAttackComponentJob>()
                   .AddDependency(ref context);
            
            context.Apply();

            context.Query()
                   .Schedule<ComebackAfterAttackJob, TransformAspect, UnitAspect, ComebackAfterAttackComponent>(new ComebackAfterAttackJob() {
                       buildGraphSystem = buildGraphSystem,
                   }).AddDependency(ref context);

        }

    }

}