using ME.BECS.Jobs;

namespace ME.BECS.Effects {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;
    
    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Destroy Visual Effects.")]
    public struct SpawnEffectSystem : IUpdate {

        public void OnUpdate(ref SystemContext context) {

            /*var dependsOn = API.Query(in context).ScheduleParallelFor<Job, Transforms.TransformAspect, UnitAspect>(new Job() {
                world = context.world,
                system = this,
                dt = context.deltaTime,
            });
            context.SetDependency(dependsOn);*/
            
        }

    }

}