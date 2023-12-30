namespace ME.BECS.TransformAspect {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST]
    public unsafe struct TransformWorldMatrixUpdateSystem : IUpdate {
        
        [BURST]
        public struct CalculateRootsJob : IJobParallelForAspect<TransformAspect> {

            public State* state;
            
            public void Execute(ref TransformAspect aspect) {

                Transform3DExt.CalculateMatrixHierarchy(this.state, ref aspect);
                
            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            // update roots
            var childHandle = API.Query(in context).ScheduleParallelFor<CalculateRootsJob, TransformAspect>(new CalculateRootsJob() {
                state = context.world.state,
            });
            context.SetDependency(childHandle);
            
        }

    }

}