namespace ME.BECS.Effects {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    public static class EffectUtils {

        [INLINE(256)]
        public static EffectAspect CreateEffect(float3 position, in EffectConfig effect) {
            return CreateEffect(position, quaternion.identity, in effect);
        }

        [INLINE(256)]
        public static EffectAspect CreateEffect(float3 position, quaternion rotation, in EffectConfig effect) {

            var ent = Ent.New();
            effect.config.Apply(ent);
            ent.InstantiateView(effect.view);
            var tr = ent.GetOrCreateAspect<TransformAspect>();
            tr.position = position;
            tr.rotation = rotation;
            ent.Destroy(effect.lifetime);
            return ent.GetOrCreateAspect<EffectAspect>();

        }

    }

}