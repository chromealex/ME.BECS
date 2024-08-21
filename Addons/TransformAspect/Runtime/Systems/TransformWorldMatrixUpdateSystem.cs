namespace ME.BECS.Transforms {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Unity.Jobs;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST(CompileSynchronously = true)]
    public struct TransformWorldMatrixUpdateSystem : IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct CalculateRootsJob : IJobParallelForAspect<TransformAspect> {

            public void Execute(in JobInfo jobInfo, ref TransformAspect aspect) {

                Transform3DExt.CalculateMatrix(in aspect);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct CalculateRootsWithChildrenJob : IJobParallelForAspect<TransformAspect> {

            public void Execute(in JobInfo jobInfo, ref TransformAspect aspect) {

                Transform3DExt.CalculateMatrixHierarchy(aspect.parent, in aspect);

            }

        }

        public void OnUpdate(ref SystemContext context) {
            
            // update roots
            var rootsHandle = API.Query(in context).Without<ParentComponent>().Schedule<CalculateRootsJob, TransformAspect>();
            // update children with roots
            var rootsWithChildrenHandle = API.Query(in context, rootsHandle).With<ParentComponent>().With<IsFirstLevelComponent>().Schedule<CalculateRootsWithChildrenJob, TransformAspect>();
            context.SetDependency(rootsWithChildrenHandle);
            
        }

    }

}