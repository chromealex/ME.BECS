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

    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    
    /// <summary>
    /// Defines aabb2 d spatial distance squared provider state and operations.
    /// </summary>
    public struct AABB2DSpatialDistanceSquaredProvider<T> : NativeTrees.ISpatialDistanceProvider<T> {
        // Just return the distance squared to our bounds
        /// <summary>
        /// Computes the squared distance without taking a square root.
        /// </summary>
        [INLINE(256)]
        public tfloat DistanceSquared(in float2 point, in T obj, in NativeTrees.AABB2D bounds) => bounds.DistanceSquared(point);
    }

    /// <summary>
    /// Processes candidates during spatial nearest ignore self aabb traversal.
    /// </summary>
    public struct SpatialNearestIgnoreSelfAABBVisitor<T> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        /// <summary>
        /// Whether the query excludes the entity that initiated it.
        /// </summary>
        public T ignoreSelf;
        /// <summary>
        /// Nearest candidate selected by the query.
        /// </summary>
        public T nearest;
        /// <summary>
        /// Squared nearest distance used by the associated calculation.
        /// </summary>
        public tfloat nearestDistanceSqr;
        /// <summary>
        /// Whether the associated search found a matching entry.
        /// </summary>
        public bool found;
        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => 1u;
        
        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds, tfloat distanceSqr) {

            if (this.ignoreSelf.Equals(obj) == true) return true;
            this.found = true;
            this.nearest = obj;
            this.nearestDistanceSqr = distanceSqr;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    /// <summary>
    /// Defines the operations required by spatial sub filter.
    /// </summary>
    public interface ISpatialSubFilter<T> where T : unmanaged {

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        bool IsValid(in T ent, in NativeTrees.AABB2D bounds);

    }

    /// <summary>
    /// Filters candidates according to the always true spatial sub filter condition.
    /// </summary>
    public struct AlwaysTrueSpatialSubFilter : ISpatialSubFilter<Ent> {

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        [INLINE(256)]
        public bool IsValid(in Ent ent, in NativeTrees.AABB2D bounds) => ent.IsAlive();

    }

    /// <summary>
    /// Defines spatial query candidate state and operations.
    /// </summary>
    public struct SpatialQueryCandidate<T> : System.IComparable<SpatialQueryCandidate<T>> where T : unmanaged, System.IComparable<T> {

        /// <summary>
        /// Object represented by this entry.
        /// </summary>
        public T obj;
        /// <summary>
        /// Squared distance used by the associated calculation.
        /// </summary>
        public tfloat distanceSqr;

        /// <summary>
        /// Compares this value with the supplied value for sorting.
        /// </summary>
        [INLINE(256)]
        public int CompareTo(SpatialQueryCandidate<T> other) {
            if (this.distanceSqr < other.distanceSqr) return -1;
            if (this.distanceSqr > other.distanceSqr) return 1;
            return this.obj.CompareTo(other.obj);
        }

    }

    /// <summary>
    /// Processes candidates during spatial k nearest fixed aabb traversal.
    /// </summary>
    public struct SpatialKNearestFixedAABBVisitor<T, TSubFilter> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T>, System.IComparable<T> where TSubFilter : struct, ISpatialSubFilter<T> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public FixedList512Bytes<SpatialQueryCandidate<T>> results;
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
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => (uint)this.results.Capacity;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds, tfloat distanceSqr) {
            if (this.subFilter.IsValid(in obj, in bounds) == false) return true;
            if (this.ignoreSelf == true && this.ignore.Equals(obj) == true) return true;
            if (this.sector.IsValid(bounds.Center) == false) return true;

            var candidate = new SpatialQueryCandidate<T>() {
                obj = obj,
                distanceSqr = distanceSqr,
            };
            if ((uint)this.results.Length < this.max) {
                this.results.Add(candidate);
            } else {
                var worstIndex = 0;
                var worst = this.results[0];
                for (int i = 1; i < this.results.Length; ++i) {
                    var item = this.results[i];
                    if (item.CompareTo(worst) > 0) {
                        worstIndex = i;
                        worst = item;
                    }
                }
                if (candidate.CompareTo(worst) < 0) this.results[worstIndex] = candidate;
            }
            return true;
        }

    }

    /// <summary>
    /// Processes candidates during spatial k nearest direct aabb traversal.
    /// </summary>
    public unsafe struct SpatialKNearestDirectAABBVisitor<TSubFilter> : NativeTrees.ISpatialNearestVisitor<Ent> where TSubFilter : struct, ISpatialSubFilter<Ent> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public safe_ptr<QueryResults> results;
        /// <summary>
        /// Maximum .
        /// </summary>
        public uint max;
        /// <summary>
        /// Number of entries tracked by this value.
        /// </summary>
        public uint count;
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
        public Ent ignore;
        /// <summary>
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => this.max;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in Ent obj, in NativeTrees.AABB2D bounds, tfloat distanceSqr) {
            if (this.count >= this.max) return false;
            if (this.subFilter.IsValid(in obj, in bounds) == false) return true;
            if (this.ignoreSelf == true && this.ignore.Equals(obj) == true) return true;
            if (this.sector.IsValid(bounds.Center) == false) return true;
            this.results.ptr->Add(obj);
            return ++this.count < this.max;
        }

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        [INLINE(256)]
        public void Reset() => this.count = 0u;

    }

    /// <summary>
    /// Processes candidates during range aabb2 d spatial direct traversal.
    /// </summary>
    public unsafe struct RangeAABB2DSpatialDirectVisitor<TSubFilter> : NativeTrees.ISpatialRangeVisitor<Ent> where TSubFilter : struct, ISpatialSubFilter<Ent> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public safe_ptr<QueryResults> results;
        /// <summary>
        /// Squared range used for distance comparisons without a square root.
        /// </summary>
        public tfloat rangeSqr;
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
        public Ent ignore;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in Ent obj, in NativeTrees.AABB2D objBounds, in NativeTrees.AABB2D queryRange) {
            if (this.subFilter.IsValid(in obj, in objBounds) == false) return true;
            if (this.ignoreSelf == true && this.ignore.Equals(obj) == true) return true;
            if (this.sector.IsValid(objBounds.Center) == false) return true;
            var distanceSqr = objBounds.DistanceSquared(queryRange.Center);
            if (objBounds.Overlaps(queryRange) == true && distanceSqr <= this.rangeSqr) this.results.ptr->Add(obj);
            return true;
        }

    }
    
    /// <summary>
    /// Processes candidates during spatial nearest aabb traversal.
    /// </summary>
    public struct SpatialNearestAABBVisitor<T, TSubFilter> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISpatialSubFilter<T> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Nearest candidate selected by the query.
        /// </summary>
        public T nearest;
        /// <summary>
        /// Squared nearest distance used by the associated calculation.
        /// </summary>
        public tfloat nearestDistanceSqr;
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
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => 1u;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds, tfloat distanceSqr) {

            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }

            if (this.sector.IsValid(bounds.Center) == false) {
                return true;
            }

            if (this.subFilter.IsValid(in obj, in bounds) == false) {
                return true;
            } 

            this.found = true;
            this.nearest = obj;
            this.nearestDistanceSqr = distanceSqr;

            return false; // false to immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        [INLINE(256)]
        public void Reset() {
            this.found = false;
            this.nearest = default;
            this.nearestDistanceSqr = default;
        }

    }

    /// <summary>
    /// Processes candidates during spatial k nearest aabb traversal.
    /// </summary>
    public struct SpatialKNearestAABBVisitor<T, TSubFilter> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T>, System.IComparable<T> where TSubFilter : struct, ISpatialSubFilter<T> {

        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public UnsafeList<SpatialQueryCandidate<T>> results;
        /// <summary>
        /// Maximum .
        /// </summary>
        public uint max;
        /// <summary>
        /// Whether stop when full behavior or state is selected.
        /// </summary>
        public bool stopWhenFull;
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
        /// Number of elements that fit in the currently reserved storage.
        /// </summary>
        public uint Capacity => (uint)this.results.Capacity;

        /// <summary>
        /// Examines a candidate encountered by a spatial traversal.
        /// </summary>
        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds, tfloat distanceSqr) {

            if (this.subFilter.IsValid(in obj, in bounds) == false) {
                return true;
            } 
            
            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }
            
            if (this.sector.IsValid(bounds.Center) == true) {
                var candidate = new SpatialQueryCandidate<T>() {
                    obj = obj,
                    distanceSqr = distanceSqr,
                };
                if (this.max == 0u || this.results.Length < this.max) {
                    this.results.Add(candidate);
                    if (this.stopWhenFull == true && this.results.Length == this.max) return false;
                } else {
                    var worstIndex = 0;
                    var worst = this.results[0];
                    for (int i = 1; i < this.results.Length; ++i) {
                        var item = this.results[i];
                        if (item.distanceSqr > worst.distanceSqr || (item.distanceSqr == worst.distanceSqr && item.obj.CompareTo(worst.obj) > 0)) {
                            worstIndex = i;
                            worst = item;
                        }
                    }

                    if (distanceSqr < worst.distanceSqr || (distanceSqr == worst.distanceSqr && obj.CompareTo(worst.obj) < 0)) {
                        this.results[worstIndex] = candidate;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        [INLINE(256)]
        public void Reset() {
            this.results.Clear();
        }

    }
    
    /// <summary>
    /// Processes candidates during range aabb2 d spatial unique traversal.
    /// </summary>
    public struct RangeAABB2DSpatialUniqueVisitor<T, TSubFilter> : NativeTrees.ISpatialRangeVisitor<T> where T : unmanaged, System.IEquatable<T>, System.IComparable<T> where TSubFilter : struct, ISpatialSubFilter<T> {
        
        /// <summary>
        /// Additional predicate applied to spatial query candidates.
        /// </summary>
        public TSubFilter subFilter;
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public UnsafeList<SpatialQueryCandidate<T>> results;
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
        public bool OnVisit(in T obj, in NativeTrees.AABB2D objBounds, in NativeTrees.AABB2D queryRange) {

            if (this.subFilter.IsValid(in obj, in objBounds) == false) {
                return true;
            } 

            if (this.ignoreSelf == true) {
                if (this.ignore.Equals(obj) == true) return true;
            }

            if (this.sector.IsValid(objBounds.Center) == true) {
                // check if our object's AABB overlaps with the query AABB
                var distanceSqr = objBounds.DistanceSquared(queryRange.Center);
                if (objBounds.Overlaps(queryRange) == true && distanceSqr <= this.rangeSqr) {
                    this.results.Add(new SpatialQueryCandidate<T>() {
                        obj = obj,
                        distanceSqr = distanceSqr,
                    });
                    if (this.max > 0u && this.results.Length == this.max) return false;
                }
            }

            return true; // keep iterating
        }

        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        [INLINE(256)]
        public void Reset() {
            this.results.Clear();
        }

    }
    
}
