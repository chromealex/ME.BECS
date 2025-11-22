using Unity.Profiling;

namespace ME.BECS.Transforms {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using Jobs;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST]
    public struct TransformWorldMatrixUpdateSystem : IAwake, IStart, IUpdate {
        
        [BURST]
        public struct CalculateLocalMatrixJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                var marker = new ProfilerMarker("CalculateLocalMatrix");
                marker.Begin();
                Transform3DExt.CalculateLocalMatrix(in aspect);
                marker.End();
                if (aspect.IsStaticLocal == true) ent.SetTag<IsTransformStaticLocalCalculatedComponent>(true);

            }

        }

        [BURST]
        public struct CalculateRootsJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateWorldMatrix(in aspect);
                if (aspect.IsStatic == true) ent.SetTag<IsTransformStaticCalculatedComponent>(true);

            }

        }

        [BURST]
        public struct ClearJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.Clear(in aspect);

            }

        }

        [BURST]
        public struct CalculateJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateWorldMatrixParent(aspect.parent, in aspect);

            }

        }

        [BURST]
        public struct CalculateLevelJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateWorldMatrixLevel(aspect.parent, in aspect);

            }

        }

        [BURST]
        public struct CalculateLevelStaticJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateWorldMatrixLevelStatic(aspect.parent, in aspect);

            }

        }

        public void OnAwake(ref SystemContext context) {
            
            Calculate(ref context);

        }

        public void OnStart(ref SystemContext context) {
            
            Calculate(ref context);

        }

        public void OnUpdate(ref SystemContext context) {

            Calculate(ref context);
            
        }

        [INLINE(256)]
        private static void Calculate(ref SystemContext context) {

            var clearCurrenTick = context.Query().AsParallel().Without<IsTransformStaticCalculatedComponent>().With<ParentComponent>().Schedule<ClearJob, TransformAspect>();
            // Calculate local matrix
            var localMatrixHandle = context.Query().AsParallel().Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>().Schedule<CalculateLocalMatrixJob, TransformAspect>();
            // Update roots
            var rootsHandle = context.Query(JobHandle.CombineDependencies(localMatrixHandle, clearCurrenTick)).AsParallel().Without<IsTransformStaticCalculatedComponent>().Without<ParentComponent>().Schedule<CalculateRootsJob, TransformAspect>();
            
            var level1 = context.Query(rootsHandle).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel1>().With<ParentComponent>().Without<IsTransformStaticComponent>().Schedule<CalculateLevelJob, TransformAspect>();
            var level1Static = context.Query(rootsHandle).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel1>().With<ParentComponent>().With<IsTransformStaticComponent>().Schedule<CalculateLevelStaticJob, TransformAspect>();
            
            var level1Dep = JobHandle.CombineDependencies(level1, level1Static);
            var level2 = context.Query(level1Dep).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel2>().With<ParentComponent>().Without<IsTransformStaticComponent>().Schedule<CalculateLevelJob, TransformAspect>();
            var level2Static = context.Query(level1Dep).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel2>().With<ParentComponent>().With<IsTransformStaticComponent>().Schedule<CalculateLevelStaticJob, TransformAspect>();
            
            var level2Dep = JobHandle.CombineDependencies(level2, level2Static);
            var level3 = context.Query(level2Dep).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel3>().With<ParentComponent>().Without<IsTransformStaticComponent>().Schedule<CalculateLevelJob, TransformAspect>();
            var level3Static = context.Query(level2Dep).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevel3>().With<ParentComponent>().With<IsTransformStaticComponent>().Schedule<CalculateLevelStaticJob, TransformAspect>();
            
            var level3Dep = JobHandle.CombineDependencies(level3, level3Static);
            var rootsWithChildrenHandle = context.Query(level3Dep).AsParallel().Without<IsTransformStaticCalculatedComponent>().With<TransformLevelOther>().With<ParentComponent>().Schedule<CalculateJob, TransformAspect>();
            // Update children with roots
            context.SetDependency(rootsWithChildrenHandle);

        }

    }

}