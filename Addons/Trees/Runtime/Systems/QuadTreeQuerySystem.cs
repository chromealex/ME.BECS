
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
        public float rangeSqr;
        /// <summary>
        /// Min range to select
        /// </summary>
        public float minRangeSqr;
        /// <summary>
        /// Sector angle in degrees (align to look rotation)
        /// </summary>
        public float sector;
        /// <summary>
        /// Select X units for each tree
        /// </summary>
        public ushort nearestCount;
        /// <summary>
        /// Reset pos.y to zero
        /// </summary>
        public byte ignoreY;
        /// <summary>
        /// Ignore self
        /// </summary>
        public byte ignoreSelf;

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

        public readonly ref QuadTreeQuery query => ref this.queryPtr.value.Get(this.ent.id, this.ent.gen);
        public readonly ref QuadTreeResult results => ref this.resultPtr.value.Get(this.ent.id, this.ent.gen);

        public readonly ref readonly QuadTreeQuery readQuery => ref this.queryPtr.value.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly QuadTreeResult readResults => ref this.resultPtr.value.Read(this.ent.id, this.ent.gen);

    }
    
    public struct AABBDistanceSquaredProvider<T> : NativeTrees.IOctreeDistanceProvider<T> {
        public bool ignoreY;
        // Just return the distance squared to our bounds
        public float DistanceSquared(float3 point, T obj, NativeTrees.AABB bounds) => bounds.DistanceSquared(point, this.ignoreY);
    }

    public struct OctreeNearestIgnoreSelfAABBVisitor<T> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        public T ignoreSelf;
        public T nearest;
        public bool found;
        public bool OnVisit(T obj, NativeTrees.AABB bounds) {

            if (this.ignoreSelf.Equals(obj) == true) return true;
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    public interface ISubFilter<T> where T : unmanaged {

        bool IsValid(in T ent, in NativeTrees.AABB bounds);

    }

    public struct AlwaysTrueSubFilter : ISubFilter<Ent> {

        public bool IsValid(in Ent ent, in NativeTrees.AABB bounds) => true;

    }
    
    public struct OctreeNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {

        public TSubFilter subFilter;
        public T nearest;
        public bool found;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;

        public bool OnVisit(T obj, NativeTrees.AABB bounds) {

            if (this.subFilter.IsValid(in obj, in bounds) == false) {
                return true;
            } 

            if (this.sector.IsValid(bounds.Center) == false) {
                return true;
            }

            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }
            
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    public struct OctreeKNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {

        public TSubFilter subFilter;
        public UnsafeHashSet<T> results;
        public uint max;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;

        public bool OnVisit(T obj, NativeTrees.AABB bounds) {

            if (this.subFilter.IsValid(in obj, in bounds) == false) {
                return true;
            } 
            
            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }
            
            if (this.sector.IsValid(bounds.Center) == true) {
                this.results.Add(obj);
            }

            if (this.max == 0u) return true;
            return this.results.Count < this.max; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }
    
    public struct RangeAABBUniqueVisitor<T, TSubFilter> : NativeTrees.IOctreeRangeVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {
        
        public TSubFilter subFilter;
        public UnsafeHashSet<T> results;
        public float rangeSqr;
        public uint max;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;

        public bool OnVisit(T obj, NativeTrees.AABB objBounds, NativeTrees.AABB queryRange) {
            
            if (this.subFilter.IsValid(in obj, in objBounds) == false) {
                return true;
            } 

            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }

            if (this.sector.IsValid(objBounds.Center) == true) {
                // check if our object's AABB overlaps with the query AABB
                if (objBounds.Overlaps(queryRange) == true &&
                    queryRange.DistanceSquared(objBounds.Center) <= this.rangeSqr) {
                    this.results.Add(obj);
                    if (this.max > 0u && this.results.Count == this.max) return false;
                }
            }

            return true; // keep iterating
        }
    }
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(QuadTreeInsertSystem))]
    public struct QuadTreeQuerySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<QuadTreeQueryAspect, TransformAspect> {

            public QuadTreeInsertSystem system;

            public void Execute(in JobInfo jobInfo, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                if (tr.IsCalculated == false) return;
                var data = query.query;
                var worldPos = tr.GetWorldMatrixPosition();
                var worldRot = tr.GetWorldMatrixRotation();
                var sector = new MathSector(worldPos, worldRot, query.query.sector);
                var ent = tr.ent;
                
                // clean up results
                if (query.results.results.IsCreated == true) query.results.results.Clear();

                if (query.results.results.IsCreated == false) query.results.results = new ListAuto<Ent>(query.ent, data.nearestCount > 0u ? data.nearestCount : 1u);

                var q = query.readQuery;
                if (data.nearestCount == 1u) {
                    var nearest = this.system.GetNearestFirst(q.treeMask, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf == 1 ? true : false, q.ignoreY == 1 ? true : false);
                    if (nearest.IsAlive() == true) query.results.results.Add(nearest);
                } else {
                    this.system.GetNearest(q.treeMask, data.nearestCount, ref query.results.results, in ent, in worldPos, in sector, q.minRangeSqr, q.rangeSqr, q.ignoreSelf == 1 ? true : false, query.readQuery.ignoreY == 1 ? true : false);
                }

            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var handle = context.Query().Schedule<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}