
using ME.BECS.Transforms;
using ME.BECS.Views;
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

    public enum DestroyBehaviour : byte {
        UseSource = 0,
        CreateVisualCopy,
    }
    
    public static class EntExt {

        private static void CleanUpDestroyComponents(in Ent ent) {
            
            ent.Remove<DestroyWithLifetime>();
            ent.Remove<DestroyWithLifetimeMs>();
            ent.Remove<DestroyWithTicks>();
            
        }

        public static bool HasDestroyLifetime(this Ent ent) {
            return ent.Has<DestroyWithLifetime>() == true ||
                   ent.Has<DestroyWithLifetimeMs>() == true ||
                   ent.Has<DestroyWithTicks>() == true;
        }

        public static bool HasDestroyLifetime(this EntRO ent) {
            return ent.Has<DestroyWithLifetime>() == true ||
                   ent.Has<DestroyWithLifetimeMs>() == true ||
                   ent.Has<DestroyWithTicks>() == true;
        }

        public static void DestroyWithLifetime(this in Ent ent, DestroyBehaviour destroyBehaviour = DestroyBehaviour.UseSource) {

            if (ent.TryRead(out DestroyWithLifetimeConfigMs destroyWithLifetime) == true) {
                if (destroyBehaviour == DestroyBehaviour.UseSource) {
                    ent.Destroy(destroyWithLifetime.lifetime);
                } else {
                    var srcTr = ent.GetAspect<TransformAspect>();
                    var copy = Ent.New<DestroyWithLifetimeEntityType>(JobInfo.Create(ent.worldId));
                    var tr = copy.Set<TransformAspect>();
                    tr.position = srcTr.position;
                    tr.rotation = srcTr.rotation;
                    copy.AssignView(ent);
                    copy.Destroy(destroyWithLifetime.lifetime);
                    ent.DestroyHierarchy();
                }
            } else {
                ent.DestroyHierarchy();
            }
            
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