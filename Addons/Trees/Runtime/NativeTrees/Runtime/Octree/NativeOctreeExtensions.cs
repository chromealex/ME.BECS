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

using System;
using Unity.Collections;
using UnityEngine;

namespace NativeTrees {
    
    using ME.BECS;

    /// <summary>
    /// Convenience queries that operate just on the object's bounding boxes
    /// </summary>
    public static class NativeOctreeExtensions {

        /// <summary>
        /// Performs a raycast on the octree just using the bounds of the objects in it
        /// </summary>
        public static bool RaycastAABB<T>(this NativeOctree<T> octree, Ray ray, out OctreeRaycastHit<T> hit) where T : unmanaged, IComparable<T> {
            return octree.RaycastAABB<T>(ray, out hit, tfloat.PositiveInfinity);
        }
        
        /// <summary>
        /// Performs a raycast on the octree just using the bounds of the objects in it
        /// </summary>
        public static bool RaycastAABB<T>(this NativeOctree<T> octree, Ray ray, out OctreeRaycastHit<T> hit, tfloat maxDistance) where T : unmanaged, IComparable<T> {
            return octree.Raycast<RayAABBIntersecter<T>>(ray, out hit, maxDistance: maxDistance);
        }

        private struct RayAABBIntersecter<T> : IOctreeRayIntersecter<T> {

            public bool IntersectRay(in PrecomputedRay ray, T obj, AABB objBounds, out tfloat distance) {
                return objBounds.IntersectsRay(ray, out distance);
            }

        }

        /// <summary>
        /// Appends all objects for which their AABB overlaps with the input range to the results list.
        /// </summary>
        /// <param name="octree"></param>
        /// <param name="range"></param>
        /// <param name="results"></param>
        /// <typeparam name="T"></typeparam>
        /// <remarks>Note that objects can be added to the list more than once if their bounds overlap multiple octree leafs</remarks>
        public static void RangeAABB<T>(this NativeOctree<T> octree, AABB range, NativeList<T> results) where T : unmanaged, IComparable<T> {
            var vistor = new RangeAABBVisitor<T>() {
                results = results,
            };

            octree.Range(range, ref vistor);
        }

        private struct RangeAABBVisitor<T> : IOctreeRangeVisitor<T> where T : unmanaged {

            public NativeList<T> results;

            public bool OnVisit(T obj, AABB objBounds, AABB queryRange) {
                if (objBounds.Overlaps(queryRange)) {
                    this.results.Add(obj);
                }

                return true; // always keep iterating, we want to catch all objects
            }

        }

        /// <summary>
        /// Appends all objects for which their AABB overlaps with the input range to the results set.
        /// This guarantuees that each object will only appear once.
        /// </summary>
        /// <param name="octree"></param>
        /// <param name="range"></param>
        /// <param name="results"></param>
        /// <typeparam name="T"></typeparam>
        public static void RangeAABBUnique<T>(this NativeOctree<T> octree, AABB range, NativeParallelHashSet<T> results) where T : unmanaged, IEquatable<T>, IComparable<T> {
            var vistor = new RangeAABBUniqueVisitor<T>() {
                results = results,
            };

            octree.Range(range, ref vistor);
        }

        private struct RangeAABBUniqueVisitor<T> : IOctreeRangeVisitor<T> where T : unmanaged, IEquatable<T> {

            public NativeParallelHashSet<T> results;

            public bool OnVisit(T obj, AABB objBounds, AABB queryRange) {
                if (objBounds.Overlaps(queryRange)) {
                    this.results.Add(obj);
                }

                return true; // always keep iterating, we want to catch all objects
            }

        }

        /// <summary>
        /// Finds the nearest object to a given point (based on it's bounding box)
        /// </summary>
        /// <param name="octree">The octree</param>
        /// <param name="point">Point to find nearest neighbour from</param>
        /// <param name="maxDistance">Max distance to limit the search</param>
        /// <param name="nearest">The nearest object found</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>If an object was found within the given maximum distance</returns>
        public static bool TryGetNearestAABB<T>(this NativeOctree<T> octree, float3 point, tfloat minDistanceSqr, tfloat maxDistanceSqr, out T nearest)
            where T : unmanaged, IComparable<T> {
            var visitor = new OctreeNearestAABBVisitor<T>();
            octree.Nearest(point, minDistanceSqr, maxDistanceSqr, ref visitor, default(AABBDistanceSquaredProvider<T>));
            nearest = visitor.nearest;
            return visitor.found;
        }

        /// <summary>
        /// Finds the nearest object to a given point (based on it's bounding box)
        /// </summary>
        /// <param name="queryCache">The already allocated query cache</param>
        /// <param name="octree">The octree</param>
        /// <param name="point">Point to find nearest neighbour from</param>
        /// <param name="maxDistance">Max distance to limit the search</param>
        /// <param name="nearest">The nearest object found</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>If an object was found within the given maximum distance</returns>
        public static bool TryGetNearestAABB<T>(this NativeOctree<T>.NearestNeighbourCache queryCache, ref NativeOctree<T> octree, float3 point, tfloat minDistanceSqr,
                                                tfloat maxDistanceSqr, out T nearest) where T : unmanaged, IComparable<T> {
            var visitor = new OctreeNearestAABBVisitor<T>();
            queryCache.Nearest(ref octree, point, minDistanceSqr, maxDistanceSqr, ref visitor, default(AABBDistanceSquaredProvider<T>));
            nearest = visitor.nearest;
            return visitor.found;
        }

        private struct AABBDistanceSquaredProvider<T> : IOctreeDistanceProvider<T> {

            public tfloat DistanceSquared(float3 point, T obj, AABB bounds) {
                return bounds.DistanceSquared(point);
            }

        }

        private struct OctreeNearestAABBVisitor<T> : IOctreeNearestVisitor<T> {

            public T nearest;
            public bool found;

            public bool OnVisit(T obj, AABB bounds) {
                this.found = true;
                this.nearest = obj;

                return false; // immediately stop iterating at first hit
                // if we want the 2nd or 3rd neighbour, we could iterate on and keep track of the count!
            }

        }

    }

}