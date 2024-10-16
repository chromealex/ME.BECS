
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    using ME.BECS.Transforms;

    public struct QuadTreeQuery : IComponent {

        /// <summary>
        /// Trees mask
        /// </summary>
        /* Ex: 1 << 0 - select first tree */
        public int treeMask;
        /// <summary>
        /// Range to select
        /// </summary>
        public float range;
        /// <summary>
        /// Reset pos.y to zero
        /// </summary>
        public bool ignoreY;
        /// <summary>
        /// Select X units for each tree
        /// </summary>
        public uint nearestCount;
        
    }

    public struct QuadTreeResult : IComponent {

        public ListAuto<Ent> results;

    }

    [EditorComment("Filter all entities which suitable for this query")]
    public struct QuadTreeQueryAspect : IAspect {

        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeQuery> queryPtr;
        public AspectDataPtr<QuadTreeResult> resultPtr;

        public ref QuadTreeQuery query => ref this.queryPtr.value.Get(this.ent.id, this.ent.gen);

        public ref QuadTreeResult results => ref this.resultPtr.value.Get(this.ent.id, this.ent.gen);
        
    }
    
    public struct AABBDistanceSquaredProvider<T> : NativeTrees.IOctreeDistanceProvider<T> {
        // Just return the distance squared to our bounds
        public float DistanceSquared(float3 point, T obj, NativeTrees.AABB bounds) => bounds.DistanceSquared(point);
    }

    public struct OctreeNearestIgnoreSelfAABBVisitor<T> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        public T ignoreSelf;
        public T nearest;
        public bool found;
        public bool OnVisit(T obj) {

            if (this.ignoreSelf.Equals(obj) == true) return true;
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    public struct OctreeNearestAABBVisitor<T> : NativeTrees.IOctreeNearestVisitor<T> {

        public T nearest;
        public bool found;
        public bool OnVisit(T obj) {
            
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    public struct OctreeKNearestAABBVisitor<T> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {
        public UnsafeHashSet<T> results;
        public uint max;
        public bool OnVisit(T obj) {
            this.results.Add(obj);
        
            return this.results.Count < this.max; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }
    
    public struct RangeAABBUniqueVisitor<T> : NativeTrees.IOctreeRangeVisitor<T> where T : unmanaged, System.IEquatable<T> {
        public UnsafeHashSet<T> results;
        public float rangeSqr;
        public uint max;
        public bool OnVisit(T obj, NativeTrees.AABB objBounds, NativeTrees.AABB queryRange) {
            // check if our object's AABB overlaps with the query AABB
            if (objBounds.Overlaps(queryRange) == true &&
                queryRange.DistanceSquared(objBounds.Center) <= this.rangeSqr) {
                this.results.Add(obj);
                if (this.max > 0u && this.results.Count == this.max) return false;
            }

            return true; // keep iterating
        }
    }
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(QuadTreeInsertSystem))]
    public unsafe struct QuadTreeQuerySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<QuadTreeQueryAspect, TransformAspect> {

            public QuadTreeInsertSystem system;

            public void Execute(in JobInfo jobInfo, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                var data = query.query;
                var worldPos = tr.GetWorldMatrixPosition();
                if (data.ignoreY == true) worldPos.y = 0f;
                
                // clean up results
                if (query.results.results.IsCreated == true) query.results.results.Clear();
                
                var heap = data.nearestCount > 0u ? new ME.BECS.NativeCollections.NativeMinHeapEnt(this.system.treesCount * (int)data.nearestCount, Unity.Collections.Allocator.Temp) : default;
                // for each tree
                for (int i = 0; i < this.system.treesCount; ++i) {

                    if ((query.query.treeMask & (1 << i)) == 0) {
                        continue;
                    }
                    
                    ref var tree = ref *this.system.GetTree(i);

                    if (data.nearestCount == 1u) {
                        
                        var visitor = new OctreeNearestAABBVisitor<Ent>();
                        tree.Nearest(worldPos, query.query.range, ref visitor, new AABBDistanceSquaredProvider<Ent>());
                        if (query.results.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, 1u);
                        //query.results.results.Add(visitor.nearest);
                        heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(visitor.nearest, math.lengthsq(worldPos - visitor.nearest.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                        
                    } else if (data.nearestCount > 1u) {
                        
                        // k-nearest
                        var visitor = new OctreeKNearestAABBVisitor<Ent>() {
                            results = new UnsafeHashSet<Ent>((int)data.nearestCount, Unity.Collections.Allocator.Temp),
                            max = data.nearestCount,
                        };
                        tree.Nearest(worldPos, query.query.range, ref visitor, new AABBDistanceSquaredProvider<Ent>());
                        if (query.results.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, (uint)visitor.results.Count);
                        //query.results.results.AddRange(visitor.results.ToNativeArray(Unity.Collections.Allocator.Temp));
                        foreach (var item in visitor.results) {
                            heap.Push(new ME.BECS.NativeCollections.MinHeapNodeEnt(item, math.lengthsq(worldPos - item.GetAspect<TransformAspect>().GetWorldMatrixPosition())));
                        }

                    } else {

                        var visitor = new RangeAABBUniqueVisitor<Ent>() {
                            results = new UnsafeHashSet<Ent>((int)data.nearestCount, Unity.Collections.Allocator.Temp),
                            rangeSqr = query.query.range * query.query.range,
                            max = data.nearestCount,
                        };
                        tree.Range(new NativeTrees.AABB(worldPos - query.query.range, worldPos + query.query.range), ref visitor);
                        if (query.results.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, (uint)visitor.results.Count);
                        query.results.results.AddRange(visitor.results.ToNativeArray(Unity.Collections.Allocator.Temp));
                        
                    }

                }

                if (data.nearestCount > 0u) {
                    var max = math.min(data.nearestCount, heap.Count);
                    for (uint i = 0; i < max; ++i) {
                        query.results.results.Add(heap[heap.Pop()].Position);
                    }
                }

            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var handle = API.Query(in context).Schedule<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}