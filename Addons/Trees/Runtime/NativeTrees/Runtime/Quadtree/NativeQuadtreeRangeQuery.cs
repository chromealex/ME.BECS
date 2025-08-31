using System.Runtime.CompilerServices;
using Unity.Collections;

// https://bartvandesande.nl
// https://github.com/bartofzo

namespace NativeTrees {

    public partial struct NativeQuadtree<T> : INativeDisposable where T : unmanaged, System.IComparable<T> {

        /// <summary>
        /// Visits all objects that are contained in the quadtree leafs that overlap with a range.
        /// Does not check if the object's bounds overlap, that should be implemented on the visitor delegate.
        /// </summary>
        /// <param name="range"></param>
        /// <param name="visitor"></param>
        /// <typeparam name="U"></typeparam>
        /// <remarks>It's possible for objects to be visited multiple times if their bounds span multiple leafs</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Range<U>(in AABB2D range, ref U visitor) where U : struct, IQuadtreeRangeVisitor<T> {
            this.RangeNext(
                in range,
                1,
                new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize),
                ref visitor,
                0);
        }

        private bool RangeNext<U>(in AABB2D range, uint nodeId, in QuarterSizeBounds quarterSizeBounds, ref U visitor, int parentDepth)
            where U : struct, IQuadtreeRangeVisitor<T> {
            parentDepth++;
            var rangeMask = GetBoundsMask(in quarterSizeBounds.nodeCenter, in range);

            for (var i = 0; i < 4; i++) {
                var quadMask = QuadMasks[i];
                if ((rangeMask & quadMask) == quadMask) {
                    var octantId = GetQuadId(nodeId, i);
                    if (this.TryGetNode(octantId, out var objectCount) == true &&
                        this.Range(
                            in range,
                            octantId,
                            QuarterSizeBounds.GetQuad(quarterSizeBounds, i),
                            objectCount,
                            ref visitor,
                            parentDepth) == false) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool Range<U>(in AABB2D range, uint nodeId, in QuarterSizeBounds quarterSizeBounds, int objectCount, ref U visitor, int depth)
            where U : struct, IQuadtreeRangeVisitor<T> {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                if (this.objects.TryGetFirstValue(nodeId, out var wrappedObj, out var it)) {
                    do {
                        if (visitor.OnVisit(wrappedObj.obj, wrappedObj.bounds, range) == false) {
                            return false; // stop traversing if visitor says so
                        }
                    } while (this.objects.TryGetNextValue(out wrappedObj, ref it));
                }

                return true;
            }

            return this.RangeNext(
                in range,
                nodeId,
                in quarterSizeBounds,
                ref visitor,
                depth);
        }

    }

}