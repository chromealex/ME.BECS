#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using ME.BECS.Jobs;
    using System.Runtime.InteropServices;
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using NativeTrees;
    using static Cuts;

    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    [StructLayout(LayoutKind.Explicit)]
    public struct QuadTreeElement : IComponent {

        [FieldOffset(0)]
        public tfloat radius;
        [FieldOffset(0)]
        public tfloat sizeX;
        [FieldOffset(4)]
        public int treeIndex;
        [FieldOffset(8)]
        public byte ignoreY;

    }

    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeElementRect : IComponent {

        public tfloat sizeY;
        
    }

    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeHeightComponent : IComponent {
        
        public tfloat height;
        
    }

    [EditorComment("Used by QuadTreeInsertSystem to filter entities by treeIndex")]
    public struct QuadTreeAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeElement> quadTreeElementPtr;
        public AspectDataPtr<QuadTreeElementRect> quadTreeRectPtr;
        public AspectDataPtr<QuadTreeHeightComponent> quadTreeHeightPtr;

        public readonly ref QuadTreeElement quadTreeElement => ref this.quadTreeElementPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly QuadTreeElement readQuadTreeElement => ref this.quadTreeElementPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref int treeIndex => ref this.quadTreeElement.treeIndex;
        public readonly ref readonly int readTreeIndex => ref this.readQuadTreeElement.treeIndex;
        public readonly bool isRect => this.ent.Has<QuadTreeElementRect>();
        public readonly bool hasHeight => this.ent.Has<QuadTreeHeightComponent>();
        public readonly float2 rectSize => new float2(this.readQuadTreeElement.sizeX, this.quadTreeRectPtr.Read(this.ent.id, this.ent.gen).sizeY);
        public readonly tfloat height => this.quadTreeHeightPtr.Read(this.ent.id, this.ent.gen).height;

        public readonly void SetHeight(tfloat height) {
            this.ent.Set(new QuadTreeHeightComponent() {
                height = height,
            });
        }
        
        public readonly void SetAsRectWithSize(tfloat sizeX, tfloat sizeY) {
            ref var rect = ref this.quadTreeRectPtr.Get(this.ent.id, this.ent.gen);
            rect.sizeY = sizeY;
            this.quadTreeElement.sizeX = sizeX;
        }

    }
    
    [BURST]
    public unsafe struct QuadTreeInsertSystem : IAwake, IUpdate, IDestroy, IDrawGizmos {
        
        public static QuadTreeInsertSystem Default => new QuadTreeInsertSystem() {
            mapSize = new float2(200f, 200f),
        };

        public float2 mapPosition;
        public float2 mapSize;
        
        private UnsafeList<safe_ptr> trees;
        public readonly uint treesCount => (uint)this.trees.Length;
        private ushort worldId;

        [BURST]
        public struct CollectRectJob : IJobForAspects<QuadTreeAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref QuadTreeAspect quadTreeAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[quadTreeAspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var size = quadTreeAspect.rectSize;
                var halfSize = new float2(size.x * 0.5f, size.y * 0.5f);
                tree.ptr->Add(tr.ent, new NativeTrees.AABB2D(pos - halfSize, pos + new float2(halfSize.x, halfSize.y)));
                
            }

        }

        [BURST]
        public struct CollectJob : IJobForAspects<QuadTreeAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref QuadTreeAspect quadTreeAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[quadTreeAspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var radius = quadTreeAspect.readQuadTreeElement.radius;

                tree.ptr->Add(tr.ent, new NativeTrees.AABB2D(pos - quadTreeAspect.readQuadTreeElement.radius, pos + new float2(radius, radius)));
                
            }

        }

        [BURST]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;
            
            public void Execute(int index) {

                var tree = (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[index];
                tree.ptr->Rebuild();
                
            }

        }
        
        [BURST]
        public struct ClearJob : Unity.Jobs.IJobParallelFor {

            public QuadTreeInsertSystem system;
            public UnsafeList<safe_ptr> trees;

            public void Execute(int index) {

                var size = new NativeTrees.AABB2D(this.system.mapPosition, this.system.mapPosition + this.system.mapSize);
                var item = (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[index];
                item.ptr->SetBounds(size);
                item.ptr->Clear();
                
            }

        }
        
        [INLINE(256)]
        public readonly safe_ptr<NativeTrees.NativeQuadtree<Ent>> GetTree(int treeIndex) {

            return (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[treeIndex];

        }

        [INLINE(256)]
        public int AddTree() {

            var size = new NativeTrees.AABB2D(this.mapPosition, this.mapPosition + this.mapSize);
            this.trees.Add((safe_ptr)_make(new NativeTrees.NativeQuadtree<Ent>(size, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator)));
            return this.trees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.worldId = context.world.id;
            this.trees = new UnsafeList<safe_ptr>(10, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator);
            
        }

        public void OnUpdate(ref SystemContext context) {

            var clearJob = new ClearJob() {
                trees = this.trees,
                system = this,
            };
            var clearJobHandle = clearJob.Schedule(this.trees.Length, 1, context.dependsOn);
            
            var handle = context.Query(clearJobHandle).Without<QuadTreeElementRect>().AsParallel().AsUnsafe().Schedule<CollectJob, QuadTreeAspect, TransformAspect>(new CollectJob() {
                trees = this.trees,
            });
            
            var handleRect = context.Query(clearJobHandle).With<QuadTreeElementRect>().AsParallel().AsUnsafe().Schedule<CollectRectJob, QuadTreeAspect, TransformAspect>(new CollectRectJob() {
                trees = this.trees,
            });

            var job = new ApplyJob() {
                trees = this.trees,
            };
            var resultHandle = job.Schedule(this.trees.Length, 1, JobHandle.CombineDependencies(handle, handleRect));
            //var resultHandle = handle;
            context.SetDependency(resultHandle);

        }

        public void OnDestroy(ref SystemContext context) {

            for (int i = 0; i < this.trees.Length; ++i) {
                var item = (safe_ptr<NativeTrees.NativeQuadtree<Ent>>)this.trees[i];
                item.ptr->Dispose();
                _free(item);
            }

            this.trees.Dispose();

        }

        public readonly void FillAll(ref QuadTreeQueryAspect query, in TransformAspect tr) {
            
            if (tr.IsCalculated == false) return;

            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick == 0) return;

            if (query.readResults.results.IsCreated == true) query.results.results.Clear();
            if (query.readResults.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, q.nearestCount > 0u ? q.nearestCount : 1u);

            for (int i = 0; i < this.treesCount; ++i) {
                if ((q.treeMask & (1 << i)) == 0) {
                    continue;
                }
                ref var tree = ref *this.GetTree(i).ptr;
                var list = tree.tempObjects.ToList(Constants.ALLOCATOR_TEMP);
                foreach (var item in list) {
                    query.results.results.Add(item.obj);
                }
            }
        }

        public readonly void FillNearest<T>(ref QuadTreeQueryAspect query, in TransformAspect tr, in T subFilter = default) where T : struct, ISubFilter<Ent> {
            
            if (tr.IsCalculated == false) return;
            
            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick == 0) return;
            
            var marker = new Unity.Profiling.ProfilerMarker("Prepare");
            marker.Begin();
            var worldPos = tr.GetWorldMatrixPosition();
            var worldRot = q.useParentRotation == true ? tr.parent.GetAspect<TransformAspect>().GetWorldMatrixRotation() : tr.GetWorldMatrixRotation();
            var sector = new MathSector(worldPos, worldRot, query.readQuery.sector);
            var ent = tr.ent;
            marker.End();
            
            // clean up results
            marker = new Unity.Profiling.ProfilerMarker("Prepare:Clear");
            marker.Begin();
            if (query.readResults.results.IsCreated == true) query.results.results.Clear();
            marker.End();
            marker = new Unity.Profiling.ProfilerMarker("Prepare:Alloc");
            marker.Begin();
            if (query.readResults.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, q.nearestCount > 0u ? q.nearestCount : 1u);
            marker.End();

            if (q.nearestCount == 1u) {
                var nearest = this.GetNearestFirst(q.treeMask, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
                if (nearest.IsAlive() == true) query.results.results.Add(nearest);
            } else {
                this.GetNearest(q.treeMask, q.nearestCount, ref query.results.results, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
            }
            
        }
        
        public readonly Ent GetNearestFirst(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default,
                                            tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default, bool ignoreSorting = false) {
            return this.GetNearestFirst(mask, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSubFilter());
        }

        public readonly Ent GetNearestFirst<T>(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default, tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreSorting = default, in T subFilter = default) where T : struct, ISubFilter<Ent> {

            const uint nearestCount = 1u;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
            var d = new AABB2DDistanceSquaredProvider<Ent>();
            // for each tree
            for (int i = 0; i < this.treesCount; ++i) {
                if ((mask & (1 << i)) == 0) {
                    continue;
                }
                ref var tree = ref *this.GetTree(i).ptr;
                {
                    var visitor = new QuadtreeNearestAABBVisitor<Ent, T>() {
                        subFilter = subFilter,
                        sector = sector,
                        ignoreSelf = ignoreSelf,
                        ignore = selfEnt,
                    };
                    var marker = new Unity.Profiling.ProfilerMarker("tree::NearestFirst");
                    marker.Begin();
                    tree.Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref visitor, ref d);
                    if (visitor.found == true) {
                        if (ignoreSorting == true) {
                            marker.End();
                            return visitor.nearest;
                        }
                        heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(visitor.nearest, math.lengthsq(worldPos - visitor.nearest.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                    }
                    marker.End();
                }
            }
            
            if (ignoreSorting == false) {
                var max = math.min(nearestCount, heap.Count);
                if (max > 0u) return heap[heap.Pop()].data;
            }

            return default;

        }

        public readonly void GetNearest(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreY, bool ignoreSorting) {
            this.GetNearest(mask, nearestCount, ref results, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSubFilter());
        }

        public readonly void GetNearest<T>(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreSorting, in T subFilter = default) where T : struct, ISubFilter<Ent> {
            
            var bitsCount = math.countbits(mask);
            if (nearestCount > 0u) {

                var d = new AABB2DDistanceSquaredProvider<Ent>();
                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(nearestCount * this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>(nearestCount, Constants.ALLOCATOR_TEMP);
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i).ptr;
                    {
                        resultsTemp.Clear();
                        var visitor = new QuadtreeKNearestAABBVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = resultsTemp,
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        var marker = new Unity.Profiling.ProfilerMarker("tree::Nearest");
                        marker.Begin();
                        tree.Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref visitor, ref d);
                        if (ignoreSorting == true) {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                            markerResults.Begin();
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResults.End();
                        } else {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                            markerResults.Begin();
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResults.End();
                        }
                        marker.End();
                    }
                    if (bitsCount == 1) break;
                }
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    var max = math.min((uint)nearestCount, heap.Count);
                    results.EnsureCapacity(max);
                    for (uint i = 0u; i < max; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            } else {
                
                // select all units
                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>((int)this.treesCount, Constants.ALLOCATOR_TEMP);
                // for each tree
                for (int i = 0; i < this.treesCount; ++i) {
                    if ((mask & (1 << i)) == 0) {
                        continue;
                    }
                    ref var tree = ref *this.GetTree(i).ptr;
                    {
                        resultsTemp.Clear();
                        var visitor = new RangeAABB2DUniqueVisitor<Ent, T>() {
                            subFilter = subFilter,
                            sector = sector,
                            results = resultsTemp,
                            rangeSqr = rangeSqr,
                            max = nearestCount,
                            ignoreSelf = ignoreSelf,
                            ignore = selfEnt,
                        };
                        var range = math.sqrt(rangeSqr);
                        var marker = new Unity.Profiling.ProfilerMarker("tree::Range");
                        marker.Begin();
                        var bounds = new NativeTrees.AABB2D(worldPos.xz - range, worldPos.xz + range);
                        tree.Range(bounds, ref visitor);
                        if (ignoreSorting == true) {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                            markerResults.Begin();
                            results.EnsureCapacity(results.Count + (uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResults.End();
                        } else {
                            var markerResults = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                            markerResults.Begin();
                            heap.EnsureCapacity((uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResults.End();
                        }
                        marker.End();
                    }
                    if (bitsCount == 1) break;
                }
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    results.EnsureCapacity(heap.Count);
                    for (uint i = 0u; i < heap.Count; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            }

        }

        public bool Raycast(UnityEngine.Ray2D ray, int mask, tfloat distance, out QuadtreeRaycastHit<Ent> raycastHit, bool ignoreSorting = false) {
            
            raycastHit = default;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeap<NativeTrees.QuadtreeRaycastHitMinNode<Ent>>(this.treesCount, Constants.ALLOCATOR_TEMP);
            for (int i = 0; i < this.treesCount; ++i) {
                if ((mask & (1 << i)) == 0) {
                    continue;
                }

                var tree = this.GetTree(i).ptr;
                if (tree->RaycastAABB(ray, out var hitResult, distance) == true) {
                    if (ignoreSorting == true) return true;
                    heap.Push(new NativeTrees.QuadtreeRaycastHitMinNode<Ent>() {
                        data = hitResult,
                        cost = math.distancesq((float2)ray.origin, hitResult.point),
                    });
                }
            }

            if (ignoreSorting == false && heap.TryPop(out var result)) {
                raycastHit = result.data;
                return true;
            }
            return false;
            
        }

        public void OnDrawGizmos(ref SystemContext context) {
            UnityEngine.Gizmos.color = UnityEngine.Color.green;
            for (int i = 0; i < this.treesCount; ++i) {
                var tree = this.GetTree(i);
                tree.ptr->DrawGizmos();
            }
        }

    }

}
