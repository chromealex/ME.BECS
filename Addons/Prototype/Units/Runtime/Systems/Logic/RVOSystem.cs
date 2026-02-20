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

    [BURST]
    [UnityEngine.Tooltip("RVO behaviour for units.")]
    public struct RVOSystem : IUpdate, IDrawGizmos {

        public static RVOSystem Default => new RVOSystem() {
            brakeStrength = 2f,
            minSpeedFactor = 0.3f,
            timeHorizon = 1.5f,
            avoidanceWeight = 1.5f,
            velocitySpeed = 5f,
        };

        public tfloat timeHorizon;
        public tfloat avoidanceWeight;
        public tfloat velocitySpeed;
        
        public tfloat brakeStrength;
        public tfloat minSpeedFactor;

        public bool drawGizmos;

        [BURST]
        public struct Job : IJobForAspects<TransformAspect, UnitAspect, QuadTreeQueryAspect> {

            public InjectSystem<RVOSystem> system;
            [InjectDeltaTime]
            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit, ref QuadTreeQueryAspect query) {

                this.system.Value.Resolve(in tr, in unit, query.readResults.results, this.dt);

            }
            
        }

        [BURST]
        public struct SpatialJob : IJobForAspects<TransformAspect, UnitAspect, SpatialQueryAspect> {

            public InjectSystem<RVOSystem> system;
            [InjectDeltaTime]
            public tfloat dt;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit, ref SpatialQueryAspect query) {

                this.system.Value.Resolve(in tr, in unit, query.readResults.results, this.dt);
                
            }
            
        }

        [BURST]
        public struct ApplyCollisionJob : IJobForAspects<TransformAspect, UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit) {

                tr.position += unit.readComponentRuntime.collisionDirection;
                if (unit.readComponentRuntime.collideWithEnd == true) unit.IsCollideWithEnd = true;
                unit.componentRuntime.collisionDirection = default;

            }
            
        }

        private void Resolve(in TransformAspect tr, in UnitAspect unit, ListAuto<Ent> list, tfloat dt) {

            float3 newVelocity;
            unit.componentRuntime.collideWithEnd = false;
            var collideWithEnd = this.ResolveOverlap(in tr, in unit, list, dt);
            if (collideWithEnd == true || unit.IsPathFollow == false) {
                newVelocity = float3.zero;
                unit.componentRuntime.collideWithEnd = collideWithEnd;
            } else {
                var desiredVelocity = math.normalizesafe(unit.readVelocity) * unit.maxSpeed;
                newVelocity = this.ComputeRVO(in tr, in unit, list, desiredVelocity, out tfloat danger);
                var speedFactor = math.lerp(1f, this.minSpeedFactor, danger * this.brakeStrength);
                newVelocity *= speedFactor;
            }

            unit.velocity = math.lerp(unit.readVelocity, newVelocity, dt * this.velocitySpeed);
            unit.velocity.y = 0;
            unit.speed = math.length(unit.readVelocity);
            
            unit.componentRuntime.desiredDirection = unit.velocity * dt;

            unit.componentRuntime.alignmentVector = unit.readComponentRuntime.desiredDirection;
            if (math.lengthsq(unit.readVelocity) > 0.01f) {
                unit.componentRuntime.alignmentVector = math.normalizesafe(unit.readVelocity);
            }

        }

        private bool ResolveOverlap(in TransformAspect tr, in UnitAspect currentUnit, ListAuto<Ent> list, tfloat dt) {
            var collideWithEnd = false;
            foreach (var unitEnt in list) {
                var unit = unitEnt.GetAspect<UnitAspect>();
                var unitTr = unitEnt.GetAspect<TransformAspect>();
                var diff = tr.position - unitTr.position;
                diff.y = 0;

                var dist = math.length(diff);
                var minDist = currentUnit.readRadius + unit.readRadius;
                if (dist < minDist && dist > 0.0001f) {
                    var penetration = minDist - dist;
                    var pushDir = math.normalizesafe(diff);

                    var delta = pushDir * (penetration * 0.5f);
                    currentUnit.componentRuntime.collisionDirection += delta;
                    unit.componentRuntime.collisionDirection -= delta;
                    if (collideWithEnd == false && unit.readUnitCommandGroup == currentUnit.readUnitCommandGroup) {
                        collideWithEnd = unit.IsCollideWithEnd;
                    }
                }
            }
            return collideWithEnd;
        }
        
        private float3 ComputeRVO(in TransformAspect tr, in UnitAspect currentUnit, ListAuto<Ent> list, float3 desiredVelocity, out tfloat danger) {
            
            var avoidance = float3.zero;
            var count = 0u;
            danger = 0f;

            foreach (var unitEnt in list) {
                
                var unit = unitEnt.GetAspect<UnitAspect>();
                var unitTr = unitEnt.GetAspect<TransformAspect>();
                
                var relVel = unit.readVelocity - currentUnit.readVelocity;
                relVel.y = 0;

                var relSpeedSqr = math.lengthsq(relVel);
                if (relSpeedSqr < 0.0001f) continue;

                var relPos = unitTr.position - tr.position;
                relPos.y = 0;

                var combinedRadius = currentUnit.readRadius + unit.readRadius;

                var t = math.dot(-relPos, relVel) / relSpeedSqr;
                if (t > 0 && t < this.timeHorizon) {
                    var closestPoint = relPos + relVel * t;
                    var closestDist = math.length(closestPoint);
                    if (closestDist < combinedRadius) {
                        var sideStep = math.normalizesafe(math.cross(new float3(0f, 1f, 0f), relPos));
                        avoidance += sideStep;
                        ++count;
                        var d = 1f - (closestDist / combinedRadius);
                        danger = math.max(danger, d);
                    }
                }
            }

            if (count == 0u) return desiredVelocity;

            var finalVel = desiredVelocity + math.normalizesafe(avoidance) * this.avoidanceWeight * currentUnit.readMaxSpeed;
            finalVel.y = 0;

            return math.normalizesafe(finalVel) * currentUnit.readMaxSpeed;
            
        }
        
        public void OnUpdate(ref SystemContext context) {

            var dependsOnQt = context.Query().AsParallel().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().Schedule<Job, TransformAspect, UnitAspect, QuadTreeQueryAspect>();
            var dependsOnSpatial = context.Query().AsParallel().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().Schedule<SpatialJob, TransformAspect, UnitAspect, SpatialQueryAspect>();
            var dependsOn = Unity.Jobs.JobHandle.CombineDependencies(dependsOnQt, dependsOnSpatial);
            dependsOn = context.Query(dependsOn).AsParallel().Schedule<ApplyCollisionJob, TransformAspect, UnitAspect>();
            context.SetDependency(dependsOn);
            
        }

        public void OnDrawGizmos(ref SystemContext context) {

            if (this.drawGizmos == false) return;
            
            var arr = context.Query().AsParallel().Without<IsUnitStaticComponent>().Without<UnitHoldComponent>().WithAspect<UnitAspect>().WithAspect<TransformAspect>().ToArrayOnDemand();
            foreach (var unitEnt in arr) {

                var tr = unitEnt.GetAspect<TransformAspect>();
                var unit = unitEnt.GetAspect<UnitAspect>();
                UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)tr.position, (float)unit.readRadius);

            }
            arr.Dispose();
            
        }

    }

}