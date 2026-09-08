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
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;
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
        private UnsafeList<safe_ptr> staticTrees;
        private ME.BECS.NativeCollections.NativeParallelList<SpatialQueryCandidate<Ent>> queryScratch;
        public readonly uint treesCount => (uint)this.trees.Length;
        private ushort worldId;
        private ushort stateVersion;
        private ulong stateTick;

        [BURST]
        public struct CollectRectJob : IJobForAspects<SpatialAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;
            public bool isStatic;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref SpatialAspect spatialAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[spatialAspect.readTreeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var size = spatialAspect.rectSize;
                var halfSize = new float2(size.x * 0.5f, size.y * 0.5f);
                var bounds = new NativeTrees.AABB2D(pos - halfSize, pos + new float2(halfSize.x, halfSize.y));
                if (this.isStatic == true) {
                    tree.ptr->AddStatic(tr.ent, bounds);
                } else {
                    tree.ptr->Add(tr.ent, bounds);
                }
                
            }

        }

        [BURST]
        public struct CollectJob : IJobForAspects<SpatialAspect, TransformAspect> {
            
            public UnsafeList<safe_ptr> trees;
            public bool isStatic;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref SpatialAspect spatialAspect, ref TransformAspect tr) {
                
                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[spatialAspect.readTreeIndex];
                if (tr.IsCalculated == false) return;
                var pos = tr.GetWorldMatrixPosition().xz;
                var radius = spatialAspect.readSpatialElement.radius;

                var bounds = new NativeTrees.AABB2D(pos - spatialAspect.readSpatialElement.radius, pos + new float2(radius, radius));
                if (this.isStatic == true) {
                    tree.ptr->AddStatic(tr.ent, bounds);
                } else {
                    tree.ptr->Add(tr.ent, bounds);
                }
                
            }

        }

        [BURST]
        public struct ApplyJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;
            public UnsafeList<safe_ptr> staticTrees;
            public bool forceStaticRebuild;
            
            public void Execute(int index) {

                var tree = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[index];
                tree.ptr->Rebuild();
                var staticTree = (safe_ptr<NativeTrees.SpatialHashing>)this.staticTrees[index];
                staticTree.ptr->RebuildStatic(this.forceStaticRebuild);
                
            }

        }
        
        [BURST]
        public struct ClearJob : Unity.Jobs.IJobParallelFor {

            public UnsafeList<safe_ptr> trees;
            public UnsafeList<safe_ptr> staticTrees;

            public void Execute(int index) {

                var item = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[index];
                item.ptr->Clear();
                var staticItem = (safe_ptr<NativeTrees.SpatialHashing>)this.staticTrees[index];
                staticItem.ptr->ClearStaticStaging();
                
            }

        }
        
        [INLINE(256)]
        public readonly safe_ptr<NativeTrees.SpatialHashing> GetTree(int treeIndex) {

            return (safe_ptr<NativeTrees.SpatialHashing>)this.trees[treeIndex];

        }

        [INLINE(256)]
        public int AddTree() {

            return this.AddTree(this.capacity, this.cellSize);

        }

        [INLINE(256)]
        public readonly safe_ptr<NativeTrees.SpatialHashing> GetStaticTree(int treeIndex) {

            return (safe_ptr<NativeTrees.SpatialHashing>)this.staticTrees[treeIndex];

        }

        [INLINE(256)]
        public int AddTree(int cellSize) {

            return this.AddTree(this.capacity, cellSize);

        }

        [INLINE(256)]
        public int AddTree(int capacity, int cellSize) {

            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator;
            this.trees.Add((safe_ptr)_make(new NativeTrees.SpatialHashing(capacity, cellSize, allocator)));
            this.staticTrees.Add((safe_ptr)_make(new NativeTrees.SpatialHashing(math.max(1, capacity / 4), cellSize, allocator, true)));
            return this.trees.Length - 1;

        }

        public void OnAwake(ref SystemContext context) {

            this.worldId = context.world.id;
            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(this.worldId).Allocator.ToAllocator;
            this.trees = new UnsafeList<safe_ptr>(10, allocator);
            this.staticTrees = new UnsafeList<safe_ptr>(10, allocator);
            this.queryScratch = new ME.BECS.NativeCollections.NativeParallelList<SpatialQueryCandidate<Ent>>(64, allocator);
            this.stateVersion = context.world.state.ptr->allocator.version;
            this.stateTick = context.world.state.ptr->tick;
            
        }

        public void OnUpdate(ref SystemContext context) {

            var currentStateVersion = context.world.state.ptr->allocator.version;
            var currentStateTick = context.world.state.ptr->tick;
            var forceStaticRebuild = currentStateVersion != this.stateVersion || currentStateTick < this.stateTick;
            this.stateVersion = currentStateVersion;
            this.stateTick = currentStateTick;
            var clearJob = new ClearJob() {
                trees = this.trees,
                staticTrees = this.staticTrees,
            };
            var clearJobHandle = clearJob.Schedule(this.trees.Length, 1, context.dependsOn);
            
            var handle = context.Query(clearJobHandle).Without<IsTransformStaticCalculatedComponent>().Without<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectJob, SpatialAspect, TransformAspect>(new CollectJob() {
                trees = this.trees,
            });
            
            var handleRect = context.Query(clearJobHandle).Without<IsTransformStaticCalculatedComponent>().With<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectRectJob, SpatialAspect, TransformAspect>(new CollectRectJob() {
                trees = this.trees,
            });

            var staticHandle = context.Query(clearJobHandle).With<IsTransformStaticCalculatedComponent>().Without<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectJob, SpatialAspect, TransformAspect>(new CollectJob() {
                trees = this.staticTrees,
                isStatic = true,
            });

            var staticHandleRect = context.Query(clearJobHandle).With<IsTransformStaticCalculatedComponent>().With<SpatialElementRect>().AsParallel().AsUnsafe().Schedule<CollectRectJob, SpatialAspect, TransformAspect>(new CollectRectJob() {
                trees = this.staticTrees,
                isStatic = true,
            });

            var job = new ApplyJob() {
                trees = this.trees,
                staticTrees = this.staticTrees,
                forceStaticRebuild = forceStaticRebuild,
            };
            var dynamicHandle = JobHandle.CombineDependencies(handle, handleRect);
            var staticCollectHandle = JobHandle.CombineDependencies(staticHandle, staticHandleRect);
            var resultHandle = job.Schedule(this.trees.Length, 1, JobHandle.CombineDependencies(dynamicHandle, staticCollectHandle));
            context.SetDependency(resultHandle);

        }

        public void OnDestroy(ref SystemContext context) {

            for (int i = 0; i < this.trees.Length; ++i) {
                var item = (safe_ptr<NativeTrees.SpatialHashing>)this.trees[i];
                item.ptr->Dispose();
                _free(item);
                var staticItem = (safe_ptr<NativeTrees.SpatialHashing>)this.staticTrees[i];
                staticItem.ptr->Dispose();
                _free(staticItem);
            }

            this.trees.Dispose();
            this.staticTrees.Dispose();
            this.queryScratch.Dispose();

        }

        [INLINE(256)]
        public readonly void FillAll(ref SpatialQueryAspect query, in TransformAspect tr) {
            
            if (tr.IsCalculated == false) return;

            var q = query.readQuery;
            if (q.updatePerTick > 0 && (query.ent.World.CurrentTick + query.ent.id) % q.updatePerTick != 0) return;

            QueryResults.Create(ref query.results.results, query.ent, q.nearestCount > 0u ? q.nearestCount : 1u, q.updatePerTick == 0);

            var mask = q.treeMask;
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                ref var tree = ref *this.GetTree(i).ptr;
                var list = tree.GetObjects();
                foreach (var item in list) {
                    query.results.results.Add(item.obj);
                }
                var staticList = this.GetStaticTree(i).ptr->GetObjects();
                foreach (var item in staticList) {
                    query.results.results.Add(item.obj);
                }
            }
        }

        [INLINE(256)]
        public readonly void FillNearest<T>(ref SpatialQueryAspect query, in TransformAspect tr, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {
            
            var q = query.readQuery;
            var ent = query.ent;

            var worldPos = tr.GetWorldMatrixPosition();
            MathSector sector = default;
            if (query.readQuery.sector > 0 && query.readQuery.sector < 360) {
                var worldRot = q.useParentRotation == true ? tr.parent.GetAspect<TransformAspect>().rotation : tr.rotation;
                sector = new MathSector(worldPos, worldRot, query.readQuery.sector);
            }
            QueryResults.Create(ref query.results.results, query.ent, q.nearestCount > 0u ? q.nearestCount : 1u, q.updatePerTick == 0);
            
            if (q.nearestCount == 1u) {
                var nearest = this.GetNearestFirst(q.treeMask, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
                if (nearest.IsAlive() == true) query.results.results.Add(nearest);
            } else {
                this.GetNearest(q.treeMask, q.nearestCount, ref query.results.results, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf, q.ignoreSorting, in subFilter);
            }
        }
        
        [INLINE(256)]
        public readonly Ent GetNearestFirst(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default,
                                            tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreSorting = false) {
            return this.GetNearestFirst(mask, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSpatialSubFilter());
        }

        [INLINE(256)]
        public readonly Ent GetNearestFirst<T>(int mask, in Ent selfEnt = default, in float3 worldPos = default, in MathSector sector = default, tfloat minRangeSqr = default, tfloat rangeSqr = default, bool ignoreSelf = default, bool ignoreSorting = default, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {

            var d = new AABB2DSpatialDistanceSquaredProvider<Ent>();
            var visitor = new SpatialNearestAABBVisitor<Ent, T>() {
                subFilter = subFilter,
                sector = sector,
                ignoreSelf = ignoreSelf,
                ignore = selfEnt,
            };
            Ent result = default;
            var resultDistanceSqr = tfloat.MaxValue;
            var found = false;
            // for each tree
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                for (var layer = 0; layer < 2; ++layer) {
                    var tree = layer == 0 ? this.GetTree(i).ptr : this.GetStaticTree(i).ptr;
                    tree->NearestFirst(worldPos.xz, minRangeSqr, rangeSqr, ref visitor, ref d, ignoreSorting);
                    if (visitor.found == true) {
                        if (ignoreSorting == true) {
                            return visitor.nearest;
                        }

                        var distSq = visitor.nearestDistanceSqr;
                        if (found == false || distSq < resultDistanceSqr || (distSq == resultDistanceSqr && visitor.nearest.CompareTo(result) < 0)) {
                            result = visitor.nearest;
                            resultDistanceSqr = distSq;
                            found = true;
                        }
                        rangeSqr = math.min(rangeSqr, distSq);
                    }
                    visitor.Reset();
                }
            }
            
            return result;

        }

        [INLINE(256)]
        public bool Raycast(Ray2D ray, int mask, tfloat distance, out SpatialRaycastHit raycastHit, bool ignoreSorting = false) {
            
            raycastHit = default;
            var heap = ignoreSorting == true ? default : new ME.BECS.NativeCollections.NativeMinHeap<NativeTrees.SpatialRaycastHitMinNode>(this.treesCount * 2u, Constants.ALLOCATOR_TEMP);
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                for (var layer = 0; layer < 2; ++layer) {
                    var tree = layer == 0 ? this.GetTree(i).ptr : this.GetStaticTree(i).ptr;
                    if (tree->RaycastAABB(ray, out var hitResult, distance) == true) {
                        if (ignoreSorting == true) return true;
                        heap.Push(new NativeTrees.SpatialRaycastHitMinNode() {
                            data = hitResult,
                            cost = math.distancesq(ray.origin, hitResult.point),
                        });
                    }
                }
            }

            if (ignoreSorting == false && heap.TryPop(out var result)) {
                raycastHit = result.data;
                return true;
            }
            return false;

        }

        [INLINE(256)]
        public readonly void GetNearest(int mask, ushort nearestCount, ref QueryResults results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreY, bool ignoreSorting) {
            this.GetNearest(mask, nearestCount, ref results, in selfEnt, in worldPos, in sector, minRangeSqr, rangeSqr, ignoreSelf, ignoreSorting, new AlwaysTrueSpatialSubFilter());
        }

        [INLINE(256)]
        public readonly void GetNearest<T>(int mask, ushort nearestCount, ref QueryResults results, in Ent selfEnt, in float3 worldPos, in MathSector sector, tfloat minRangeSqr, tfloat rangeSqr, bool ignoreSelf, bool ignoreSorting, in T subFilter = default) where T : struct, ISpatialSubFilter<Ent> {
            var distanceProvider = new AABB2DSpatialDistanceSquaredProvider<Ent>();
            if (nearestCount > 0u) {
                if (ignoreSorting == true) {
                    results.EnsureCapacity(results.Count + (uint)nearestCount * (uint)math.countbits(mask));
                    var directVisitor = new SpatialKNearestDirectAABBVisitor<T>() {
                        subFilter = subFilter,
                        results = _addressT(ref results),
                        max = nearestCount,
                        sector = sector,
                        ignoreSelf = ignoreSelf,
                        ignore = selfEnt,
                    };
                    while (mask != 0) {
                        int i = math.tzcnt(mask);
                        mask &= mask - 1;
                        this.GetTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref directVisitor, ref distanceProvider);
                        this.GetStaticTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref directVisitor, ref distanceProvider);
                        directVisitor.Reset();
                    }
                    return;
                }

                FixedList512Bytes<SpatialQueryCandidate<Ent>> fixedResults = default;
                if (nearestCount <= fixedResults.Capacity) {
                    var fixedVisitor = new SpatialKNearestFixedAABBVisitor<Ent, T>() {
                        subFilter = subFilter,
                        results = fixedResults,
                        max = nearestCount,
                        sector = sector,
                        ignoreSelf = ignoreSelf,
                        ignore = selfEnt,
                    };
                    while (mask != 0) {
                        int i = math.tzcnt(mask);
                        mask &= mask - 1;
                        this.GetTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref fixedVisitor, ref distanceProvider);
                        this.GetStaticTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref fixedVisitor, ref distanceProvider);
                    }
                    fixedResults = fixedVisitor.results;
                    Sort(ref fixedResults);
                    results.EnsureCapacity((uint)fixedResults.Length);
                    for (var i = 0; i < fixedResults.Length; ++i) results.Add(fixedResults[i].obj);
                    return;
                }

                ref var nearestScratch = ref this.queryScratch.GetThreadList();
                nearestScratch.Clear();
                var nearestVisitor = new SpatialKNearestAABBVisitor<Ent, T>() {
                    subFilter = subFilter,
                    results = nearestScratch,
                    max = nearestCount,
                    sector = sector,
                    ignoreSelf = ignoreSelf,
                    ignore = selfEnt,
                };
                while (mask != 0) {
                    int i = math.tzcnt(mask);
                    mask &= mask - 1;
                    this.GetTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref nearestVisitor, ref distanceProvider);
                    this.GetStaticTree(i).ptr->Nearest(worldPos.xz, minRangeSqr, rangeSqr, ref nearestVisitor, ref distanceProvider);
                }
                nearestVisitor.results.Sort();
                results.EnsureCapacity((uint)nearestVisitor.results.Length);
                foreach (var item in nearestVisitor.results) results.Add(item.obj);
                nearestScratch = nearestVisitor.results;
                return;
            }

            var range = math.sqrt(rangeSqr);
            var bounds = new NativeTrees.AABB2D(worldPos.xz - range, worldPos.xz + range);
            if (ignoreSorting == true) {
                var directVisitor = new RangeAABB2DSpatialDirectVisitor<T>() {
                    subFilter = subFilter,
                    results = _addressT(ref results),
                    rangeSqr = rangeSqr,
                    sector = sector,
                    ignoreSelf = ignoreSelf,
                    ignore = selfEnt,
                };
                while (mask != 0) {
                    int i = math.tzcnt(mask);
                    mask &= mask - 1;
                    this.GetTree(i).ptr->Range(bounds, ref directVisitor);
                    this.GetStaticTree(i).ptr->Range(bounds, ref directVisitor);
                }
                return;
            }

            ref var rangeScratch = ref this.queryScratch.GetThreadList();
            rangeScratch.Clear();
            var rangeVisitor = new RangeAABB2DSpatialUniqueVisitor<Ent, T>() {
                subFilter = subFilter,
                results = rangeScratch,
                rangeSqr = rangeSqr,
                sector = sector,
                ignoreSelf = ignoreSelf,
                ignore = selfEnt,
            };
            while (mask != 0) {
                int i = math.tzcnt(mask);
                mask &= mask - 1;
                this.GetTree(i).ptr->Range(bounds, ref rangeVisitor);
                this.GetStaticTree(i).ptr->Range(bounds, ref rangeVisitor);
            }
            rangeVisitor.results.Sort();
            results.EnsureCapacity((uint)rangeVisitor.results.Length);
            foreach (var item in rangeVisitor.results) results.Add(item.obj);
            rangeScratch = rangeVisitor.results;

        }

        [INLINE(256)]
        private static void Sort(ref FixedList512Bytes<SpatialQueryCandidate<Ent>> items) {
            for (var i = 1; i < items.Length; ++i) {
                var value = items[i];
                var j = i - 1;
                while (j >= 0 && value.CompareTo(items[j]) < 0) {
                    items[j + 1] = items[j];
                    --j;
                }
                items[j + 1] = value;
            }
        }

        [WithoutBurst]
        public void OnDrawGizmos(ref SystemContext context) {
            UnityEngine.Gizmos.color = UnityEngine.Color.green;
            for (int i = 0; i < this.treesCount; ++i) {
                var tree = this.GetTree(i);
                tree.ptr->DrawGizmos();
                var staticTree = this.GetStaticTree(i);
                staticTree.ptr->DrawGizmos();
            }
        }

    }

}
