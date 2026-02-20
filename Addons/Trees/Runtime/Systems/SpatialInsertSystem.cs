#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Ray2D = ME.BECS.FixedPoint.Ray2D;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Ray2D = UnityEngine.Ray2D;
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
    
    [ComponentGroup(typeof(SpatialComponentGroup))]
    [StructLayout(LayoutKind.Explicit)]
    public struct SpatialElement : IComponent {

        [FieldOffset(0)]
        public tfloat radius;
        [FieldOffset(0)]
        public tfloat sizeX;
        [FieldOffset(4)]
        public int treeIndex;
        [FieldOffset(8)]
        public byte ignoreY;

    }

    [ComponentGroup(typeof(SpatialComponentGroup))]
    public struct SpatialElementRect : IComponent {

        public tfloat sizeY;
        
    }

    [ComponentGroup(typeof(SpatialComponentGroup))]
    public struct SpatialHeightComponent : IComponent {
        
        public tfloat height;
        
    }

    [EditorComment("Used by SpatialInsertSystem to filter entities by treeIndex")]
    public struct SpatialAspect : IAspect {
        
        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<SpatialElement> spatialElementPtr;
        public AspectDataPtr<SpatialElementRect> spatialRectPtr;
        public AspectDataPtr<SpatialHeightComponent> spatialHeightPtr;

        public readonly ref SpatialElement spatialElement => ref this.spatialElementPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref readonly SpatialElement readSpatialElement => ref this.spatialElementPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref int treeIndex => ref this.spatialElement.treeIndex;
        public readonly ref readonly int readTreeIndex => ref this.readSpatialElement.treeIndex;
        public readonly bool isRect => this.ent.Has<SpatialElementRect>();
        public readonly bool hasHeight => this.ent.Has<SpatialHeightComponent>();
        public readonly float2 rectSize => new float2(this.readSpatialElement.sizeX, this.spatialRectPtr.Read(this.ent.id, this.ent.gen).sizeY);
        public readonly tfloat height => this.spatialHeightPtr.Read(this.ent.id, this.ent.gen).height;

        public readonly void SetHeight(tfloat height) {
            this.ent.Set(new SpatialHeightComponent() {
                height = height,
            });
        }
        
        public readonly void SetAsRectWithSize(tfloat sizeX, tfloat sizeY) {
            ref var rect = ref this.spatialRectPtr.Get(this.ent.id, this.ent.gen);
            rect.sizeY = sizeY;
            this.spatialElement.sizeX = sizeX;
        }

    }
    
    [BURST]
    public unsafe struct SpatialInsertSystem : IAwake, IUpdate, IDestroy, IDrawGizmos {
        
        public static SpatialInsertSystem Default => new SpatialInsertSystem() {
            capacity = 1000,
            cellSize = 2,
        };

        public int capacity;
        public int cellSize;
        
        private UnsafeList<safe_ptr> trees;
        public readonly uint treesCount => (uint)this.trees.Length;
        private ushort worldId;

        [BURST]
        public struct CollectRectJob : IJobForAspects<SpatialAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref SpatialAspect spatialAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[spatialAspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var size = spatialAspect.rectSize;
                var halfSize = new float2(size.x * 0.5f, size.y * 0.5f);
                tree.ptr->Add(tr.ent, new NativeTrees.AABB2D(pos - halfSize, pos + new float2(halfSize.x, halfSize.y)));
                
            }

        }

        [BURST]
        public struct CollectJob : IJobForAspects<SpatialAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref SpatialAspect spatialAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[spatialAspect.treeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var radius = spatialAspect.readSpatialElement.radius;

                tree.ptr->Add(tr.ent, new NativeTrees.AABB2D(pos - spatialAspect.readSpatialElement.radius, pos + new float2(radius, radius)));
                
            }

        }

        [BURST]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;
            
            public void Execute(int index) {

                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[index];
                tree.ptr->Rebuild();
                
            }

        }
        
        [BURST]
        public struct ClearJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;

            public void Execute(int index) {

                var item = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[index];
                item.ptr->Clear();
                
            }

        }
        
        [INLINE(256)]
        public readonly safe_ptr<NativeTrees.SpatialHashing> GetTree(int treeIndex) {

            return (safe_ptr<NativeTrees.SpatialHashing>)this.trees[treeIndex];

        }

        [INLINE(256)]
        public int AddTree() {

            this.trees.Add((safe_ptr)_make(new NativeTrees.SpatialHashing(this.capacity, this.cellSize, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator)));
            return this.trees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.worldId = context.world.id;
            this.trees = new UnsafeList<safe_ptr>(10, WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator);
            
        }

        public void OnUpdate(ref SystemContext context) {

            var clearJob = new ClearJob() {
                trees = this.trees,
            };
            var clearJobHandle = clearJob.Schedule(this.trees.Length, 1, context.dependsOn);
            
            var handle = context.Query(clearJobHandle).Without<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectJob, SpatialAspect, TransformAspect>(new CollectJob() {
                trees = this.trees,
            });
            
            var handleRect = context.Query(clearJobHandle).With<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectRectJob, SpatialAspect, TransformAspect>(new CollectRectJob() {
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
                var item = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[i];
                item.ptr->Dispose();
                _free(item);
            }

            this.trees.Dispose();

        }

        [INLINE(256)]
        public readonly void FillAll(ref SpatialQueryAspect query, in TransformAspect tr) {
            
            if (tr.IsCalculated == false) return;

            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick == 0) return;

            if (query.readResults.results.IsCreated == true) query.results.results.Clear();
            if (query.readResults.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, q.nearestCount > 0u ? q.nearestCount : 1u);

            var mask = q.treeMask;
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                ref var tree = ref *this.GetTree(i).ptr;
                var list = tree.tempObjects.ToList(Constants.ALLOCATOR_TEMP);
                foreach (var item in list) {
                    query.results.results.Add(item.obj);
                }
            }
        }

        [INLINE(256)]
        public readonly void FillNearest<T>(ref SpatialQueryAspect query, in TransformAspect tr, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {
            
            if (tr.IsCalculated == false) return;
            
            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick == 0) return;
            
            var worldPos = tr.GetWorldMatrixPosition();
            MathSector sector = default;
            if (query.readQuery.sector > 0 || query.readQuery.sector < 360) {
                var worldRot = q.useParentRotation == true ? tr.parent.GetAspect<TransformAspect>().GetWorldMatrixRotation() : tr.GetWorldMatrixRotation();
                sector = new MathSector(worldPos, worldRot, query.readQuery.sector);
            }

            var ent = tr.ent;
            
            // clean up results
            if (query.readResults.results.IsCreated == true) query.results.results.Clear();
            if (query.readResults.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, q.nearestCount > 0u ? q.nearestCount : 1u);
            
            if (q.nearestCount == 1u) {
                var nearest = this.GetNearestFirst(q.treeMask, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
                if (nearest.IsAlive() == true) query.results.results.Add(nearest);
            } else {
                this.GetNearest(q.treeMask, q.nearestCount, ref query.results.results, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
            }
            
        }
        
        [INLINE(256)]
        public readonly Ent GetNearestFirst(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default,
                                            tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreY = default, bool ignoreSorting = false) {
            return this.GetNearestFirst(mask, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSpatialSubFilter());
        }

        [INLINE(256)]
        public readonly Ent GetNearestFirst<T>(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default, tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreSorting = default, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {

            const uint nearestCount = 1u;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
            var d = new AABB2DSpatialDistanceSquaredProvider<Ent>();
            var marker = new Unity.Profiling.ProfilerMarker("tree::NearestFirst");
            marker.Begin();
            var visitor = new SpatialNearestAABBVisitor<Ent, T>() {
                subFilter = subFilter,
                sector = sector,
                ignoreSelf = ignoreSelf,
                ignore = selfEnt,
            };
            Ent result = default;
            // for each tree
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                var tree = this.GetTree(i).ptr;
                {
                    tree->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref visitor, ref d);
                    if (visitor.found == true) {
                        if (ignoreSorting == true) {
                            result = visitor.nearest;
                            break;
                        }

                        var distSq = math.distancesq(worldPos, visitor.nearest.GetAspect<TransformAspect>().GetWorldMatrixPosition());
                        heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(visitor.nearest, distSq));
                        rangeSqr = math.min(rangeSqr, distSq);
                    }
                    visitor.Reset();
                }
            }
            marker.End();
            
            if (ignoreSorting == false) {
                var max = math.min(nearestCount, heap.Count);
                if (max > 0u) return heap[heap.Pop()].data;
            }

            return result;

        }

        [INLINE(256)]
        public bool Raycast(Ray2D ray, int mask, sfloat distance, out SpatialRaycastHit raycastHit, bool ignoreSorting = false) {
            
            raycastHit = default;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeap<NativeTrees.SpatialRaycastHitMinNode>(this.treesCount, Constants.ALLOCATOR_TEMP);
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                var tree = this.GetTree(i).ptr;
                if (tree->RaycastAABB(ray, out var hitResult, distance) == true) {
                    if (ignoreSorting == true) return true;
                    heap.Push(new NativeTrees.SpatialRaycastHitMinNode() {
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

        [INLINE(256)]
        public readonly void GetNearest(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreY, bool ignoreSorting) {
            this.GetNearest(mask, nearestCount, ref results, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSpatialSubFilter());
        }

        [INLINE(256)]
        public readonly void GetNearest<T>(int mask, ushort nearestCount, ref ListAuto<Ent> results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreSorting, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {
            
            if (nearestCount > 0u) {

                var marker = new Unity.Profiling.ProfilerMarker("tree::Nearest");
                var markerResultsUnsorted = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                var markerResultsSorted = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                var d = new AABB2DSpatialDistanceSquaredProvider<Ent>();
                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(nearestCount * this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>(nearestCount, Constants.ALLOCATOR_TEMP);
                var visitor = new SpatialKNearestAABBVisitor<Ent, T>() {
                    subFilter = subFilter,
                    sector = sector,
                    results = resultsTemp,
                    max = nearestCount,
                    ignoreSelf = ignoreSelf,
                    ignore = selfEnt,
                };
                marker.Begin();
                // for each tree
                while (mask != 0) {
                    int i = math.tzcnt(mask);
                    mask &= mask - 1;
                    var tree = this.GetTree(i).ptr;
                    {
                        tree->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref visitor, ref d);
                        if (ignoreSorting == true) {
                            markerResultsUnsorted.Begin();
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResultsUnsorted.End();
                        } else {
                            markerResultsSorted.Begin();
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.distancesq(worldPos, item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResultsSorted.End();
                        }
                        visitor.Reset();
                    }
                }
                marker.End();
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    var max = math.min((uint)nearestCount, heap.Count);
                    results.EnsureCapacity(max);
                    for (uint i = 0u; i < max; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            } else {
                
                var marker = new Unity.Profiling.ProfilerMarker("tree::Range");
                var markerResultsUnsorted = new Unity.Profiling.ProfilerMarker("Fill Results (Unsorted)");
                var markerResultsSorted = new Unity.Profiling.ProfilerMarker("Fill Results (Sorted)");
                // select all units
                var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeapEnt(this.treesCount, Constants.ALLOCATOR_TEMP);
                var resultsTemp = new UnsafeHashSet<Ent>((int)this.treesCount, Constants.ALLOCATOR_TEMP);
                var visitor = new RangeAABB2DSpatialUniqueVisitor<Ent, T>() {
                    subFilter = subFilter,
                    sector = sector,
                    results = resultsTemp,
                    rangeSqr = rangeSqr,
                    max = nearestCount,
                    ignoreSelf = ignoreSelf,
                    ignore = selfEnt,
                };
                // for each tree
                marker.Begin();
                while (mask != 0) {
                    int i = math.tzcnt(mask);
                    mask &= mask - 1;
                    var tree = this.GetTree(i).ptr;
                    {
                        var range = math.sqrt(rangeSqr);
                        var bounds = new NativeTrees.AABB2D(worldPos.xz - range, worldPos.xz + range);
                        tree->Range(bounds, ref visitor);
                        if (ignoreSorting == true) {
                            markerResultsUnsorted.Begin();
                            results.EnsureCapacity(results.Count + (uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                results.Add(item);
                            }
                            markerResultsUnsorted.End();
                        } else {
                            markerResultsSorted.Begin();
                            heap.EnsureCapacity((uint)visitor.results.Count);
                            foreach (var item in visitor.results) {
                                heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.distancesq(worldPos, item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                            }
                            markerResultsSorted.End();
                        }
                        visitor.Reset();
                    }
                }
                marker.End();
                resultsTemp.Dispose();

                if (ignoreSorting == false) {
                    results.EnsureCapacity(heap.Count);
                    for (uint i = 0u; i < heap.Count; ++i) {
                        results.Add(heap[heap.Pop()].data);
                    }
                }

            }

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
