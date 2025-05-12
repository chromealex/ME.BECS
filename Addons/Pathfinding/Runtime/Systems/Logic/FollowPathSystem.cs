#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a path.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct FollowPathSystem : IUpdate {

        public static FollowPathSystem Default => new FollowPathSystem() {
            collisionForce = 15f,
            cohesionForce = 1f,
            separationForce = 1f,
            alignmentForce = 1f,
            movementForce = 1f,
        };

        public tfloat collisionForce;
        public tfloat cohesionForce;
        public tfloat separationForce;
        public tfloat alignmentForce;
        public tfloat movementForce;
        
        [BURST(CompileSynchronously = true)]
        public struct PathFollowJob : IJobForAspects<TransformAspect, UnitAspect> {

            public World world;
            public tfloat dt;
            public BuildGraphSystem buildGraphSystem;
            public FollowPathSystem followPathSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit) {

                var pos = tr.position;

                if (unit.IsPathFollow == false) {
                    // just apply rvo direction
                    this.Move(ref tr, ref unit, in float3.zero, false);
                    return;
                }

                if (unit.unitCommandGroup.IsAlive() == false) {
                    unit.IsPathFollow = false;
                    return;
                }

                var group = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
                var target = group.targets[unit.typeId];
                if (target.IsAlive() == false) {
                    unit.IsPathFollow = false;
                    return;
                }
                
                var targetComponent = target.Read<TargetComponent>();
                if (targetComponent.target.IsAlive() == false) {
                    return;
                }
                
                var targetPathComponent = target.Read<TargetPathComponent>();
                var path = targetPathComponent.path;
                if (path.IsCreated == false) {
                    return;
                }
                
                var dir = Graph.GetDirection(in this.world, pos, in path, out var complete);
                if ((unit.collideWithEnd == 1 && PathUtils.HasArrived(in tr, in unit) == true) ||
                    (path.IsCreated == true && complete == true)) {
                    // complete path
                    unit.IsPathFollow = false;
                    PathUtils.SetArrived(in unit);
                    return;
                }

                this.Move(ref tr, ref unit, in dir, true);

                var root = path.graph.Read<RootGraphComponent>();
                var chunkIndex = Graph.GetChunkIndex(in root, pos, false);
                if (chunkIndex != uint.MaxValue) {
                    targetPathComponent.chunksToUpdate[chunkIndex] = 1;
                }

            }

            [INLINE(256)]
            private void Move(ref TransformAspect tr, ref UnitAspect unit, in float3 movementDirection, bool isMoving) {

                var agent = unit.ent.Read<AgentComponent>();
                var graph = this.buildGraphSystem.GetGraphByTypeId(unit.typeId);
                var vel = unit.readComponentRuntime.cohesionVector * this.followPathSystem.cohesionForce + 
                          unit.readComponentRuntime.separationVector * this.followPathSystem.separationForce + 
                          unit.readComponentRuntime.alignmentVector * this.followPathSystem.alignmentForce +
                          unit.readComponentRuntime.collisionDirection * this.followPathSystem.collisionForce +
                          movementDirection * this.followPathSystem.movementForce;
                var desiredDirection = math.normalizesafe(vel);

                unit.componentRuntime.desiredDirection = desiredDirection;
                var lengthSq = math.lengthsq(unit.readComponentRuntime.desiredDirection);
                
                tfloat force = 0f;
                if (lengthSq > math.EPSILON) {
                    this.buildGraphSystem.heights.GetHeight(tr.position, out var unitNormal);
                    var rot = tr.rotation;
                    var toRot = quaternion.LookRotation(unit.readComponentRuntime.desiredDirection, unitNormal);
                    var maxDegreesDelta = this.dt * unit.readRotationSpeed;
                    var qAngle = math.angle(rot, toRot);
                    if (qAngle != 0f) {
                        toRot = math.slerp(rot, toRot, math.min(1.0f, maxDegreesDelta / qAngle));
                    }
                    tr.rotation = toRot;
                    var angle = mathext.angle(tr.forward, unit.readComponentRuntime.desiredDirection);
                    force = 1f - angle / 180f;
                }

                if (isMoving == false && lengthSq <= math.EPSILON) {
                    unit.speed = math.lerp(unit.readSpeed, 0f, this.dt * unit.readDecelerationSpeed);
                } else {
                    var accSpeed = unit.readAccelerationSpeed;
                    unit.speed = math.lerp(unit.readSpeed, math.select(0f, math.select(unit.readSpeed * 0.5f, unit.readMaxSpeed, force * 0.5f > 0.4f), force > 0.5f), this.dt * accSpeed);
                }

                {
                    var prevPos = tr.position;
                    prevPos.y = 0f;
                    var newPos = prevPos;
                    //newPos += tr.forward * unit.speed * this.dt;
                    newPos = Math.MoveTowards(newPos, newPos + unit.componentRuntime.desiredDirection, unit.readSpeed * this.dt);
                    //newPos += unit.componentRuntime.collisionDirection * (this.followPathSystem.collisionForce * this.dt);
                    newPos = GraphUtils.GetPositionWithMapBorders(graph, out var collisionDirection, in newPos, in prevPos, in agent.filter);
                    unit.velocity = newPos - prevPos;
                    var delta = collisionDirection * this.followPathSystem.collisionForce * this.dt;
                    if (GraphUtils.GetPositionWithMapBordersNode(out var node, in graph, newPos + delta) == true) {
                        newPos += delta;
                    }

                    newPos.y = this.buildGraphSystem.heights.GetHeight(newPos);
                    tr.position = newPos;
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct SpeedDownOnHoldJob : IJobForAspects<UnitAspect> {

            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitAspect unit) {
                
                unit.speed = math.lerp(unit.readSpeed, 0f, this.dt * unit.readDecelerationSpeed);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOnFollow = context.Query().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().AsUnsafe().AsParallel().Schedule<PathFollowJob, TransformAspect, UnitAspect>(new PathFollowJob() {
                world = context.world,
                dt = context.deltaTime,
                buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
                followPathSystem = this,
            });
            var dependsOnStop = context.Query().Without<IsUnitStaticComponent>().With<UnitHoldComponent>().AsUnsafe().AsParallel().Schedule<SpeedDownOnHoldJob, UnitAspect>(new SpeedDownOnHoldJob() {
                dt = context.deltaTime,
            });
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(dependsOnFollow, dependsOnStop));
            
        }

    }

}