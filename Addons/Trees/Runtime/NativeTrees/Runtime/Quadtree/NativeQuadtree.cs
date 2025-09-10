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
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using ME.BECS;
using static ME.BECS.Cuts;

// https://bartvandesande.nl
// https://github.com/bartofzo

// ReSharper disable StaticMemberInGenericType

namespace NativeTrees {

    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Generic Burst/ECS compatible sparse quadtree that stores objects together with their axis aligned bounding boxes (AABB2D's)
    /// Ported from <see cref="NativeOctree{T}"/>
    /// <para>
    /// Supported queries:
    ///     - Raycast
    ///     - Range (AABB2D overlap)
    ///     - Nearest neighbours
    /// </para>
    /// <para>
    /// Other features:
    ///     - Implemented as a sparse quadtree, so only stores nodes that are occupied,
    ///       allowing it to go to a max depth of 15 (this could be more if the nodeId's are stored as long values
    ///     - Supports insertion of AABB2D's
    ///     - Fast insertion for points
    ///     - Employs an extremely fast technique of checking for AABB2D / quad overlaps, see comment near the end
    ///     - Optimized with SIMD instructions so greatly benefits from burst compilation
    /// </para>
    /// <para>
    /// Limitations:
    ///     - No remove or update. Tried several approaches but they either left an unbalanced tree or doing a
    ///       full clear and re-insert was faster.
    /// </para>
    /// <para>
    /// Future todo's:
    ///     - Frustrum query
    ///     - 'Fat' raycast (virtually expand AABB2D's of nodes and objects when testing for ray intersections)
    /// </para>
    /// </summary>
    public partial struct NativeQuadtree<T> : INativeDisposable where T : unmanaged, IComparable<T> {

        private readonly int maxDepth;
        private readonly int objectsPerNode;

        private AABB2D bounds;
        private float2 boundsCenter; // Precomputed bounds values as they are used often
        private float2 boundsExtents;
        private float2 boundsQuarterSize;

        /// <summary>
        /// Mapping from nodeId to the amount of objects that are in it. Once that limit surpasses <see cref="objectsPerNode"/>
        /// the node subdivides and the count remains at <see cref="objectsPerNode"/> + 1.
        /// To check if a node is a leaf is therefore as simple as checking that the count is less or equal than <see cref="objectsPerNode"/>,
        /// or whether depth is <see cref="maxDepth"/>
        /// </summary>
        public NativeArray<int> nodes;
        private UnsafeParallelMultiHashMap<uint, ObjWrapper> objects;

        public ME.BECS.NativeCollections.NativeParallelList<ObjWrapper> tempObjects;
        private Allocator allocator;

        /// <summary>
        /// Constructs an quadtree with a max depth of 8
        /// </summary>
        public NativeQuadtree(AABB2D bounds, Allocator allocator) : this(bounds, 32, 6, allocator) { }

        /// <summary>
        /// Constructs an quadtree
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="objectsPerNode">The max amount of objects per quad until max depth is reached</param>
        /// <param name="maxDepth">Max split depth of the tree.</param>
        /// <param name="initialCapacity">Hint at the initial capacity of the tree</param>
        /// <param name="allocator"></param>
        public NativeQuadtree(AABB2D bounds, int objectsPerNode, int maxDepth, Allocator allocator, int initialCapacity = 0) {
            if (maxDepth <= 1 || maxDepth > MaxDepth) {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth is " + MaxDepth);
            }

            if (!bounds.IsValid) {
                throw new ArgumentException("Bounds max must be greater than min");
            }

            this.allocator = allocator;
            this.objects = new UnsafeParallelMultiHashMap<uint, ObjWrapper>(initialCapacity, allocator);
            this.nodes = CollectionHelper.CreateNativeArray<int>(initialCapacity / objectsPerNode, allocator);//new NativeArray<int>(initialCapacity / objectsPerNode, allocator);//new NativeHashMap<uint, int>(initialCapacity / objectsPerNode, allocator);

            this.objectsPerNode = objectsPerNode;
            this.maxDepth = maxDepth;
            this.bounds = bounds;
            this.boundsCenter = bounds.Center;
            this.boundsExtents = bounds.Size / 2;
            this.boundsQuarterSize = this.boundsExtents / 2;
            this.tempObjects = new ME.BECS.NativeCollections.NativeParallelList<ObjWrapper>(initialCapacity, allocator);
        }

        public void SetBounds(AABB2D bounds) {
            this.bounds = bounds;
            this.boundsCenter = bounds.Center;
            this.boundsExtents = bounds.Size / 2;
            this.boundsQuarterSize = this.boundsExtents / 2;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T obj, AABB2D bounds) {
            this.tempObjects.Add(new ObjWrapper(obj, bounds));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rebuild() {

            var temp = this.tempObjects.ToList(Allocator.Temp);
            // [!] Must be sorted because we add elements in threads 
            temp.Sort();
            foreach (var obj in temp) {
                var marker = new Unity.Profiling.ProfilerMarker("Insert");
                marker.Begin();
                this.Insert(obj.obj, obj.bounds);
                marker.End();
            }
            
        }
        
        /// <summary>
        /// Clear the tree
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Clear() {
            this.tempObjects.Clear();
            this.objects.Clear();
            //this.nodes.Clear();
            _memclear((safe_ptr)this.nodes.GetUnsafePtr(), TSize<int>.size * this.nodes.Length);
        }

        /// <summary>
        /// Bounds of the tree
        /// </summary>
        public AABB2D Bounds => this.bounds;

        /// <summary>
        /// Insert an object into the tree.
        /// </summary>
        /// <param name="obj">The object to insert</param>
        /// <param name="bounds">The axis aligned bounding box of the object</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(T obj, AABB2D bounds) {
            // Root node id = 1
            // we always start at depth one (assume one split)
            // our root node has a non zero id so that quads with bits 000 never get confused with the root node
            // since we have 32 bits of room and our max depth is 10 (30 bits) we have room for that one root node bit
            // at position 31
            var objWrapper = new ObjWrapper(obj, bounds);
            this.InsertNext(1, new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize), objWrapper, 0);
        }

        /// <summary>
        /// Insert a point into the quadtree (AABB2D's min == max).
        /// This insertion method is significantly faster than the insertion with an AABB2D.
        /// </summary>
        public void InsertPoint(T obj, float2 point) {
            var objWrapper = new ObjWrapper(obj, new AABB2D(point, point));

            // We can (mainly) do without recursion for this insertion method. Except for when an quad needs to subdivide.
            var extents = new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize);
            var depth = 1;
            uint nodeId = 1;
            while (depth <= this.maxDepth) {
                // We can get the one quad the point is in with one operation
                var quadIndex = PointToQuadIndex(point, extents.nodeCenter);
                extents = QuarterSizeBounds.GetQuad(extents, quadIndex);
                nodeId = GetQuadId(nodeId, quadIndex);

                if (this.TryInsert(nodeId, extents, objWrapper, depth)) {
                    return;
                }

                depth++;
            }
        }

        private void InsertNext(uint nodeid, in QuarterSizeBounds quarterSizeBounds, in ObjWrapper objWrapper, int parentDepth) {
            parentDepth++;
            var objMask = GetBoundsMask(quarterSizeBounds.nodeCenter, objWrapper.bounds);

            for (var i = 0; i < 4; i++) {
                var quadMask = QuadMasks[i];
                if ((objMask & quadMask) != quadMask) {
                    continue;
                }

                var quadId = GetQuadId(nodeid, i);
                var quadCenterQuarterSize = QuarterSizeBounds.GetQuad(quarterSizeBounds, i);

                if (!this.TryInsert(quadId, quadCenterQuarterSize, objWrapper, parentDepth)) {
                    this.InsertNext(quadId, quadCenterQuarterSize, objWrapper, parentDepth);
                }
            }
        }

        /// <summary>
        /// Inserts the object if the node is a leaf. Otherwise, returns false and the tree should be traversed deeper.
        /// </summary>
        private bool TryInsert(uint nodeId, in QuarterSizeBounds extents, in ObjWrapper objWrapper, int depth) {
            //this.nodes.TryGetValue(nodeId, out var objectCount);
            this.TryGetNode(nodeId, out var objectCount);

            // a node is considered a leaf as long as it's (prev) objectcount <= max objects per node
            // or, when it is at the max tree depth
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                objectCount++;
                this.objects.Add(nodeId, objWrapper);
                this.SetNode(nodeId, objectCount);
                //this.nodes[nodeId] = objectCount;
                if (objectCount > this.objectsPerNode && depth < this.maxDepth) {
                    this.Subdivide(nodeId, in extents, depth);
                }

                return true;
            }

            return false;
        }

        private bool TryGetNode(uint nodeId, out int count) {
            count = 0;
            if (nodeId >= this.nodes.Length) return false;
            count = this.nodes[(int)nodeId];
            return count > 0;
        }

        private void SetNode(uint nodeId, int count) {
            if (nodeId >= this.nodes.Length) {
                var newNodes = CollectionHelper.CreateNativeArray<int>((int)(nodeId + 1u), this.allocator);//new NativeArray<int>((int)(nodeId + 1u), this.allocator);
                NativeArray<int>.Copy(this.nodes, 0, newNodes, 0, this.nodes.Length);
                this.nodes.Dispose();
                this.nodes = newNodes;
            }
            this.nodes[(int)nodeId] = count;
        }

        private void Subdivide(uint nodeId, in QuarterSizeBounds quarterSizeBounds, int depth) {
            var objectCount = 0;
            var tempObjects = new NativeArray<ObjWrapper>(this.objectsPerNode + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            foreach (var tempObj in this.objects.GetValuesForKey(nodeId)) {
                tempObjects[objectCount++] = tempObj;
            }

            var countPerQuad = new FixedList32Bytes<int>();
            countPerQuad.Length = 4;

            this.objects.Remove(nodeId); // remove all occurances of objects in our original
            for (var i = 0; i < objectCount; i++) {
                var moveObject = tempObjects[i];
                var aabbMask = GetBoundsMask(quarterSizeBounds.nodeCenter, moveObject.bounds);

                // Can't make the point optimization here because we can't be certain the node only contained points
                // Also, benchmarking turned out that this does not yield a significant performance boost anyway

                for (var j = 0; j < 4; j++) {
                    var quadMask = QuadMasks[j];
                    if ((aabbMask & quadMask) == quadMask) {
                        this.objects.Add(GetQuadId(nodeId, j), moveObject);
                        countPerQuad[j] = countPerQuad[j] + 1; // ++ ?
                    }
                }
            }

            //tempObjects.Dispose();

            // Update counts, create nodes when neccessary
            depth++;
            for (var i = 0; i < 4; i++) {
                var count = countPerQuad[i];
                if (count > 0) {
                    var quadId = GetQuadId(nodeId, i);
                    //this.nodes[(int)quadId] = count; // mark our node as being used
                    this.SetNode(quadId, count);

                    // could be that we need to subdivide again if all of the objects went to the same quad
                    if (count > this.objectsPerNode && depth < this.maxDepth) // todo: maxDepth check can be hoisted
                    {
                        this.Subdivide(quadId, QuarterSizeBounds.GetQuad(quarterSizeBounds, i), depth);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the quad's index of a point. Which translated to bits means if the point is on the negative (0) side or positve (1)
        /// side of the node's center.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PointToQuadIndex(float2 point, float2 nodeCenter) {
            return math.bitmask((point >= nodeCenter).xyxy) >> 2;
        }

        /// <summary>
        /// Max depth of the quadtree
        /// </summary>
        public const int MaxDepth = 8 * sizeof(int) / 2 - 1; //  we need 2 bits for each level we go down

        /// leave one bit for root node Id
        /// <summary>
        /// Gets a unique identifier for a node.
        /// The quads are identified using the following pattern, where a zero stands for the negative side:
        ///
        /// 10  11
        ///
        /// 00  01
        /// 
        /// For each level we go down, we shift these bits 2 spaces to the left
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetQuadId(uint parent, int quadIndex) {
            return (parent << 2) | (uint)quadIndex;
        }

        /*
         * AABB2D - quad overlap technique explanation:
         * We use a clever technique to determine if bounds belong in an quad by computing one mask value for the object's bounds
         * and comparing it to a predefined bitmask for each quad. This avoids needing to compute the actual overlap eight times
         * and instead boils down to one bitwise AND comparison for each quad. This boosts performance for AABB2D insertion and range queries.
         *
         * How it works:
         * First, we compare the object's bounds to the center of the parent node we're currently in and convert it to a 4 bit wide bitmask where
         * the lower 2 bits are set to 1 if the bounds min was on a negative side for that axis.
         * The upper 2 bits are set to 1 if the bounds max was on the positive side for that axis.
         *
         * We have predefined masks for each quad, where a bitwise AND against the object's mask tells you if the object belongs in that quad,
         * in which case the result must be equal the the quad mask value.
         *
         * The quad mask says: if the quad is negative for an axis, the min bit for that axis must be set on the object.
         * If the quad is positive for an axis, the max bit for that axis must be set on the object.
         *
         * This works because for positive axes, the object's bounds max must be in there (it's impossible for max to be negative and min to be positive)
         * Same goes for negative axis overlaps, it's impossible for the max to be on the negative side if min is not there as well
         *
         * Also note that, we don't need to check against the outer bounds of the parent node, as this inherently happens because
         * every object is inserted top down from the root node.
         */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBoundsMask(in float2 nodeCenter, in AABB2D AABB2D) {
            var offMin = math.bitmask(AABB2D.min.xyxy <= nodeCenter.xyxy) >> 2;
            return offMin | (math.bitmask(AABB2D.max.xyxy >= nodeCenter.xyxy) << 2);
        }

        private static readonly int[] QuadMasks = new[] {
            0b00_11,
            0b01_10,
            0b10_01,
            0b11_00,
        };

        // Offset from parent node's center multipliers for each quad
        private static readonly float2[] QuadCenterOffsets = new[] {
            new float2(-1, -1),
            new float2(1, -1),
            new float2(-1, 1),
            new float2(1, 1),
        };

        /// <summary>
        /// Stores an object together with it's bounds
        /// </summary>
        public readonly struct ObjWrapper : IComparable<ObjWrapper> {

            public readonly AABB2D bounds;
            public readonly T obj;

            public ObjWrapper(T obj, AABB2D bounds) {
                this.obj = obj;
                this.bounds = bounds;
            }

            public int CompareTo(ObjWrapper other) {
                return this.obj.CompareTo(other.obj);
            }

        }

        // This turned out to be the most efficient way of passing down dimensions of nodes
        // for insertion and range queries
        private readonly ref struct QuarterSizeBounds {

            public readonly float2 nodeCenter;
            public readonly float2 nodeQuarterSize;

            public QuarterSizeBounds(float2 nodeCenter, float2 nodeQuarterSize) {
                this.nodeCenter = nodeCenter;
                this.nodeQuarterSize = nodeQuarterSize;
            }

            /// <summary>
            /// Returns the insert node info for the next quad at index.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QuarterSizeBounds GetQuad(in QuarterSizeBounds parent, int index) {
                return new QuarterSizeBounds(parent.nodeCenter + QuadCenterOffsets[index] * parent.nodeQuarterSize, .5f * parent.nodeQuarterSize);
            }

            // note: yes, new quarterSize can be precomputed but benchmarking showed no difference and it enhances readability

        }

        private readonly struct ExtentsBounds {

            public readonly float2 nodeCenter;
            public readonly float2 nodeExtents;

            public ExtentsBounds(float2 nodeCenter, float2 nodeExtents) {
                this.nodeCenter = nodeCenter;
                this.nodeExtents = nodeExtents;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ExtentsBounds GetQuad(in ExtentsBounds parent, int index) {
                var quadExtents = .5f * parent.nodeExtents;
                return new ExtentsBounds(parent.nodeCenter + QuadCenterOffsets[index] * quadExtents, quadExtents);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(float2 point) {
                return math.all(math.abs(this.nodeCenter - point) <= this.nodeExtents);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AABB2D GetBounds(in ExtentsBounds ce) {
                return new AABB2D(ce.nodeCenter - ce.nodeExtents, ce.nodeCenter + ce.nodeExtents);
            }

        }

        /// <summary>
        /// Dispose the NativeOctree
        /// </summary>
        public void Dispose() {
            this.nodes.Dispose();
            this.objects.Dispose();
        }

        /// <summary>
        /// Dispose the NativeOctree
        /// </summary>
        public JobHandle Dispose(JobHandle inputDeps) {
            return JobHandle.CombineDependencies(this.nodes.Dispose(inputDeps), this.objects.Dispose(inputDeps));
        }

        /// <summary>
        /// Draws debug gizmos of all the quads in the tree
        /// </summary>
        public void DrawGizmos() {
            var root = new ExtentsBounds(this.boundsCenter, this.boundsExtents);
            this.GizmosNext(1, root, 0);
        }

        private void GizmosNext(uint nodeId, in ExtentsBounds quarterSizeBounds, int parentDepth) {
            parentDepth++;

            for (var i = 0; i < 4; i++) {
                var quadId = GetQuadId(nodeId, i);
                if (this.TryGetNode(quadId, out var count)) {
                    this.Gizmos(quadId, ExtentsBounds.GetQuad(quarterSizeBounds, i), count, parentDepth);
                }
            }
        }

        private void Gizmos(uint nodeId, in ExtentsBounds quarterSizeBounds, int objectCount, int depth) {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                UnityEngine.Gizmos.DrawWireCube(new Vector3(quarterSizeBounds.nodeCenter.x, 0f, quarterSizeBounds.nodeCenter.y), new Vector3(quarterSizeBounds.nodeExtents.x, 0f, quarterSizeBounds.nodeExtents.y) * 2);
                return;
            }

            this.GizmosNext(
                nodeId,
                quarterSizeBounds,
                depth);
        }

    }

}