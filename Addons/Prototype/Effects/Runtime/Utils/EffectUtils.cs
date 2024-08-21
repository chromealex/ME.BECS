namespace ME.BECS.Effects {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Views;
    using ME.BECS.Transforms;
    using Unity.Mathematics;

    public static class EffectUtils {

        [INLINE(256)]
        public static EffectAspect CreateEffect(float3 position, in EffectConfig effect, JobInfo jobInfo = default) {
            return CreateEffect(position, quaternion.identity, in effect, jobInfo);
        }

        [INLINE(256)]
        public static EffectAspect CreateEffect(float3 position, quaternion rotation, in EffectConfig effect, JobInfo jobInfo = default) {

            var ent = Ent.New(jobInfo);
            effect.config.Apply(ent);
            var tr = ent.GetOrCreateAspect<TransformAspect>();
            tr.position = position;
            tr.rotation = rotation;
            ent.Destroy(effect.lifetime);
            return ent.GetOrCreateAspect<EffectAspect>();

        }

    }

}