#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
#endif

namespace ME.BECS.Bullets {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    
    public static class BulletUtils {

        
        [INLINE(256)]
        public static uint CalculateDamage(uint minDamage, uint maxDamage, tfloat hitRangeSqr, float2 bulletPosition, float2 unitPosition, tfloat unitRadius) {
            var damageMin = minDamage;
            var damageMax = maxDamage;
            if (damageMin == damageMax) {
                return damageMax;
            }

            var dist = math.distance(bulletPosition, unitPosition) - unitRadius;
            if (dist > 0) {
                dist *= dist;
            }
            return (uint)math.lerp(damageMax, damageMin, math.clamp(dist / hitRangeSqr, 0, 1));
        }

        [INLINE(256)]
        public static uint CalculateDamage(uint minDamage, uint maxDamage, tfloat hitRangeSqr, float3 bulletPosition, float3 unitPosition, tfloat unitRadius) {
            var damageMin = minDamage;
            var damageMax = maxDamage;
            if (damageMin == damageMax) {
                return damageMax;
            }

            var dist = math.distance(bulletPosition, unitPosition) - unitRadius;
            if (dist > 0) {
                dist *= dist;
            }
            return (uint)math.lerp(damageMax, damageMin, math.clamp(dist / hitRangeSqr, 0, 1));
        }

        [INLINE(256)]
        public static Ent RegisterFirePoint(in Ent root, in float3 position, in quaternion rotation, in JobInfo jobInfo) {

            var point = Ent.New<FirePointEntityType>(in jobInfo, editorName: "FirePoint");
            var tr = point.Set<TransformAspect>();
            tr.IsStaticLocal = true;
            point.SetParent(in root);
            tr.localPosition = position;
            tr.localRotation = rotation;

            ref var firePoints = ref root.Get<FirePointComponent>();
            if (firePoints.points.IsCreated == false) firePoints.points = new ListAuto<Ent>(in point, 1u);
            firePoints.points.Add(point);
            return point;

        }

        [INLINE(256)]
        public static Ent GetNextFirePoint(in Ent root) {
            
            ref var point = ref root.Get<FirePointComponent>();
            var points = point.points;
            if (point.index >= points.Count) {
                point.index = 0u;
            }
            if (point.index >= points.Count) return default;
            return points[point.index++];

        }

        [INLINE(256)]
        public static ListAuto<Ent> GetFirePoints(in Ent root) {

            return root.Read<FirePointComponent>().points;

        }

    }

}