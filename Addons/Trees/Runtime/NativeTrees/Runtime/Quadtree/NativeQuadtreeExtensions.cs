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

// https://bartvandesande.nl
// https://github.com/bartofzo

namespace NativeTrees {

    /// <summary>
    /// Convenience queries that operate just on the object's bounding boxes
    /// </summary>
    public static class NativeQuadtreeExtensions {

        /// <summary>
        /// Performs a raycast on the octree just using the bounds of the objects in it
        /// </summary>
        public static bool RaycastAABB<T>(this NativeQuadtree<T> quadtree, Ray2D ray, out QuadtreeRaycastHit<T> hit) where T : unmanaged, IComparable<T> {
            return quadtree.RaycastAABB(ray, out hit, tfloat.PositiveInfinity);
        }

        /// <summary>
        /// Performs a raycast on the octree just using the bounds of the objects in it
        /// </summary>
        public static bool RaycastAABB<T>(this NativeQuadtree<T> quadtree, Ray2D ray, out QuadtreeRaycastHit<T> hit, tfloat maxDistance) where T : unmanaged, IComparable<T> {
            return quadtree.Raycast<RayAABBIntersecter<T>>(ray, out hit, intersecter: default, maxDistance: maxDistance);
        }

        private struct RayAABBIntersecter<T> : IQuadtreeRayIntersecter<T> where T : unmanaged {

            public bool IntersectRay(in PrecomputedRay2D ray, T obj, AABB2D objBounds, out tfloat distance) {
                return objBounds.IntersectsRay(ray.origin, ray.invDir, out distance);
            }

        }

        /// <summary>
        /// Appends all objects for which their AABB overlaps with the input range to the results list.
        /// </summary>
        /// <param name="quadtree"></param>
        /// <param name="range"></param>
        /// <param name="results"></param>
        /// <typeparam name="T"></typeparam>
        /// <remarks>Note that objects can be added to the list more than once if their bounds overlap multiple octree leafs</remarks>
        public static void RangeAABB<T>(this NativeQuadtree<T> quadtree, AABB2D range, NativeList<T> results) where T : unmanaged, IComparable<T> {
            var visitor = new RangeAABBVisitor<T>() {
                results = results,
            };

            quadtree.Range(range, ref visitor);
        }

        private struct RangeAABBVisitor<T> : IQuadtreeRangeVisitor<T> where T : unmanaged {

            public NativeList<T> results;

            public bool OnVisit(in T obj, in AABB2D objBounds, in AABB2D queryRange) {
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
        /// <param name="quadtree"></param>
        /// <param name="range"></param>
        /// <param name="results"></param>
        /// <typeparam name="T"></typeparam>
        public static void RangeAABBUnique<T>(this NativeQuadtree<T> quadtree, AABB2D range, NativeParallelHashSet<T> results) where T : unmanaged, IEquatable<T>, IComparable<T> {
            var vistor = new RangeAABBUniqueVisitor<T>() {
                results = results,
            };

            quadtree.Range(range, ref vistor);
        }

        private struct RangeAABBUniqueVisitor<T> : IQuadtreeRangeVisitor<T> where T : unmanaged, IEquatable<T> {

            public NativeParallelHashSet<T> results;

            public bool OnVisit(in T obj, in AABB2D objBounds, in AABB2D queryRange) {
                if (objBounds.Overlaps(queryRange)) {
                    this.results.Add(obj);
                }

                return true; // always keep iterating, we want to catch all objects
            }

        }

        /// <summary>
        /// Finds the nearest object to a given point (based on it's bounding box)
        /// </summary>
        /// <param name="quadtree">The tree</param>
        /// <param name="point">Point to find nearest neighbour from</param>
        /// <param name="maxDistance">Max distance to limit the search</param>
        /// <param name="nearest">The nearest object found</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>If an object was found within the given maximum distance</returns>
        public static bool TryGetNearestAABB<T>(this NativeQuadtree<T> quadtree, float2 point, tfloat maxDistanceSqr, tfloat minDistanceSqr, out T nearest) where T : unmanaged, IComparable<T> {
            var visitor = new QuadtreeNearestAABBVisitor<T>();
            var d = new AABBDistanceSquaredProvider<T>();
            quadtree.Nearest(point, maxDistanceSqr, minDistanceSqr, ref visitor, ref d);
            nearest = visitor.nearest;
            return visitor.found;
        }

        /// <summary>
        /// Finds the nearest object to a given point (based on it's bounding box)
        /// </summary>
        /// <param name="queryCache">The already allocated query cache</param>
        /// <param name="quadtree">The tree</param>
        /// <param name="point">Point to find nearest neighbour from</param>
        /// <param name="maxDistance">Max distance to limit the search</param>
        /// <param name="nearest">The nearest object found</param>
        /// <typeparam name="T"></typeparam>
        /// <returns>If an object was found within the given maximum distance</returns>
        public static bool TryGetNearestAABB<T>(this NativeQuadtree<T>.NearestNeighbourQuery queryCache, ref NativeQuadtree<T> quadtree, float2 point, tfloat maxDistanceSqr,
                                                tfloat minDistanceSqr, out T nearest) where T : unmanaged, IComparable<T> {
            var visitor = new QuadtreeNearestAABBVisitor<T>();
            var d = new AABBDistanceSquaredProvider<T>();
            queryCache.Nearest(ref quadtree, point, maxDistanceSqr, minDistanceSqr, ref visitor, ref d);
            nearest = visitor.nearest;
            return visitor.found;
        }

        public struct AABBDistanceSquaredProvider<T> : IQuadtreeDistanceProvider<T> where T : unmanaged {

            public tfloat DistanceSquared(in float2 point, in T obj, in AABB2D bounds) {
                return bounds.DistanceSquared(point);
            }

        }

        private struct QuadtreeNearestAABBVisitor<T> : IQuadtreeNearestVisitor<T> where T : unmanaged {

            public T nearest;
            public bool found;
            public uint Capacity => 1u;

            public bool OnVisit(in T obj, in AABB2D bounds) {
                this.found = true;
                this.nearest = obj;

                return false; // immediately stop iterating at first hit
            }

        }

    }

}