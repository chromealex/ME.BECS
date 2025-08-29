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

namespace ME.BECS {

    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    
    public struct AABBDistanceSquaredProvider<T> : NativeTrees.IOctreeDistanceProvider<T> {
        public bool ignoreY;
        // Just return the distance squared to our bounds
        public tfloat DistanceSquared(float3 point, T obj, NativeTrees.AABB bounds) => bounds.DistanceSquared(point, this.ignoreY);
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

    public interface IOctreeSubFilter<T> where T : unmanaged {

        bool IsValid(in T ent, in NativeTrees.AABB bounds);

    }

    public struct AlwaysTrueOctreeSubFilter : IOctreeSubFilter<Ent> {

        public bool IsValid(in Ent ent, in NativeTrees.AABB bounds) => ent.IsAlive();

    }
    
    public struct OctreeNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {

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

    public struct OctreeKNearestAABBVisitor<T, TSubFilter> : NativeTrees.IOctreeNearestVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {

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
    
    public struct RangeAABBUniqueVisitor<T, TSubFilter> : NativeTrees.IOctreeRangeVisitor<T> where T : unmanaged, System.IEquatable<T> where TSubFilter : struct, IOctreeSubFilter<T> {
        
        public TSubFilter subFilter;
        public UnsafeHashSet<T> results;
        public tfloat rangeSqr;
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
                    objBounds.DistanceSquared(queryRange.Center) <= this.rangeSqr) {
                    this.results.Add(obj);
                    if (this.max > 0u && this.results.Count == this.max) return false;
                }
            }

            return true; // keep iterating
        }
    }
    
}