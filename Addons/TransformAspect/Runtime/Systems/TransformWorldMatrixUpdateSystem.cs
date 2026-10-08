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
    
    /// <summary>
    /// Coordinates transform world matrix update during the ECS system lifecycle.
    /// </summary>
    [UnityEngine.Tooltip("Update all entities with TransformAspect (LocalPosition and LocalRotation components are required).")]
    [BURST]
    public partial struct TransformWorldMatrixUpdateSystem : IAwake, IStart, IUpdate, IDestroy {

        private NativeParallelList<Transform3DExt.HierarchyItem> hierarchyStack;
        
        /// <summary>
        /// Executes calculate local matrix work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CalculateLocalMatrixJob : IJobForAspects<TransformAspect> {

            /// <summary>
            /// Processes calculate local matrix using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateLocalMatrixAndMarkDirty(in aspect);

            }

        }

        /// <summary>
        /// Executes calculate local matrix static work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CalculateLocalMatrixStaticJob : IJobForAspects<TransformAspect> {

            /// <summary>
            /// Processes calculate local matrix static using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                Transform3DExt.CalculateLocalMatrixAndMarkDirty(in aspect);
                ent.SetTag<IsTransformStaticLocalCalculatedComponent>(true);

            }

        }

        /// <summary>
        /// Executes calculate hierarchy work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CalculateHierarchyJob : IJobForAspects<TransformAspect> {

            /// <summary>
            /// Hierarchy stack used by <c>TransformWorldMatrixUpdateSystem.CalculateHierarchyJob</c>.
            /// </summary>
            public NativeParallelList<Transform3DExt.HierarchyItem> hierarchyStack;

            /// <summary>
            /// Processes calculate hierarchy using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect aspect) {

                ref var stack = ref this.hierarchyStack.GetThreadList();
                stack.Clear();
                Transform3DExt.CalculateWorldMatrixHierarchy(in aspect, ref stack);

            }

        }

        /// <summary>
        /// Initializes transform world matrix update system state from the supplied context.
        /// </summary>
        public void OnAwake(ref SystemContext context) {

            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(context.world.id).Allocator.ToAllocator;
            this.hierarchyStack = new NativeParallelList<Transform3DExt.HierarchyItem>(64, allocator);
            Calculate(ref context);

        }

        /// <summary>
        /// Starts transform world matrix update system processing for the supplied context.
        /// </summary>
        public void OnStart(ref SystemContext context) {
            
            Calculate(ref context);

        }

        /// <summary>
        /// Updates transform world matrix update system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            Calculate(ref context);
            
        }

        /// <summary>
        /// Releases transform world matrix update system state at the end of its owning lifecycle.
        /// </summary>
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
