#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif
#if INLINE_DISABLED
using INLINE = ME.BECS.NoInline;
#else
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
#endif

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    
    /// <summary>
    /// Defines aabb distance squared provider state and operations.
    /// </summary>
    public struct AABBDistanceSquaredProvider<T> : NativeTrees.IOctreeDistanceProvider<T> {
        /// <summary>
        /// Whether the spatial calculation ignores the vertical coordinate.
        /// </summary>
        public bool ignoreY;
        // Just return the distance squared to our bounds
        /// <summary>
        /// Computes the squared distance without taking a square root.
        /// </summary>
        [INLINE(256)]
        public tfloat DistanceSquared(float3 point, T obj, NativeTrees.AABB bounds) => bounds.DistanceSquared(point, this.ignoreY);
    }

    /// <summary>
    /// Processes candidates during octree nearest ignore self aabb traversal.
    /// </summary>
    public struct OctreeNearestIgnoreSelfAABBVisitor<T> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public T ignoreSelf;
        /// <summary>
        /// Nearest candidate selected by the query.
        /// </summary>
        public T nearest;
        /// <summary>
        /// Whether the associated search found a matching entry.
        /// </summary>
        public bool found;
        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(T obj, NativeTrees.AABB bounds) {

            if (this.ignoreSelf.Equals(obj) == true) return true;
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    /// <summary>
    /// Defines the operations required by octree sub filter.
    /// </summary>
    public interface IOctreeSubFilter<T> where T : unmanaged {

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        bool IsValid(in T ent, in NativeTrees.AABB bounds);

    }

    /// <summary>
    /// Filters candidates according to the always true octree sub filter condition.
    /// </summary>
    public struct AlwaysTrueOctreeSubFilter : IOctreeSubFilter<Ent> {

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid(in Ent ent, in NativeTrees.AABB bounds) => ent.IsAlive();

    }
    
    /// <summary>
    /// Processes candidates during octree nearest aabb traversal.
    /// </summary>
    public struct OctreeNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Nearest candidate selected by the query.
        /// </summary>
        public T nearest;
        /// <summary>
        /// Whether the associated search found a matching entry.
        /// </summary>
        public bool found;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public MathSector sector;
        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public bool ignoreSelf;
        /// <summary>
        /// Entries excluded from the associated operation.
        /// </summary>
        public T ignore;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
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

    /// <summary>
    /// Processes candidates during octree k nearest aabb traversal.
    /// </summary>
    public struct OctreeKNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public UnsafeHashSet<T> results;
        /// <summary>
        /// Maximum .
        /// </summary>
        public uint max;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public MathSector sector;
        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public bool ignoreSelf;
        /// <summary>
        /// Entries excluded from the associated operation.
        /// </summary>
        public T ignore;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
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
    
    /// <summary>
    /// Processes candidates during range aabb unique traversal.
    /// </summary>
    public struct RangeAABBUniqueVisitor<T, TSubFilter> : NativeTrees.IOctreeRangeVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {
        
        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public UnsafeHashSet<T> results;
        /// <summary>
        /// Squared range used for distance comparisons without a square root.
        /// </summary>
        public tfloat rangeSqr;
        /// <summary>
        /// Maximum .
        /// </summary>
        public uint max;
        /// <summary>
        /// Sector bounds used by the spatial query.
        /// </summary>
        public MathSector sector;
        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public bool ignoreSelf;
        /// <summary>
        /// Entries excluded from the associated operation.
        /// </summary>
        public T ignore;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
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
                    objBounds.DistanceSquared(queryRange.Center) <= this.rangeSqr) {
                    this.results.Add(obj);
                    if (this.max > 0u && this.results.Count == this.max) return false;
                }
            }

            return true; // keep iterating
        }
    }
    
}