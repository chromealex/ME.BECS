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

namespace ME.BECS {

    public static class EntExt {

        private static void CleanUpDestroyComponents(in Ent ent) {
            
            ent.Remove<DestroyWithLifetime>();
            ent.Remove<DestroyWithLifetimeMs>();
            ent.Remove<DestroyWithTicks>();
            
        }

        public static void Destroy(this in Ent ent, uint ms) {

            CleanUpDestroyComponents(in ent);
            ent.Set(new DestroyWithLifetimeMs() { lifetime = ms, });
            
        }

        public static void Destroy(this in Ent ent, tfloat lifetime) {

            CleanUpDestroyComponents(in ent);
            ent.Set(new DestroyWithLifetime() { lifetime = lifetime, });
            
        }

        public static void DestroyEndTick(this in Ent ent) {

            CleanUpDestroyComponents(in ent);
            ent.Set(new DestroyWithTicks() { ticks = 0UL, });
            
        }

        public static void Destroy(this in Ent ent, ulong ticks) {

            CleanUpDestroyComponents(in ent);
            ent.Set(new DestroyWithTicks() { ticks = ticks, });

        }

    }

}