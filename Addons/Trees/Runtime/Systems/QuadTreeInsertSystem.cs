
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using Unity.Mathematics;
    using static Cuts;

    public struct QuadTreeElement : IComponent {

        public int treeIndex;
        public float radius;
        public bool ignoreY;

    }

    [EditorComment("Used by QuadTreeInsertSystem to filter entities by treeIndex")]
    public struct QuadTreeAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeElement> quadTreeElementPtr;

        public ref QuadTreeElement quadTreeElement => ref this.quadTreeElementPtr.value.Get(this.ent.id, this.ent.gen);
        public int treeIndex => this.quadTreeElementPtr.value.Read(this.ent.id, this.ent.gen).treeIndex;

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct QuadTreeInsertSystem : IAwake, IUpdate, IDestroy {

        public float3 mapPosition;
        public float3 mapSize;
        
        [NativeDisableUnsafePtrRestriction]
        private UnsafeList<System.IntPtr> quadTrees;
        public int treesCount => this.quadTrees.Length;

        [BURST(CompileSynchronously = true)]
        public struct CollectJob : IJobParallelForAspect<QuadTreeAspect, Transforms.TransformAspect> {
            
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<System.IntPtr> quadTrees;

            public void Execute(in JobInfo jobInfo, ref QuadTreeAspect quadTreeAspect, ref ME.BECS.Transforms.TransformAspect tr) {
                
                var tree = (NativeTrees.NativeOctree<Ent>*)this.quadTrees[quadTreeAspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition();
                if (quadTreeAspect.quadTreeElement.ignoreY == true) pos.y = 0f;
                tree->Add(tr.ent, new NativeTrees.AABB(pos - quadTreeAspect.quadTreeElement.radius, pos + quadTreeAspect.quadTreeElement.radius));
                //tree->AddPoint(pos, tr.ent, quadTreeAspect.quadTreeElement.radius);
                /*tree->lockSpinner.Lock();
                tree->Insert(tr.ent, new NativeTrees.AABB(pos - quadTreeAspect.quadTreeElement.radius, pos + quadTreeAspect.quadTreeElement.radius));
                tree->lockSpinner.Unlock();*/
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<System.IntPtr> quadTrees;

            public void Execute(int index) {

                var tree = (NativeTrees.NativeOctree<Ent>*)this.quadTrees[index];
                //tree->SetPoints(Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator);
                //tree->Rebuild();
                tree->Rebuild();
                
            }

        }
        
        [BURST(CompileSynchronously = true)]
        public struct ClearJob : Unity.Jobs.IJobParallelFor {

            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<System.IntPtr> quadTrees;

            public void Execute(int index) {

                var item = (NativeTrees.NativeOctree<Ent>*)this.quadTrees[index];
                item->Clear();
                
            }

        }

        public readonly NativeTrees.NativeOctree<Ent>* GetTree(int treeIndex) {

            return (NativeTrees.NativeOctree<Ent>*)this.quadTrees[treeIndex];

        }

        public int AddTree() {

            var size = new NativeTrees.AABB(this.mapPosition, this.mapPosition + this.mapSize);
            this.quadTrees.Add((System.IntPtr)_make(new NativeTrees.NativeOctree<Ent>(size, Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator)));
            return this.quadTrees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.quadTrees = new UnsafeList<System.IntPtr>(10, Constants.ALLOCATOR_PERSISTENT_ST.ToAllocator);
            
        }

        public void OnUpdate(ref SystemContext context) {

            var clearJob = new ClearJob() {
                quadTrees = this.quadTrees,
            };
            var clearJobHandle = clearJob.Schedule(this.quadTrees.Length, 1, context.dependsOn);
            
            var handle = API.Query(in context, clearJobHandle).Schedule<CollectJob, QuadTreeAspect, Transforms.TransformAspect>(new CollectJob() {
                quadTrees = this.quadTrees,
            });

            /*var dependencies = new Unity.Collections.NativeArray<JobHandle>(this.quadTrees.Length, Unity.Collections.Allocator.Temp);
            for (var i = 0; i < this.quadTrees.Length; ++i) {
                var tree = this.quadTrees[i];
                dependencies[i] = GridSearch<Ent>.Rebuild((GridSearch<Ent>*)tree, handle);
            }
            var resultHandle = JobHandle.CombineDependencies(dependencies);*/

            var job = new ApplyJob() {
                quadTrees = this.quadTrees,
            };
            var resultHandle = job.Schedule(this.quadTrees.Length, 1, handle);
            //var resultHandle = handle;
            context.SetDependency(resultHandle);

        }

        public void OnDestroy(ref SystemContext context) {

            for (int i = 0; i < this.quadTrees.Length; ++i) {
                var item = (NativeTrees.NativeOctree<Ent>*)this.quadTrees[i];
                item->Dispose();
                _free(item);
            }

            this.quadTrees.Dispose();

        }

        public Ent GetNearestFirst(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, float minRangeSqr = default,
                                      float rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default) {
            return this.GetNearestFirst(mask, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreY, new AlwaysTrueSubFilter());
        }

        public Ent GetNearestFirst<T>(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, float minRangeSqr = default, float rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default, T subFilter = default) where T : struct, ISubFilter<Ent> {

            const uint nearestCount = 1u;
            var heap = new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Unity.Collections.Allocator.Temp);
            // for each tree
            for (int i = 0; i < this.treesCount; ++i) {
                if ((mask & (1 << i)) == 0) {
                    continue;
                }
                ref var tree = ref *this.GetTree(i);
                {
                    var visitor = new OctreeNearestAABBVisitor<Ent, T>() {
                        subFilter = subFilter,
                        sector = sector,
                        ignoreSelf = ignoreSelf,
                        ignore = selfEnt,
                    };
                    tree.Nearest(worldPos, minRangeSqr, rangeSqr, ref visitor, new AABBDistanceSquaredProvider<Ent>() { ignoreY = ignoreY });
                    if (visitor.found == true) heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(visitor.nearest, math.lengthsq(worldPos - visitor.nearest.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                }
            }
            
            {
                var max = math.min(nearestCount, heap.Count);
                for (uint i = 0; i < max; ++i) {
                    return heap[heap.Pop()].Position;
                }
            }

            return default;

        }

        public void GetNearest(int mask, uint nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, float minRangeSqr, float rangeSqr, bool ignoreSelf, bool ignoreY) {
            this.GetNearest(mask, nearestCount, ref results, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreY, new AlwaysTrueSubFilter());
        }

        public void GetNearest<T>(int mask, uint nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, float minRangeSqr, float rangeSqr, bool ignoreSelf, bool ignoreY, T subFilter = default) where T : struct, ISubFilter<Ent> {

            if (nearestCount >= 1u) {

                var heap = new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Unity.Collections.Allocator.Temp);
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i);
                    {
                        var visitor = new OctreeKNearestAABBVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = new UnsafeHashSet<Ent>((int)nearestCount, Unity.Collections.Allocator.Temp),
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        tree.Nearest(worldPos, minRangeSqr, rangeSqr, ref visitor, new AABBDistanceSquaredProvider<Ent>() { ignoreY = ignoreY });
                        foreach (var item in visitor.results) {
                            heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                        }
                    }
                }

                {
                    var max = math.min(nearestCount, heap.Count);
                    for (uint i = 0; i < max; ++i) {
                        results.Add(heap[heap.Pop()].Position);
                    }
                }

            } else {
                
                // select all units
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i);
                    {
                        var visitor = new RangeAABBUniqueVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = new UnsafeHashSet<Ent>((int)nearestCount, Unity.Collections.Allocator.Temp),
                            rangeSqr = rangeSqr,
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        var range = math.sqrt(rangeSqr);
                        tree.Range(new NativeTrees.AABB(worldPos - range, worldPos + range), ref visitor);
                        results.AddRange(visitor.results.ToNativeArray(Unity.Collections.Allocator.Temp));
                    }
                }

            }

        }

    }

}