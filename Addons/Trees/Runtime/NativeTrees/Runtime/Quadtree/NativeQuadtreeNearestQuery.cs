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

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;

// https://bartvandesande.nl
// https://github.com/bartofzo

namespace NativeTrees {

    public partial struct NativeQuadtree<T> : INativeDisposable where T : unmanaged, System.IComparable<T> {

        /// <summary>
        /// Perform a nearest neighbour query. 
        /// </summary>
        /// <param name="point">Point to get nearest neighbours for</param>
        /// <param name="maxDistance">Maximum distance to look</param>
        /// <param name="visitor">Handler for when a neighbour is encountered</param>
        /// <param name="distanceSquaredProvider">Provide a calculation for the distance</param>
        /// <typeparam name="U">Handler type for when a neighbour is encountered</typeparam>
        /// <typeparam name="V">Provide a calculation for the distance</typeparam>
        /// <remarks>Allocates native containers. To prevent reallocating for every query, create a <see cref="NearestNeighbourQuery"/> struct
        /// and re-use it.</remarks>
        public void Nearest<U, V>(float2 point, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor, V distanceSquaredProvider = default)
            where U : struct, IQuadtreeNearestVisitor<T>
            where V : struct, IQuadtreeDistanceProvider<T> {
            var query = new NearestNeighbourQuery(Allocator.Temp);
            query.Nearest(ref this, point, minDistanceSqr, maxDistanceSqr, ref visitor, distanceSquaredProvider);
        }

        /// <summary>
        /// Struct to perform an N-nearest neighbour query on the tree.
        /// </summary>
        /// <remarks>Implemented as a struct because this type of query requires the use of some extra native containers.
        /// You can cache this struct and re-use it to circumvent the extra cost associated with allocating the internal containers.</remarks>
        public struct NearestNeighbourQuery : INativeDisposable {

            // objects and nodes are stored in a separate list, as benchmarking turned out,
            // putting everything in one big struct was much, much slower because of the large struct size
            // we want to keep the struct in the minheap as small as possibly as many comparisons and swaps take place there
            private NativeList<ObjWrapper> objList;
            private NativeList<NodeWrapper> nodeList;
            private ME.BECS.NativeCollections.NativeMinHeap<DistanceAndIndexWrapper> minHeap;

            public NearestNeighbourQuery(Allocator allocator) : this(8, allocator) { }

            public NearestNeighbourQuery(int initialCapacity, Allocator allocator) {
                this.nodeList = new NativeList<NodeWrapper>(initialCapacity, allocator);
                this.objList = new NativeList<ObjWrapper>(initialCapacity, allocator);
                this.minHeap = new ME.BECS.NativeCollections.NativeMinHeap<DistanceAndIndexWrapper>((uint)initialCapacity, allocator);
            }

            public void Dispose() {
                this.objList.Dispose();
                this.nodeList.Dispose();
                this.minHeap.Dispose();
            }

            public JobHandle Dispose(JobHandle inputDeps) {
                return JobHandle.CombineDependencies(this.objList.Dispose(inputDeps), this.nodeList.Dispose(inputDeps), this.minHeap.Dispose(inputDeps));
            }

            /// <summary>
            /// Perform a nearest neighbour query. 
            /// </summary>
            /// <param name="quadtree">quadtree to perform the query on</param>
            /// <param name="point">Point to get nearest neighbours for</param>
            /// <param name="maxDistance">Maximum distance to look</param>
            /// <param name="visitor">Handler for when a neighbour is encountered</param>
            /// <param name="distanceSquaredProvider">Provide a calculation for the distance</param>
            /// <typeparam name="U">Handler type for when a neighbour is encountered</typeparam>
            /// <typeparam name="V">Provide a calculation for the distance</typeparam>
            public void Nearest<U, V>(ref NativeQuadtree<T> quadtree, float2 point, tfloat minDistanceSqr, tfloat maxDistanceSqr, ref U visitor,
                                      V distanceSquaredProvider = default)
                where U : struct, IQuadtreeNearestVisitor<T>
                where V : struct, IQuadtreeDistanceProvider<T> {
                // reference for the method used:
                // https://stackoverflow.com/questions/41306122/nearest-neighbor-search-in-quadtree
                // - add root to priority queue
                // - pop queue, if it's an object, it's the closest one, if it's a node, add it's children to the queue
                // - repeat

                this.minHeap.Clear();
                this.nodeList.Clear();
                this.objList.Clear();

                var root = new NodeWrapper(
                    1,
                    0,
                    0,
                    new ExtentsBounds(quadtree.boundsCenter, quadtree.boundsExtents));

                // Add our first quads to the heap
                this.NearestNodeNext(
                    ref quadtree,
                    point,
                    ref root,
                    maxDistanceSqr,
                    0);

                while (this.minHeap.TryPop(out var nearestWrapper)) {
                    if (nearestWrapper.isNode) {
                        this.NearestNode(
                            ref quadtree,
                            point,
                            distanceAndIndexWrapper: nearestWrapper,
                            maxDistanceSquared: maxDistanceSqr,
                            distanceProvider: distanceSquaredProvider);
                    } else {
                        var item = this.objList[nearestWrapper.objIndex];
                        if (minDistanceSqr > 0f && math.distancesq(item.bounds.Center, point) <= minDistanceSqr) {
                            continue;
                        }

                        if (visitor.OnVisit(item.obj, item.bounds) == false) {
                            break;
                        }
                    }
                }
            }

            private void NearestNode<V>(ref NativeQuadtree<T> quadtree, float2 point, tfloat maxDistanceSquared, in DistanceAndIndexWrapper distanceAndIndexWrapper,
                                        V distanceProvider = default)
                where V : struct, IQuadtreeDistanceProvider<T> {
                ref var node = ref this.nodeList.ElementAt(distanceAndIndexWrapper.nodeIndex);
                ref var objects = ref quadtree.objects;

                // Leaf?
                if (node.nodeCounter <= quadtree.objectsPerNode || node.nodeDepth == quadtree.maxDepth) {
                    if (objects.TryGetFirstValue(node.nodeId, out var objWrapper, out var it)) {
                        do {
                            var objDistanceSquared = distanceProvider.DistanceSquared(point, objWrapper.obj, objWrapper.bounds);
                            if (objDistanceSquared > maxDistanceSquared) {
                                continue;
                            }

                            var objIndex = this.objList.Length;
                            this.objList.Add(objWrapper);

                            this.minHeap.Push(new DistanceAndIndexWrapper(
                                                  objDistanceSquared,
                                                  objIndex,
                                                  0,
                                                  false));

                        } while (objects.TryGetNextValue(out objWrapper, ref it));
                    }

                    return;
                }

                // Add child nodes
                this.NearestNodeNext(
                    ref quadtree,
                    point,
                    ref node,
                    maxDistanceSquared,
                    node.nodeDepth);
            }

            private void NearestNodeNext(ref NativeQuadtree<T> quadtree, float2 point, ref NodeWrapper nodeWrapper, tfloat maxDistanceSquared, int parentDepth) {
                parentDepth++;
                for (var i = 0; i < 4; i++) {
                    var quadId = GetQuadId(nodeWrapper.nodeId, i);
                    if (!quadtree.nodes.TryGetValue(quadId, out var quadObjectCount)) {
                        continue;
                    }

                    var quadCenterExtents = ExtentsBounds.GetQuad(nodeWrapper.ExtentsBounds, i);
                    var distanceSquared = ExtentsBounds.GetBounds(quadCenterExtents).DistanceSquared(point);

                    if (distanceSquared > maxDistanceSquared) {
                        continue;
                    }

                    var nodeIndex = this.nodeList.Length;
                    this.nodeList.Add(
                        new NodeWrapper(
                            quadId,
                            parentDepth,
                            quadObjectCount,
                            quadCenterExtents));

                    this.minHeap.Push(new DistanceAndIndexWrapper(
                                          distanceSquared,
                                          0,
                                          nodeIndex,
                                          true));
                }
            }

            /// <summary>
            /// Goes in the priority queue
            /// </summary>
            private struct DistanceAndIndexWrapper : ME.BECS.NativeCollections.IMinHeapNode {

                public readonly tfloat distanceSquared;

                // There's no polymorphism with HPC#, so this is our way around that
                public readonly int objIndex;
                public readonly int nodeIndex;
                public readonly bool isNode;

                public DistanceAndIndexWrapper(tfloat distanceSquared, int objIndex, int nodeIndex, bool isNode) {
                    this.distanceSquared = distanceSquared;
                    this.objIndex = objIndex;
                    this.nodeIndex = nodeIndex;
                    this.isNode = isNode;
                    this.Next = -1;
                }

                public tfloat ExpectedCost => this.distanceSquared;
                public int Next { get; set; }

            }

            private readonly struct NodeWrapper {

                public readonly uint nodeId;
                public readonly int nodeDepth;
                public readonly int nodeCounter;
                public readonly ExtentsBounds ExtentsBounds;

                public NodeWrapper(uint nodeId, int nodeDepth, int nodeCounter, in ExtentsBounds extentsBounds) {
                    this.nodeId = nodeId;
                    this.nodeDepth = nodeDepth;
                    this.nodeCounter = nodeCounter;
                    this.ExtentsBounds = extentsBounds;
                }

            }

            private struct NearestComp : IComparer<DistanceAndIndexWrapper> {

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int Compare(DistanceAndIndexWrapper x, DistanceAndIndexWrapper y) {
                    return x.distanceSquared.CompareTo(y.distanceSquared);
                }

            }

        }

    }

}