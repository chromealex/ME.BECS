using ME.BECS.Jobs;

namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a path.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct FollowPathSystem : IUpdate {

        public static FollowPathSystem Default => new FollowPathSystem() {
            collisionForce = 15f,
            avoidanceForce = 1f,
            cohesionForce = 2f,
            separationForce = 1f,
            alignmentForce = 0.5f,
            movementForce = 5f,
        };

        public float collisionForce;
        public float avoidanceForce;
        public float cohesionForce;
        public float separationForce;
        public float alignmentForce;
        public float movementForce;
        
        [BURST(CompileSynchronously = true)]
        public struct PathFollowJob : ME.BECS.Jobs.IJobParallelForAspect<Transforms.TransformAspect, UnitAspect> {

            public World world;
            public float dt;
            public BuildGraphSystem buildGraphSystem;
            public FollowPathSystem followPathSystem;
            
            public void Execute(in JobInfo jobInfo, ref ME.BECS.Transforms.TransformAspect tr, ref UnitAspect unit) {

                var pos = tr.position;

                if (unit.IsPathFollow == false) {
                    // just apply rvo direction
                    this.Move(ref tr, ref unit, in float3.zero, false);
                    return;
                }

                if (unit.unitCommandGroup.IsAlive() == false) {
                    return;
                }

                var group = unit.unitCommandGroup.GetAspect<UnitCommandGroupAspect>();
                var target = group.targets[unit.typeId];
                if (target.IsAlive() == false) return;
                
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
                    targetPathComponent.chunksToUpdate[chunkIndex] = true;
                }

            }

            [INLINE(256)]
            private void Move(ref ME.BECS.Transforms.TransformAspect tr, ref UnitAspect unit, in float3 movementDirection, bool isMoving) {

                var agent = unit.ent.Read<AgentComponent>();
                var graph = this.buildGraphSystem.GetGraphByTypeId(unit.typeId);
                var desiredDirection = math.normalizesafe(movementDirection * this.followPathSystem.movementForce + 
                                                          unit.componentRuntime.cohesionVector * this.followPathSystem.cohesionForce + 
                                                          unit.componentRuntime.collisionDirection * this.followPathSystem.collisionForce +
                                                          unit.componentRuntime.separationVector * this.followPathSystem.separationForce + 
                                                          unit.componentRuntime.avoidanceVector * this.followPathSystem.avoidanceForce + 
                                                          unit.componentRuntime.alignmentVector * this.followPathSystem.alignmentForce);

                /*{
                    var prevPos = tr.position;
                    prevPos.y = 0f;
                    var newPos = prevPos;
                    newPos += desiredDirection * unit.speed * this.dt;
                    var movePos = GraphUtils.GetPositionWithMapBorders(graph, out var collisionDirection, in newPos, in prevPos, in agent.filter);
                    if (math.all(movePos == prevPos) == true) {
                        desiredDirection = collisionDirection;
                    }
                }*/
                unit.componentRuntime.desiredDirection = desiredDirection;
                var lengthSq = math.lengthsq(unit.componentRuntime.desiredDirection);
                
                var force = 0f;
                if (lengthSq > math.EPSILON) {
                    this.buildGraphSystem.heights.GetHeight(tr.position, out var unitNormal);
                    tr.rotation = math.slerp(tr.rotation, quaternion.LookRotation(unit.componentRuntime.desiredDirection, unitNormal), this.dt * unit.rotationSpeed);
                    var angle = UnityEngine.Vector3.Angle(tr.forward, unit.componentRuntime.desiredDirection);
                    force = 1f - angle / 180f;
                }

                if (isMoving == false && lengthSq <= math.EPSILON) {
                    unit.speed = math.lerp(unit.speed, 0f, this.dt * unit.decelerationSpeed);
                } else {
                    var accSpeed = unit.accelerationSpeed;
                    unit.speed = math.lerp(unit.speed, math.select(0f, math.select(unit.speed * 0.5f, unit.maxSpeed, force * 0.5f > 0.4f), force > 0.5f), this.dt * accSpeed);
                }

                {
                    var prevPos = tr.position;
                    prevPos.y = 0f;
                    var newPos = prevPos;
                    newPos += tr.forward * unit.speed * this.dt;
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

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).Without<IsUnitStaticComponent>().Schedule<PathFollowJob, Transforms.TransformAspect, UnitAspect>(new PathFollowJob() {
                world = context.world,
                dt = context.deltaTime,
                buildGraphSystem = context.world.GetSystem<BuildGraphSystem>(),
                followPathSystem = this,
            });
            context.SetDependency(dependsOn);
            
        }

    }

}