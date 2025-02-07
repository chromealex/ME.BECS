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

namespace ME.BECS.Units {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Steering behaviour for units.")]
    [RequiredDependencies(typeof(QuadTreeQuerySystem))]
    public struct SteeringSystem : IUpdate, IDrawGizmos {

        public static SteeringSystem Default => new SteeringSystem() {
            calculateSeparation = true,
            calculateCohesion = true,
            calculateAlignment = true,
        };

        public bool calculateSeparation;
        public bool calculateCohesion;
        public bool calculateAlignment;
        public bool drawGizmos;

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<TransformAspect, UnitAspect, QuadTreeQueryAspect> {

            public SteeringSystem system;
            public World world;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit, ref QuadTreeQueryAspect query) {

                var collisionDir = float3.zero;
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
                    var vec = srcPos - targetPos;
                    if (math.all(vec == float3.zero) == true) {
                        vec = unit.randomVector;
                    }
                    var normal = math.normalizesafe(vec);
                    var lengthSqr = math.lengthsq(vec);
                    
                    // check collide with end
                    // if unit collides with another unit which stops
                    // and belongs to the same group
                    var isGroupEquals = entUnit.IsPathFollow == false &&
                                        entUnit.readUnitCommandGroup == unit.readUnitCommandGroup;

                    var radiusSum = (unit.radius + entUnit.radius);
                    var radiusSumSq = (radiusSum * radiusSum);
                    
                    if (isGroupEquals == false && lengthSqr <= rangeSq && unit.IsPathFollow == true) {
                        // check separation
                        if (this.system.calculateSeparation == true) {
                            separationVector += -vec;
                        }

                        // check cohesion
                        if (this.system.calculateCohesion == true) {
                            cohesionVector += targetPos;
                            ++cohesionUnitsCount;
                        }
                        
                        // check alignment
                        if (this.system.calculateAlignment == true) {
                            alignmentVector += entUnit.readVelocity;
                            ++alignmentUnitsCount;
                        }
                    }

                    if (unit.IsPathFollow == false && lengthSqr <= radiusSumSq) {
                        {
                            // move to normal
                            var collision = -normal * (radiusSum - math.sqrt(lengthSqr));
                            collisionDir += collision;
                        }
                        {
                            // set the flag
                            if (isGroupEquals == true) {
                                unit.collideWithEnd = 1;
                            }
                        }
                    }

                }

                unit.componentRuntime.collisionDirection = -math.normalizesafe(collisionDir);
                unit.componentRuntime.separationVector = math.normalizesafe(-separationVector);
                if (cohesionUnitsCount > 0u) {
                    unit.componentRuntime.cohesionVector = math.normalizesafe(cohesionVector / cohesionUnitsCount - srcPos);
                } else {
                    unit.componentRuntime.cohesionVector = float3.zero;
                }
                if (alignmentUnitsCount > 0u) {
                    unit.componentRuntime.alignmentVector = math.normalizesafe(alignmentVector / alignmentUnitsCount);
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