using System.Runtime.CompilerServices;
using Unity.Collections;

// https://bartvandesande.nl
// https://github.com/bartofzo

namespace NativeTrees {

    public partial struct NativeOctree<T> : INativeDisposable {

        /// <summary>
        /// Visits all objects that are contained in the octree leafs that overlap with a range.
        /// Does not check if the object's bounds overlap, that should be implemented on the visitor delegate.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="visitor"></param>
        /// <typeparam name="U"></typeparam>
        /// <remarks>It's possible for objects to be visited multiple times if their bounds span multiple leafs</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Range<U>(in AABB range, ref U visitor) where U : struct, IOctreeRangeVisitor<T> {
            this.RangeNext(
                range,
                1,
                new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize),
                ref visitor,
                0);
        }

        private bool RangeNext<U>(in AABB range, uint nodeId, in QuarterSizeBounds quarterSizeBounds, ref U visitor, int parentDepth) where U : struct, IOctreeRangeVisitor<T> {
            parentDepth++;
            var rangeMask = NativeOctree<T>.GetBoundsMask(quarterSizeBounds.nodeCenter, range);

            for (var i = 0; i < 8; i++) {
                var octantMask = NativeOctree<T>.OctantMasks[i];
                if ((rangeMask & octantMask) == octantMask) {
                    var octantId = NativeOctree<T>.GetOctantId(nodeId, i);
                    if (this.nodes.TryGetValue(octantId, out var objectCount) &&
                        !this.Range(
                            range,
                            octantId,
                            QuarterSizeBounds.GetOctant(quarterSizeBounds, i),
                            objectCount,
                            ref visitor,
                            parentDepth)) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool Range<U>(in AABB range, uint nodeId, in QuarterSizeBounds quarterSizeBounds, int objectCount, ref U visitor, int depth)
            where U : struct, IOctreeRangeVisitor<T> {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                if (this.objects.TryGetFirstValue(nodeId, out var wrappedObj, out var it)) {
                    do {
                        if (!visitor.OnVisit(wrappedObj.obj, wrappedObj.bounds, range)) {
                            return false; // stop traversing if visitor says so
                        }
                    } while (this.objects.TryGetNextValue(out wrappedObj, ref it));
                }

                return true;
            }

            return this.RangeNext(
                range,
                nodeId,
                quarterSizeBounds,
                ref visitor,
                depth);
        }

    }

}