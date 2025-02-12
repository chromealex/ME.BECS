namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST(CompileSynchronously = true)]
    public struct TransformWorldMatrixUpdateSystem : IAwake, IUpdate {
        
        [BURST(CompileSynchronously = true)]
        public struct CalculateLocalMatrixJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateLocalMatrix(in aspect);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct CalculateRootsJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateWorldMatrix(in aspect);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct CalculateRootsWithChildrenJob : IJobFor1Aspects2Components<TransformAspect, ParentComponent, IsFirstLevelComponent> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect, ref ParentComponent parent, ref IsFirstLevelComponent isFirstLevelComponent) {

                Transform3DExt.CalculateWorldMatrixHierarchy(parent.value, in aspect);

            }

        }

        public void OnAwake(ref SystemContext context) {

            Calculate(ref context);

        }

        public void OnUpdate(ref SystemContext context) {

            Calculate(ref context);
            
        }

        [INLINE(256)]
        private static void Calculate(ref SystemContext context) {
            
            // Calculate local matrix
            var localMatrixHandle = context.Query().AsParallel().Schedule<CalculateLocalMatrixJob, TransformAspect>();
            // Update roots
            var rootsHandle = context.Query(localMatrixHandle).AsParallel().Without<ParentComponent>().Schedule<CalculateRootsJob, TransformAspect>();
            // Update children with roots
            var rootsWithChildrenHandle = context.Query(rootsHandle).AsParallel().Schedule<CalculateRootsWithChildrenJob, TransformAspect, ParentComponent, IsFirstLevelComponent>();
            context.SetDependency(rootsWithChildrenHandle);

        }

    }

}