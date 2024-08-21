
namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Steering behaviour for units.")]
    [RequiredDependencies(typeof(QuadTreeQuerySystem))]
    public struct SteeringSystem : IUpdate, IDrawGizmos {

        public static SteeringSystem Default => new SteeringSystem() {
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
        public float alignmentSpeed;
        public float maxAgentRadius;
        public bool drawGizmos;

        [BURST(CompileSynchronously = true)]
        public unsafe struct Job : IJobParallelForAspect<TransformAspect, UnitAspect> {

            public SteeringSystem system;
            public float dt;
            public World world;
            
            public void Execute(in JobInfo jobInfo, ref TransformAspect tr, ref UnitAspect unit) {

                var facingCone = math.cos(math.radians(120f));
                var collisionDir = float3.zero;
                var avoidanceVector = float3.zero;
                var cohesionVector = float3.zero;
                var separationVector = float3.zero;
                var alignmentVector = float3.zero;
                var cohesionUnitsCount = 0u;
                var alignmentUnitsCount = 0u;
                var query = unit.ent.GetAspect<QuadTreeQueryAspect>();
                var rangeSq = query.query.range * query.query.range;
                var srcPos = tr.position;
                srcPos.y = 0f;
                for (uint i = 0, size = query.results.results.Count; i < size; ++i) {
                    var ent = query.results.results[this.world.state, i];
                    if (ent.IsAlive() == false) continue;
                    if (ent == unit.ent) continue;

                    var entTr = ent.GetAspect<TransformAspect>();
                    var entUnit = ent.GetAspect<UnitAspect>();
                    
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

                    var radiusSum = unit.radius + entUnit.radius;
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
                                unit.collideWithEnd = true;
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
            private static bool IsFacing(float3 rightTransformVector, float3 normal, float cosineValue) {
                return math.dot(rightTransformVector, normal) >= cosineValue;
            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().Schedule<Job, TransformAspect, UnitAspect>(new Job() {
                world = context.world,
                system = this,
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);
            
        }

        public void OnDrawGizmos(ref SystemContext context) {

            if (this.drawGizmos == false) return;
            
            var arr = API.Query(in context).Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().WithAspect<UnitAspect>().WithAspect<TransformAspect>().ToArray();
            foreach (var unitEnt in arr) {

                var tr = unitEnt.GetAspect<TransformAspect>();
                var unit = unitEnt.GetAspect<UnitAspect>();
                UnityEngine.Gizmos.DrawWireSphere(tr.position, unit.readRadius);

            }
            
        }

    }

}