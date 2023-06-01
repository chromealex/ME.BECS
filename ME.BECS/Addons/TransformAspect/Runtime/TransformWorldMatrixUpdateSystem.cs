namespace ME.BECS.TransformAspect {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    public unsafe struct TransformWorldMatrixUpdateSystem : IUpdate {
        
        [BURST]
        private struct CalculateRootsJob : IJobParallelForAspect<TransformAspect> {

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