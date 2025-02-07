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

using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

// https://bartvandesande.nl
// https://github.com/bartofzo

namespace NativeTrees {

    public partial struct NativeOctree<T> : INativeDisposable {

        /// <summary>
        /// Perform a raycast against the octree
        /// </summary>
        /// <param name="ray">Input ray</param>
        /// <param name="hit">The resulting hit</param>
        /// <param name="intersecter">Delegate to compute ray intersections against the objects or AABB's</param>
        /// <param name="maxDistance">Max distance from the ray's origin a hit may occur</param>
        /// <typeparam name="U">Type of intersecter</typeparam>
        /// <returns>True when a hit has occured</returns>
        public bool Raycast<U>(Ray ray, out OctreeRaycastHit<T> hit, U intersecter = default, tfloat? maxDistance = null)
            where U : struct, IOctreeRayIntersecter<T> {
            
            if (maxDistance == null) maxDistance = tfloat.PositiveInfinity;
            
            var computedRay = new PrecomputedRay(ray);

            // check if ray even hits the boundary, and if so, we use the intersectin point to transpose our ray
            if (!this.bounds.IntersectsRay(computedRay.origin, computedRay.invDir, out var tMin) || tMin > maxDistance) {
                hit = default;
                return false;
            }

            maxDistance -= tMin;
            var rayPos = computedRay.origin + computedRay.dir * tMin;

            // Note: transpose computed ray to boundary and go
            return this.RaycastNext(
                new PrecomputedRay(computedRay, rayPos),
                1,
                new ExtentsBounds(this.boundsCenter, this.boundsExtents),
                out hit,
                ref intersecter,
                maxDistance: maxDistance.Value,
                parentDepth: 0);
        }

        private bool RaycastNext<U>(
            in PrecomputedRay ray,
            uint nodeId, in ExtentsBounds extentsBounds,
            out OctreeRaycastHit<T> hit,
            ref U intersecter,
            int parentDepth, tfloat maxDistance)
            where U : struct, IOctreeRayIntersecter<T> {
            parentDepth++;

            // Reference for the method used to determine the order of octants to visit
            // https://daeken.svbtle.com/a-stupidly-simple-fast-octree-traversal-for-ray-intersection

            // Compute the bounds of the parent node we're in, we use it to check if a plane intersection is valid
            // var parentBounds = ExtentsBounds.GetBounds(extentsBounds);

            // compute the plane intersections of YZ, XZ and XY
            var planeHits = NativeOctree<T>.PlaneHits(ray, extentsBounds.nodeCenter);

            // for our first (closest) octant, it must be the position the ray entered the parent node
            var octantIndex = NativeOctree<T>.PointToOctantIndex(ray.origin, extentsBounds.nodeCenter);
            var octantRayIntersection = ray.origin;
            tfloat octantDistance = 0;

            for (var i = 0; i < 4; i++) {
                var octantId = NativeOctree<T>.GetOctantId(nodeId, octantIndex);
                if (this.nodes.TryGetValue(octantId, out var objectCount) && this.Raycast(
                        new PrecomputedRay(ray, octantRayIntersection),
                        octantId,
                        ExtentsBounds.GetOctant(extentsBounds, octantIndex),
                        objectCount,
                        out hit,
                        ref intersecter,
                        maxDistance - octantDistance,
                        parentDepth)) {
                    return true;
                }

                // find next octant to test:
                var closestDistance = maxDistance; //float.PositiveInfinity;
                var closestPlaneIndex = -1;

                for (var j = 0; j < 3; j++) {
                    var t = planeHits[j];
                    if (t > closestDistance || t < 0) {
                        continue; // negative t is backwards
                    }

                    var planeRayIntersection = ray.origin + t * ray.dir;
                    //if (parentBounds.Contains(planeRayIntersection))
                    if (extentsBounds.Contains(planeRayIntersection)) {
                        octantRayIntersection = planeRayIntersection;
                        closestPlaneIndex = j;
                        closestDistance = octantDistance = t;
                    }
                }

                // No valid octant intersections left, bail
                if (closestPlaneIndex == -1) {
                    break;
                }

                // get next octant from plane index
                octantIndex ^= 1 << closestPlaneIndex;
                planeHits[closestPlaneIndex] = float.PositiveInfinity;
            }

            hit = default;
            return false;
        }

        private bool Raycast<U>(in PrecomputedRay ray,
                                uint nodeId,
                                in ExtentsBounds extentsBounds,
                                int objectCount,
                                out OctreeRaycastHit<T> hit,
                                ref U intersecter, tfloat maxDistance,
                                int depth) where U : struct, IOctreeRayIntersecter<T> {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                hit = default;
                var closest = maxDistance;
                var didHit = false;

                if (this.objects.TryGetFirstValue(nodeId, out var wrappedObj, out var it)) {
                    do {
                        if (intersecter.IntersectRay(ray, wrappedObj.obj, wrappedObj.bounds, out var t) && t < closest) {
                            closest = t;
                            hit.obj = wrappedObj.obj;
                            didHit = true;
                        }
                    } while (this.objects.TryGetNextValue(out wrappedObj, ref it));
                }

                if (didHit) {
                    hit.point = ray.origin + ray.dir * closest;
                    return true;
                }

                return false;
            }

            return this.RaycastNext(
                ray,
                nodeId,
                extentsBounds,
                out hit,
                ref intersecter,
                maxDistance: maxDistance,
                parentDepth: depth);
        }

        /// <summary>
        /// Computes ray plane intersections compute of YZ, XZ and XY respectively stored in xyz
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 PlaneHits(in PrecomputedRay ray, float3 nodeCenter) {
            return (nodeCenter - ray.origin) * ray.invDir;
        }

    }

}