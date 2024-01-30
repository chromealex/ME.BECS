namespace ME.BECS.Transforms {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST(CompileSynchronously = true)]
    public unsafe struct TransformWorldMatrixUpdateSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
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