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

namespace ME.BECS.Effects {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;
    using ME.BECS.Players;

    public static class EffectUtils {

        [INLINE(256)]
        public static EffectAspect CreateEffect(in JobInfo jobInfo, in float3 position, in EffectConfig effect) {
            return CreateEffect(in jobInfo, in position, quaternion.identity, in effect);
        }

        [INLINE(256)]
        public static EffectAspect CreateEffect(in JobInfo jobInfo, in float3 position, in EffectConfig effect, in PlayerAspect owner) {
            return CreateEffect(in jobInfo, in position, quaternion.identity, in effect, owner);
        }

        [INLINE(256)]
        public static EffectAspect CreateEffect(in JobInfo jobInfo, in float3 position, in quaternion rotation, in EffectConfig effect, in PlayerAspect owner = default) {

            if (effect.config.IsValid == false) return default;
            
            var ent = Ent.New(in jobInfo);
            effect.config.Apply(ent);
            if (owner.ent != default) {
                ME.BECS.Players.PlayerUtils.SetOwner(in ent, in owner);
            }
            var tr = ent.GetOrCreateAspect<TransformAspect>();
            tr.position = position;
            tr.rotation = rotation;
            ent.Destroy(effect.lifetime);
            return ent.GetOrCreateAspect<EffectAspect>();

        }

    }

}