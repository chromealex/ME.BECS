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
    
    public struct AABB2DSpatialDistanceSquaredProvider<T> : NativeTrees.ISpatialDistanceProvider<T> {
        // Just return the distance squared to our bounds
        [INLINE(256)]
        public tfloat DistanceSquared(in float2 point, in T obj, in NativeTrees.AABB2D bounds) => bounds.DistanceSquared(point);
    }

    public struct SpatialNearestIgnoreSelfAABBVisitor<T> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        public T ignoreSelf;
        public T nearest;
        public tfloat nearestDistanceSqr;
        public bool found;
        public uint Capacity => 1u;
        
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

    public interface ISpatialSubFilter<T> where T : unmanaged {

        [INLINE(256)]
        bool IsValid(in T ent, in NativeTrees.AABB2D bounds);

    }

    public struct AlwaysTrueSpatialSubFilter : ISpatialSubFilter<Ent> {

        [INLINE(256)]
        public bool IsValid(in Ent ent, in NativeTrees.AABB2D bounds) => ent.IsAlive();

    }

    public struct SpatialQueryCandidate<T> where T : unmanaged {

        public T obj;
        public tfloat distanceSqr;

    }
    
    public struct SpatialNearestAABBVisitor<T, TSubFilter> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISpatialSubFilter<T> {

        public TSubFilter subFilter;
        public T nearest;
        public tfloat nearestDistanceSqr;
        public bool found;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;
        public uint Capacity => 1u;

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

        [INLINE(256)]
        public void Reset() {
            this.found = false;
            this.nearest = default;
            this.nearestDistanceSqr = default;
        }

    }

    public struct SpatialKNearestAABBVisitor<T, TSubFilter> : NativeTrees.ISpatialNearestVisitor<T> where T : unmanaged, System.IEquatable<T>, System.IComparable<T> where TSubFilter : struct, ISpatialSubFilter<T> {

        public TSubFilter subFilter;
        public UnsafeList<SpatialQueryCandidate<T>> results;
        public uint max;
        public bool stopWhenFull;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;
        public uint Capacity => (uint)this.results.Capacity;

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

        [INLINE(256)]
        public void Reset() {
            this.results.Clear();
        }

    }
    
    public struct RangeAABB2DSpatialUniqueVisitor<T, TSubFilter> : NativeTrees.ISpatialRangeVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISpatialSubFilter<T> {
        
        public TSubFilter subFilter;
        public UnsafeList<SpatialQueryCandidate<T>> results;
        public tfloat rangeSqr;
        public uint max;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;

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

        [INLINE(256)]
        public void Reset() {
            this.results.Clear();
        }

    }
    
}
