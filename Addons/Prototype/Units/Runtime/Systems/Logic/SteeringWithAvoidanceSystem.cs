#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Steering behaviour for units.")]
    [RequiredDependencies(typeof(QuadTreeQuerySystem))]
    public struct SteeringWithAvoidanceSystem : IUpdate, IDrawGizmos {

        public static SteeringWithAvoidanceSystem Default => new SteeringWithAvoidanceSystem() {
            calculateAvoidance = true,
            calculateSeparation = true,
            calculateCohesion = true,
            calculateAlignment = true,
            alignmentSpeed = 3f,
            maxAgentRadius = 2.5f,
        };

        public bool calculateAvoidance;
        public bool calculateSeparation;
        public bool calculateCohesion;
        public bool calculateAlignment;
        public tfloat alignmentSpeed;
        public tfloat maxAgentRadius;
        public bool drawGizmos;

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<TransformAspect, UnitAspect, QuadTreeQueryAspect> {

            public SteeringWithAvoidanceSystem system;
            public tfloat dt;
            public World world;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit, ref QuadTreeQueryAspect query) {

                var facingCone = math.cos(math.radians(120f));
                var collisionDir = float3.zero;
                var avoidanceVector = float3.zero;
                var cohesionVector = float3.zero;
                var separationVector = float3.zero;
                var alignmentVector = float3.zero;
                var cohesionUnitsCount = 0u;
                var alignmentUnitsCount = 0u;
                var rangeSq = query.readQuery.rangeSqr;
                var srcPos = tr.position;
                srcPos.y = 0f;
                for (uint i = 0, size = query.readResults.results.Count; i < size; ++i) {
                    var queryEnt = query.readResults.results[this.world.state, i];
                    if (queryEnt.IsAlive() == false) continue;
                    if (queryEnt == unit.ent) continue;

                    var entTr = queryEnt.GetAspect<TransformAspect>();
                    var entUnit = queryEnt.GetAspect<UnitAspect>();
                    
                    if (entUnit.IsPathFollow == false && 
                        entUnit.IsHold == false &&
                        unit.IsPathFollow == true) {
                        // unit is not moving and not on hold - so we can skip this unit
                        // if we are follow the path
                        continue;
                    }
                    var targetPos = entTr.position;
                    targetPos.y = 0f;
                    var vec = targetPos - srcPos;
                    if (math.all(vec == float3.zero) == true) {
                        vec = unit.randomVector;
                    }
                    var normal = math.normalizesafe(vec);

                    // check collide with end
                    // if unit collides with another unit which stops
                    // and belongs to the same group
                    var isGroupEquals = entUnit.IsPathFollow == false &&
                                        entUnit.unitCommandGroup == unit.unitCommandGroup;

                    var radiusSum = (unit.radius + entUnit.radius);
                    var radiusSumSq = radiusSum * radiusSum;
                    
                    var lengthSqr = math.lengthsq(vec);
                    if (isGroupEquals == false && lengthSqr <= rangeSq && unit.IsPathFollow == true) {
                        var isFacing = IsFacing(tr.right, normal, facingCone);
                        var relativePos = srcPos - targetPos;
                        var relativeVel = unit.velocity - entUnit.velocity;
                        // check avoidance
                        if (this.system.calculateAvoidance == true) {
                            var relativeSpeed = math.lengthsq(relativeVel);
                            if (relativeSpeed > 0f) {
                                var timeToCollision = -1f * math.dot(relativePos, relativeVel) / (relativeSpeed * relativeSpeed);
                                if (timeToCollision > 0f) {
                                    avoidanceVector += relativePos + relativeVel * timeToCollision;
                                }
                            }
                        }

                        // check separation
                        if (this.system.calculateSeparation == true) {
                            var maxSepDistSq = 1f + this.system.maxAgentRadius;
                            var distSq = math.lengthsq(relativePos);
                            if (distSq < maxSepDistSq) {
                                var strength = unit.accelerationSpeed * (maxSepDistSq - distSq) / (maxSepDistSq - radiusSumSq);
                                separationVector += -normal * strength;
                            }
                        }

                        // check cohesion
                        if (this.system.calculateCohesion == true) {
                            if (isFacing == true) {
                                cohesionVector += targetPos;
                                ++cohesionUnitsCount;
                            }
                        }
                        
                        // check alignment
                        if (this.system.calculateAlignment == true) {
                            if (isFacing == true) {
                                var vel = -relativeVel;
                                vel /= unit.maxSpeed;
                                alignmentVector += vel;
                                ++alignmentUnitsCount;
                            }
                        }
                    }

                    var length = math.sqrt(lengthSqr);
                    if (length <= radiusSum) {
                        {
                            // move to normal
                            var collision = -normal * (radiusSum - length);
                            collisionDir += collision;
                            //tr.position += collision;
                        }
                        {
                            // set the flag
                            if (isGroupEquals == true) {
                                unit.collideWithEnd = 1;
                            }
                        }
                    }

                }

                unit.componentRuntime.collisionDirection = math.normalizesafe(collisionDir);
                unit.componentRuntime.avoidanceVector = math.normalizesafe(avoidanceVector);
                unit.componentRuntime.separationVector = math.normalizesafe(separationVector);
                if (cohesionUnitsCount > 0u) {
                    unit.componentRuntime.cohesionVector = math.normalizesafe(cohesionVector / cohesionUnitsCount);
                } else {
                    unit.componentRuntime.cohesionVector = float3.zero;
                }
                if (alignmentUnitsCount > 0u) {
                    unit.componentRuntime.alignmentVector = math.lerp(unit.componentRuntime.alignmentVector, alignmentVector / alignmentUnitsCount, this.dt * this.system.alignmentSpeed);
                } else {
                    unit.componentRuntime.alignmentVector = float3.zero;
                }

            }
            
            [INLINE(256)]
            private static bool IsFacing(float3 rightTransformVector, float3 normal, tfloat cosineValue) {
                return math.dot(rightTransformVector, normal) >= cosineValue;
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().Schedule<Job, TransformAspect, UnitAspect, QuadTreeQueryAspect>(new Job() {
                world = context.world,
                system = this,
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);
            
        }

        public void OnDrawGizmos(ref SystemContext context) {

            if (this.drawGizmos == false) return;
            
            var arr = context.Query().AsParallel().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().WithAspect<UnitAspect>().WithAspect<TransformAspect>().ToArray();
            foreach (var unitEnt in arr) {

                var tr = unitEnt.GetAspect<TransformAspect>();
                var unit = unitEnt.GetAspect<UnitAspect>();
                UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)tr.position, (float)unit.readRadius);

            }
            
        }

    }

}