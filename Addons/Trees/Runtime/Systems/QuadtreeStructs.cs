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
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    
    public struct AABB2DDistanceSquaredProvider<T> : NativeTrees.IQuadtreeDistanceProvider<T> {
        // Just return the distance squared to our bounds
        [INLINE(256)]
        public tfloat DistanceSquared(in float2 point, in T obj, in NativeTrees.AABB2D bounds) => bounds.DistanceSquared(point);
    }

    public struct QuadtreeNearestIgnoreSelfAABBVisitor<T> : NativeTrees.IQuadtreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> {

        public T ignoreSelf;
        public T nearest;
        public bool found;
        public uint Capacity => 1u;
        
        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds) {

            if (this.ignoreSelf.Equals(obj) == true) return true;
            this.found = true;
            this.nearest = obj;
        
            return false; // immediately stop iterating at first hit
            // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
        }
    }

    public interface ISubFilter<T> where T : unmanaged {

        [INLINE(256)]
        bool IsValid(in T ent, in NativeTrees.AABB2D bounds);

    }

    public struct AlwaysTrueSubFilter : ISubFilter<Ent> {

        [INLINE(256)]
        public bool IsValid(in Ent ent, in NativeTrees.AABB2D bounds) => ent.IsAlive();

    }
    
    public struct QuadtreeNearestAABBVisitor<T, TSubFilter> : NativeTrees.IQuadtreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {

        public TSubFilter subFilter;
        public T nearest;
        public bool found;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;
        public uint Capacity => 1u;

        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds) {

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

    public struct QuadtreeKNearestAABBVisitor<T, TSubFilter> : NativeTrees.IQuadtreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {

        public TSubFilter subFilter;
        public UnsafeHashSet<T> results;
        public uint max;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;
        public uint Capacity => (uint)this.results.Capacity;

        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D bounds) {

            if (this.results.Contains(obj) == true) return true;

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
    
    public struct RangeAABB2DUniqueVisitor<T, TSubFilter> : NativeTrees.IQuadtreeRangeVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, ISubFilter<T> {
        
        public TSubFilter subFilter;
        public UnsafeHashSet<T> results;
        public tfloat rangeSqr;
        public uint max;
        public MathSector sector;
        public bool ignoreSelf;
        public T ignore;

        [INLINE(256)]
        public bool OnVisit(in T obj, in NativeTrees.AABB2D objBounds, in NativeTrees.AABB2D queryRange) {

            if (this.results.Contains(obj) == true) return true;
            
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