namespace ME.BECS.Transforms {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using Jobs;
    using ME.BECS.NativeCollections;
    
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST]
    public struct TransformWorldMatrixUpdateSystem : IAwake, IStart, IUpdate, IDestroy {

        private NativeParallelList<Transform3DExt.HierarchyItem> hierarchyStack;
        
        [BURST]
        public struct CalculateLocalMatrixJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateLocalMatrixAndMarkDirty(in aspect);

            }

        }

        [BURST]
        public struct CalculateLocalMatrixStaticJob : IJobForAspects<TransformAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateLocalMatrixAndMarkDirty(in aspect);
                ent.SetTag<IsTransformStaticLocalCalculatedComponent>(true);

            }

        }

        [BURST]
        public struct CalculateHierarchyJob : IJobForAspects<TransformAspect> {

            public NativeParallelList<Transform3DExt.HierarchyItem> hierarchyStack;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                ref var stack = ref this.hierarchyStack.GetThreadList();
                stack.Clear();
                Transform3DExt.CalculateWorldMatrixHierarchy(in aspect, ref stack);

            }

        }

        public void OnAwake(ref SystemContext context) {

            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(context.world.id).Allocator.ToAllocator;
            this.hierarchyStack = new NativeParallelList<Transform3DExt.HierarchyItem>(64, allocator);
            Calculate(ref context);

        }

        public void OnStart(ref SystemContext context) {
            
            Calculate(ref context);

        }

        public void OnUpdate(ref SystemContext context) {

            Calculate(ref context);
            
        }

        public void OnDestroy(ref SystemContext context) {

            this.hierarchyStack.Dispose();

        }

        [INLINE(256)]
        private void Calculate(ref SystemContext context) {

            // Calculate local matrix
            var localMatrixHandle = context.Query().AsParallel().AsUnsafe().Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>().Without<IsTransformStaticLocalComponent>().Schedule<CalculateLocalMatrixJob, TransformAspect>();
            var localMatrixStaticHandle = context.Query().AsParallel().AsUnsafe().Without<IsTransformStaticCalculatedComponent>().Without<IsTransformStaticLocalCalculatedComponent>().With<IsTransformStaticLocalComponent>().Schedule<CalculateLocalMatrixStaticJob, TransformAspect>();
            var hierarchyHandle = context.Query(JobHandle.CombineDependencies(localMatrixHandle, localMatrixStaticHandle))
                                         .AsParallel()
                                         .AsUnsafe()
                                         .Without<ParentComponent>()
                                         .Schedule<CalculateHierarchyJob, TransformAspect>(new CalculateHierarchyJob() {
                                             hierarchyStack = this.hierarchyStack,
                                         });
            context.SetDependency(hierarchyHandle);

        }

    }

}
