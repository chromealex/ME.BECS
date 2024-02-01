
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Jobs;
    using Unity.Jobs;
    using static Cuts;

    public struct QuadTreeElement : IComponent {

        public int treeIndex;
        public bool ignoreY;

    }

    public struct QuadTreeAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeElement> quadTreeElementPtr;

        public ref QuadTreeElement quadTreeElement => ref this.quadTreeElementPtr.value.Get(this.ent.id, this.ent.gen);
        public int treeIndex => this.quadTreeElementPtr.value.Read(this.ent.id, this.ent.gen).treeIndex;

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct QuadTreeInsertSystem : IAwake, IUpdate, IDestroy {

        [NativeDisableUnsafePtrRestriction]
        private UnsafeList<System.IntPtr> quadTrees;
        public int treesCount => this.quadTrees.Length;

        [BURST(CompileSynchronously = true)]
        public struct CollectJob : IJobParallelForAspect<QuadTreeAspect, Transforms.TransformAspect> {
            
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<System.IntPtr> quadTrees;

            public void Execute(ref QuadTreeAspect quadTreeAspect, ref ME.BECS.Transforms.TransformAspect tr) {
                
                var tree = (KNN.KnnContainer<Ent>*)this.quadTrees[quadTreeAspect.treeIndex];
                var pos = tr.GetWorldMatrixPosition();
                if (quadTreeAspect.quadTreeElement.ignoreY == true) pos.y = 0f;
                tree->AddPoint(pos, tr.ent);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<System.IntPtr> quadTrees;

            public void Execute(int index) {

                var tree = (KNN.KnnContainer<Ent>*)this.quadTrees[index];
                tree->SetPoints(Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator);
                tree->Rebuild();
                
            }

        }

        public readonly KNN.KnnContainer<Ent>* GetTree(int treeIndex) {

            return (KNN.KnnContainer<Ent>*)this.quadTrees[treeIndex];

        }

        public int AddTree() {

            this.quadTrees.Add((System.IntPtr)_make(new KNN.KnnContainer<Ent>().Initialize(100, Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator)));
            return this.quadTrees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.quadTrees = new UnsafeList<System.IntPtr>(10, Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator);
            
        }

        public void OnUpdate(ref SystemContext context) {

            for (int i = 0; i < this.quadTrees.Length; ++i) {
                var item = (KNN.KnnContainer<Ent>*)this.quadTrees[i];
                item->Clear();
            }
            
            var handle = API.Query(in context).ScheduleParallelFor<CollectJob, QuadTreeAspect, Transforms.TransformAspect>(new CollectJob() {
                quadTrees = this.quadTrees,
            });

            var job = new ApplyJob() {
                quadTrees = this.quadTrees,
            };
            var resultHandle = job.Schedule(this.quadTrees.Length, 1, handle);
            context.SetDependency(resultHandle);

        }

        public void OnDestroy(ref SystemContext context) {

            for (int i = 0; i < this.quadTrees.Length; ++i) {
                var item = (KNN.KnnContainer<Ent>*)this.quadTrees[i];
                item->Dispose();
                _free(item);
            }

            this.quadTrees.Dispose();

        }

    }

}