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

    public partial struct NativeQuadtree<T> : INativeDisposable where T : unmanaged, System.IComparable<T> {

        /// <summary>
        /// Perform a raycast against the quadtree
        /// </summary>
        /// <param name="ray">Input ray</param>
        /// <param name="hit">The resulting hit</param>
        /// <param name="intersecter">Delegate to compute ray intersections against the objects or AABB's</param>
        /// <param name="maxDistance">Max distance from the ray's origin a hit may occur</param>
        /// <typeparam name="U">Type of intersecter</typeparam>
        /// <returns>True when a hit has occured</returns>
        public bool Raycast<U>(Ray2D ray, out QuadtreeRaycastHit<T> hit, U intersecter = default) where U : struct, IQuadtreeRayIntersecter<T> {
            return this.Raycast(ray, out hit, intersecter, tfloat.PositiveInfinity);
        }
        public bool Raycast<U>(Ray2D ray, out QuadtreeRaycastHit<T> hit, U intersecter, tfloat maxDistance)
            where U : struct, IQuadtreeRayIntersecter<T> {
            var computedRay = new PrecomputedRay2D(ray);

            // check if ray even hits the boundary, and if so, we use the intersectin point to transpose our ray
            if (!this.bounds.IntersectsRay(computedRay.origin, computedRay.invDir, out var tMin) || tMin > maxDistance) {
                hit = default;
                return false;
            }

            maxDistance -= tMin;
            var rayPos = computedRay.origin + computedRay.dir * tMin;

            // Note: transpose computed ray to boundary and go
            return this.RaycastNext(
                ray: new PrecomputedRay2D(computedRay, rayPos),
                nodeId: 1,
                extentsBounds: new ExtentsBounds(this.boundsCenter, this.boundsExtents),
                hit: out hit,
                visitor: ref intersecter,
                maxDistance: maxDistance,
                parentDepth: 0);
        }

        private bool RaycastNext<U>(
            in PrecomputedRay2D ray,
            uint nodeId, in ExtentsBounds extentsBounds,
            out QuadtreeRaycastHit<T> hit,
            ref U visitor, tfloat maxDistance,
            int parentDepth)
            where U : struct, IQuadtreeRayIntersecter<T> {
            parentDepth++;

            // Reference for the method used to determine the order of octants to visit
            // https://daeken.svbtle.com/a-stupidly-simple-fast-quadtree-traversal-for-ray-intersection

            // Compute the bounds of the parent node we're in, we use it to check if a plane intersection is valid
            // var parentBounds = ExtentsBounds.GetBounds(extentsBounds);

            // compute the plane intersections
            var planeHits = PlaneHits(ray, extentsBounds.nodeCenter);

            // for our first (closest) octant, it must be the position the ray entered the parent node
            var quadIndex = PointToQuadIndex(ray.origin, extentsBounds.nodeCenter);
            var quadRayIntersection = ray.origin;
            tfloat quadDistance = 0;

            for (var i = 0; i < 3; i++) {
                var quadId = GetQuadId(nodeId, quadIndex);

                #if DEBUG_RAYCAST_GIZMO
                var debugExt = ExtentsBounds.GetOctant(extentsBounds, octantIndex);
                var color = new Color(0, 0, 0, .25f);
                color[i] = 1f;
                UnityEngine.Gizmos.color = color;
                UnityEngine.Gizmos.DrawCube((Vector2)debugExt.nodeCenter, (Vector2)debugExt.nodeExtents * 1.75f);
                #endif

                if (this.nodes.TryGetValue(quadId, out var objectCount) && this.Raycast(
                        new PrecomputedRay2D(ray, quadRayIntersection),
                        quadId,
                        ExtentsBounds.GetQuad(extentsBounds, quadIndex),
                        objectCount,
                        out hit,
                        ref visitor,
                        maxDistance - quadDistance,
                        parentDepth)) {
                    return true;
                }

                // find next octant to test:

                var closestPlaneIndex = -1;
                var closestDistance = maxDistance;

                for (var j = 0; j < 2; j++) {
                    var t = planeHits[j];
                    if (t > closestDistance || t < 0) {
                        continue; // negative t is backwards
                    }

                    var planeRayIntersection = ray.origin + t * ray.dir;
                    //if (parentBounds.Contains(planeRayIntersection))
                    if (extentsBounds.Contains(planeRayIntersection)) {
                        quadRayIntersection = planeRayIntersection;
                        closestPlaneIndex = j;
                        closestDistance = quadDistance = (float)t;

                        #if DEBUG_RAYCAST_GIZMO
                        var debugExt2 = ExtentsBounds.GetOctant(extentsBounds, octantIndex);
                        var color2 = new Color(0, 0, 0, .25f);
                        color2[i] = 1f;
                        UnityEngine.Gizmos.color = color2;
                        UnityEngine.Gizmos.DrawSphere((Vector2)debugExt2.nodeCenter, .25f);
                        #endif
                    }
                }

                // No valid octant intersections left, bail
                if (closestPlaneIndex == -1) {
                    break;
                }

                // get next octant from plane index
                quadIndex ^= 1 << closestPlaneIndex;
                planeHits[closestPlaneIndex] = float.PositiveInfinity;
            }

            hit = default;
            return false;
        }

        private bool Raycast<U>(in PrecomputedRay2D ray,
                                uint nodeId,
                                in ExtentsBounds extentsBounds,
                                int objectCount,
                                out QuadtreeRaycastHit<T> hit,
                                ref U visitor, tfloat maxDistance,
                                int depth) where U : struct, IQuadtreeRayIntersecter<T> {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                hit = default;
                var closest = maxDistance;
                var didHit = false;

                if (this.objects.TryGetFirstValue(nodeId, out var wrappedObj, out var it)) {
                    do {
                        if (visitor.IntersectRay(ray, wrappedObj.obj, wrappedObj.bounds, out var t) && t < closest) {
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
                ref visitor,
                maxDistance,
                depth);
        }

        /// <summary>
        /// Computes ray plane intersections compute of YZ, XZ and XY respectively stored in xyz
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 PlaneHits(in PrecomputedRay2D ray, float2 nodeCenter) {
            return (nodeCenter - ray.origin) * ray.invDir;
        }

    }

}