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

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using ME.BECS.Players;
    
    /// <summary>
    /// Provides helper operations for bullet.
    /// </summary>
    public static class BulletUtils {

        /// <summary>
        /// Calculates damage factor.
        /// </summary>
        [INLINE(256)]
        public static tfloat CalculateDamageFactor(tfloat hitRangeSqr, float2 bulletPosition, float2 unitPosition, tfloat unitRadius) {
            var dist = math.distance(bulletPosition, unitPosition) - unitRadius;
            if (dist > 0) {
                dist *= dist;
            }
            return math.clamp(dist / hitRangeSqr, 0, 1);
        }

        /// <summary>
        /// Calculates damage factor.
        /// </summary>
        [INLINE(256)]
        public static tfloat CalculateDamageFactor(tfloat hitRangeSqr, float3 bulletPosition, float3 unitPosition, tfloat unitRadius) {
            var dist = math.distance(bulletPosition, unitPosition) - unitRadius;
            if (dist > 0) {
                dist *= dist;
            }
            return math.clamp(dist / hitRangeSqr, 0, 1);
        }

        /// <summary>
        /// Calculates damage.
        /// </summary>
        [INLINE(256)]
        public static uint CalculateDamage(uint minDamage, uint maxDamage, tfloat hitRangeSqr, float2 bulletPosition, float2 unitPosition, tfloat unitRadius) {
            var damageMin = minDamage;
            var damageMax = maxDamage;
            if (damageMin == damageMax) {
                return damageMax;
            }
            return (uint)math.lerp(damageMax, damageMin, CalculateDamageFactor(hitRangeSqr, bulletPosition, unitPosition, unitRadius));
        }

        /// <summary>
        /// Calculates damage.
        /// </summary>
        [INLINE(256)]
        public static uint CalculateDamage(uint minDamage, uint maxDamage, tfloat hitRangeSqr, float3 bulletPosition, float3 unitPosition, tfloat unitRadius) {
            var damageMin = minDamage;
            var damageMax = maxDamage;
            if (damageMin == damageMax) {
                return damageMax;
            }

            return (uint)math.lerp(damageMax, damageMin, CalculateDamageFactor(hitRangeSqr, bulletPosition, unitPosition, unitRadius));
        }

        /// <summary>
        /// Registers fire point.
        /// </summary>
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

        /// <summary>
        /// Returns next fire point.
        /// </summary>
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

        /// <summary>
        /// Returns fire points.
        /// </summary>
        [INLINE(256)]
        public static ListAuto<Ent> GetFirePoints(in Ent root) {

            return root.Read<FirePointComponent>().points;

        }

    }

}