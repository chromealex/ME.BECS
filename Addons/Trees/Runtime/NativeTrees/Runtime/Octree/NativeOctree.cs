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

// https://bartvandesande.nl
// https://github.com/bartofzo

// ReSharper disable StaticMemberInGenericType

namespace NativeTrees {

    /// <summary>
    /// Generic Burst/ECS compatible sparse octree that stores objects together with their axis aligned bounding boxes (AABB's)
    /// <para>
    /// Supported queries:
    ///     - Raycast
    ///     - Range (AABB overlap)
    ///     - Nearest neighbours
    /// </para>
    /// <para>
    /// Other features:
    ///     - Implemented as a sparse octree, so only stores nodes that are occupied,
    ///       allowing it to go to a max depth of 10 (this could be more if the nodeId's are stored as long values
    ///     - Supports insertion of AABB's
    ///     - Fast insertion for points
    ///     - Employs an extremely fast technique of checking for AABB / octant overlaps, see comment near the end
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
    ///     - 'Fat' raycast (virtually expand AABB's of nodes and objects when testing for ray intersections)
    /// </para>
    /// </summary>
    public partial struct NativeOctree<T> : INativeDisposable
        where T : unmanaged, IComparable<T> {

        private readonly int maxDepth;
        private readonly int objectsPerNode;

        private AABB bounds;
        private readonly float3 boundsCenter; // Precomputed bounds values as they are used often
        private readonly float3 boundsExtents;
        private readonly float3 boundsQuarterSize;
        
        /// <summary>
        /// Mapping from nodeId to the amount of objects that are in it. Once that limit surpasses <see cref="objectsPerNode"/>
        /// the node subdivides and the count remains at <see cref="objectsPerNode"/> + 1.
        /// To check if a node is a leaf is therefore as simple as checking that the count is less or equal than <see cref="objectsPerNode"/>,
        /// or whether depth is <see cref="maxDepth"/>
        /// </summary>
        private NativeHashMap<uint, int> nodes;
        private NativeParallelMultiHashMap<uint, ObjWrapper> objects;

        private ME.BECS.NativeCollections.NativeParallelList<ObjWrapper> tempObjects;

        /// <summary>
        /// Constructs an octree with a max depth of 8
        /// </summary>
        public NativeOctree(AABB bounds, Allocator allocator) : this(bounds, 32, 8, allocator) { }

        /// <summary>
        /// Constructs an octree
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="objectsPerNode">The max amount of objects per octant until max depth is reached</param>
        /// <param name="maxDepth">Max split depth of the tree.</param>
        /// <param name="initialCapacity">Hint at the initial capacity of the tree</param>
        /// <param name="allocator"></param>
        public NativeOctree(AABB bounds, int objectsPerNode, int maxDepth, Allocator allocator, int initialCapacity = 0) {
            if (maxDepth <= 1 || maxDepth > NativeOctree<T>.MaxDepth) {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth is " + NativeOctree<T>.MaxDepth);
            }

            if (!bounds.IsValid) {
                throw new ArgumentException("Bounds max must be greater than min");
            }

            this.objects = new NativeParallelMultiHashMap<uint, ObjWrapper>(initialCapacity, allocator);
            this.nodes = new NativeHashMap<uint, int>(initialCapacity / objectsPerNode, allocator);

            this.objectsPerNode = objectsPerNode;
            this.maxDepth = maxDepth;
            this.bounds = bounds;
            this.boundsCenter = bounds.Center;
            this.boundsExtents = bounds.Size / 2f;
            this.boundsQuarterSize = this.boundsExtents / 2f;
            this.tempObjects = new ME.BECS.NativeCollections.NativeParallelList<ObjWrapper>(initialCapacity, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T obj, AABB bounds) {
            this.tempObjects.Add(new ObjWrapper(obj, bounds));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rebuild() {

            var temp = this.tempObjects.ToList(Allocator.Temp);
            // [!] Must be sorted because we add elements in threads 
            temp.Sort();
            foreach (var obj in temp) {
                this.Insert(obj.obj, obj.bounds);
            }
            
        }

        /// <summary>
        /// Clear the tree
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            this.tempObjects.Clear();
            this.objects.Clear();
            this.nodes.Clear();
        }

        /// <summary>
        /// Bounds of the tree
        /// </summary>
        public AABB Bounds => this.bounds;

        /// <summary>
        /// Insert an object into the tree.
        /// </summary>
        /// <param name="obj">The object to insert</param>
        /// <param name="bounds">The axis aligned bounding box of the object</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(T obj, AABB bounds) {
            // Root node id = 1
            // we always start at depth one (assume one split)
            // our root node has a non zero id so that octants with bits 000 never get confused with the root node
            // since we have 32 bits of room and our max depth is 10 (30 bits) we have room for that one root node bit
            // at position 31
            var objWrapper = new ObjWrapper(obj, bounds);
            this.InsertNext(1, new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize), objWrapper, 0);
        }

        /// <summary>
        /// Insert a point into the octree (AABB's min == max).
        /// This insertion method is significantly faster than the insertion with an AABB.
        /// </summary>
        public void InsertPoint(T obj, float3 point) {
            var objWrapper = new ObjWrapper(obj, new AABB(point, point));

            // We can (mainly) do without recursion for this insertion method. Except for when an octant needs to subdivide.
            var extents = new QuarterSizeBounds(this.boundsCenter, this.boundsQuarterSize);
            var depth = 1;
            uint nodeId = 1;
            while (depth <= this.maxDepth) {
                // We can get the one octant the point is in with one operation
                var octantIndex = NativeOctree<T>.PointToOctantIndex(point, extents.nodeCenter);
                extents = QuarterSizeBounds.GetOctant(extents, octantIndex);
                nodeId = NativeOctree<T>.GetOctantId(nodeId, octantIndex);

                if (this.TryInsert(nodeId, extents, objWrapper, depth)) {
                    return;
                }

                depth++;
            }
        }

        private struct InsertNextNode {

            public uint nodeId;
            public QuarterSizeBounds quarterSizeBounds;
            public ObjWrapper objWrapper;
            public int parentDepth;

        }

        private void InsertNext(uint nodeid, in QuarterSizeBounds quarterSizeBounds, in ObjWrapper objWrapper, int parentDepth) {
            InsertNext(new InsertNextNode() {
                nodeId = nodeid,
                quarterSizeBounds = quarterSizeBounds,
                objWrapper = objWrapper,
                parentDepth = parentDepth,
            });
        }

        private void InsertNext(InsertNextNode nodeData) {
            nodeData.parentDepth++;
            var objMask = NativeOctree<T>.GetBoundsMask(nodeData.quarterSizeBounds.nodeCenter, nodeData.objWrapper.bounds);

            var q = new Unity.Collections.LowLevel.Unsafe.UnsafeList<InsertNextNode>(8, Allocator.Temp);
            q.Add(nodeData);
            while (q.Length > 0) {
                var n = q[0];
                q.RemoveAtSwapBack(0);
                for (var i = 0; i < 8; i++) {
                    var octantMask = NativeOctree<T>.OctantMasks[i];
                    if ((objMask & octantMask) != octantMask) {
                        continue;
                    }

                    var octantId = NativeOctree<T>.GetOctantId(n.nodeId, i);
                    var octantCenterQuarterSize = QuarterSizeBounds.GetOctant(n.quarterSizeBounds, i);

                    if (this.TryInsert(octantId, octantCenterQuarterSize, n.objWrapper, n.parentDepth) == false) {
                        //this.InsertNext(octantId, octantCenterQuarterSize, n.objWrapper, n.parentDepth);
                        q.Add(n);
                    }
                }
            }
            q.Dispose();
        }

        /// <summary>
        /// Inserts the object if the node is a leaf. Otherwise, returns false and the tree should be traversed deeper.
        /// </summary>
        private bool TryInsert(uint nodeId, in QuarterSizeBounds extents, in ObjWrapper objWrapper, int depth) {
            this.nodes.TryGetValue(nodeId, out var objectCount);

            // a node is considered a leaf as long as it's (prev) objectcount <= max objects per node
            // or, when it is at the max tree depth
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                objectCount++;
                this.objects.Add(nodeId, objWrapper);
                this.nodes[nodeId] = objectCount;
                if (objectCount > this.objectsPerNode && depth < this.maxDepth) {
                    this.Subdivide(nodeId, in extents, depth);
                }

                return true;
            }

            return false;
        }

        private void Subdivide(uint nodeId, in QuarterSizeBounds quarterSizeBounds, int depth) {
            var objectCount = 0;
            var tempObjects = new NativeArray<ObjWrapper>(this.objectsPerNode + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            foreach (var tempObj in this.objects.GetValuesForKey(nodeId)) {
                tempObjects[objectCount++] = tempObj;
            }

            var countPerOctant = new FixedList64Bytes<int>();
            countPerOctant.Length = 8;

            this.objects.Remove(nodeId); // remove all occurances of objects in our original
            for (var i = 0; i < objectCount; i++) {
                var moveObject = tempObjects[i];
                var aabbMask = NativeOctree<T>.GetBoundsMask(quarterSizeBounds.nodeCenter, moveObject.bounds);

                // Can't make the point optimization here because we can't be certain the node only contained points
                // Also, benchmarking turned out that this does not yield a significant performance boost anyway

                for (var j = 0; j < 8; j++) {
                    var octantMask = NativeOctree<T>.OctantMasks[j];
                    if ((aabbMask & octantMask) == octantMask) {
                        this.objects.Add(NativeOctree<T>.GetOctantId(nodeId, j), moveObject);
                        countPerOctant[j] = countPerOctant[j] + 1; // ++ ?
                    }
                }
            }

            tempObjects.Dispose();

            // Update counts, create nodes when neccessary
            depth++;
            for (var i = 0; i < 8; i++) {
                var count = countPerOctant[i];
                if (count > 0) {
                    var octantId = NativeOctree<T>.GetOctantId(nodeId, i);
                    this.nodes[octantId] = count; // mark our node as being used

                    // could be that we need to subdivide again if all of the objects went to the same octant
                    if (count > this.objectsPerNode && depth < this.maxDepth) // todo: maxDepth check can be hoisted
                    {
                        this.Subdivide(octantId, QuarterSizeBounds.GetOctant(quarterSizeBounds, i), depth);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the octant's index of a point. Which translated to bits means if the point is on the negative (0) side or positve (1)
        /// side of the node's center.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PointToOctantIndex(float3 point, float3 nodeCenter) {
            return math.bitmask((point >= nodeCenter).xxyz) >> 1;
        }

        /// <summary>
        /// Max depth of the octree, we need 3 bits for each level we go down
        /// </summary>
        public const int MaxDepth = 8 * sizeof(int) / 3;

        /// <summary>
        /// Gets a unique identifier for a node.
        /// The octants are identified using the following pattern, where a zero stands for the negative side:
        /// 
        ///         110     111
        ///     010     011
        ///     
        ///         100     101
        ///     000    001
        /// 
        /// For each level we go down, we shift these bits 3 spaces to the left
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetOctantId(uint parent, int octantIndex) {
            return (parent << 3) | (uint)octantIndex;
        }

        /*
         * AABB - Octant overlap technique explanation:
         * We use a clever technique to determine if bounds belong in an octant by computing one mask value for the object's bounds
         * and comparing it to a predefined bitmask for each octant. This avoids needing to compute the actual overlap eight times
         * and instead boils down to one bitwise AND comparison for each octant. This boosts performance for AABB insertion and range queries.
         *
         * How it works:
         * First, we compare the object's bounds to the center of the parent node we're currently in and convert it to a 6 bit wide bitmask where
         * the lower 3 bits are set to 1 if the bounds min was on a negative side for that axis.
         * The upper 3 bits are set to 1 if the bounds max was on the positive side for that axis.
         *
         * We have predefined masks for each octant, where a bitwise AND against the object's mask tells you if the object belongs in that octant,
         * in which case the result must be equal the the octant mask value.
         *
         * The octant mask says: if the octant is negative for an axis, the min bit for that axis must be set on the object.
         * If the octant is positive for an axis, the max bit for that axis must be set on the object.
         *
         * This works because for positive axes, the object's bounds max must be in there (it's impossible for max to be negative and min to be positive)
         * Same goes for negative axis overlaps, it's impossible for the max to be on the negative side if min is not there as well
         *
         * Also note that, we don't need to check against the outer bounds of the parent node, as this inherently happens because
         * every object is inserted top down from the root node.
         */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBoundsMask(float3 nodeCenter, AABB aabb) {
            var offMin = math.bitmask(aabb.min.xxyz <= nodeCenter.xxyz) >> 1; // leaves xyz
            // for the max we have one bit too many but that's OK, since we AND it with the octant mask, we lose that bit anyway
            return offMin | (math.bitmask(aabb.max.xyzz >= nodeCenter.xyzz) << 3);

            // non-SIMD version:
            /*
            float3 offMin = aabb.min - nodeCenter;
            float3 offMax = aabb.max - nodeCenter;
            return (offMin.x <= 0 ? 0b000_001 : 0) |
                   (offMin.y <= 0 ? 0b000_010 : 0) |
                   (offMin.z <= 0 ? 0b000_100 : 0) |
                   (offMax.x >= 0 ? 0b001_000 : 0) |
                   (offMax.y >= 0 ? 0b010_000 : 0) |
                   (offMax.z >= 0 ? 0b100_000 : 0);
                   */
        }

        private static readonly int[] OctantMasks = new[] {
            //             ZYX
            0b000_111, // 000  all negative, so the entire object's min should be negative
            0b001_110, // 001  YZ are negative so they compare with min, but X is positive so compares with max
            0b010_101, // 010  XZ negative so compare them with min, Y is positive so compares with max 
            0b011_100, // 011  Z is negative, compare that with min. Compare XY with max.
            0b100_011, // 100  etc...
            0b101_010, // 101
            0b110_001, // 110
            0b111_000, // 111 all axes are positive, so the entire objects max should be positive as well
        };

        // Offset from parent node's center multipliers for each octant
        private static readonly float3[] OctantCenterOffsets = new[] {
            //                         ZYX
            new float3(-1), // 000
            new float3(1, -1, -1), // 001
            new float3(-1, 1, -1), // 010
            new float3(1, 1, -1), // 011
            new float3(-1, -1, 1), // 100
            new float3(1, -1, 1), // 101
            new float3(-1, 1, 1), // 110
            new float3(1), // 111
        };


        /// <summary>
        /// Stores an object together with it's bounds
        /// </summary>
        private readonly struct ObjWrapper : IComparable<ObjWrapper> {

            public readonly AABB bounds;
            public readonly T obj;

            public ObjWrapper(T obj, AABB bounds) {
                this.obj = obj;
                this.bounds = bounds;
            }

            public int CompareTo(ObjWrapper other) {
                return this.obj.CompareTo(other.obj);
            }

        }

        // This turned out to be the most efficient way of passing down dimensions of nodes
        // for insertion and range queries
        private readonly struct QuarterSizeBounds {

            public readonly float3 nodeCenter;
            public readonly float3 nodeQuarterSize;

            public QuarterSizeBounds(float3 nodeCenter, float3 nodeQuarterSize) {
                this.nodeCenter = nodeCenter;
                this.nodeQuarterSize = nodeQuarterSize;
            }

            /// <summary>
            /// Returns the insert node info for the next octant at index.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static QuarterSizeBounds GetOctant(in QuarterSizeBounds parent, int index) {
                return new QuarterSizeBounds(parent.nodeCenter + OctantCenterOffsets[index] * parent.nodeQuarterSize, .5f * parent.nodeQuarterSize);
            }

            // note: yes, new quarterSize can be precomputed but benchmarking showed no difference and it enhances readability

        }

        private readonly struct ExtentsBounds {

            public readonly float3 nodeCenter;
            public readonly float3 nodeExtents;

            public ExtentsBounds(float3 nodeCenter, float3 nodeExtents) {
                this.nodeCenter = nodeCenter;
                this.nodeExtents = nodeExtents;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ExtentsBounds GetOctant(in ExtentsBounds parent, int index) {
                var octantExtents = .5f * parent.nodeExtents;
                return new ExtentsBounds(parent.nodeCenter + OctantCenterOffsets[index] * octantExtents, octantExtents);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Contains(float3 point) {
                return math.all(math.abs(this.nodeCenter - point) <= this.nodeExtents);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AABB GetBounds(in ExtentsBounds ce) {
                return new AABB(ce.nodeCenter - ce.nodeExtents, ce.nodeCenter + ce.nodeExtents);
            }

        }

        /// <summary>
        /// Clears and copies the contents of the source tree into this one
        /// </summary>
        /// <param name="source">The source tree to copy</param>
        public void CopyFrom(NativeOctree<T> source) {
            if (this.maxDepth != source.maxDepth || this.objectsPerNode != source.objectsPerNode) {
                throw new ArgumentException("Source maxDepth and objectsPerNode must be the same as destination");
            }

            if (!this.boundsCenter.Equals(source.boundsCenter) || !this.boundsExtents.Equals(source.boundsExtents)) {
                throw new ArgumentException("Source bounds must be equal");
            }

            this.objects.Clear();
            this.nodes.Clear();

            var kvs = source.objects.GetKeyValueArrays(Allocator.Temp);
            for (var i = 0; i < kvs.Length; i++) {
                this.objects.Add(kvs.Keys[i], kvs.Values[i]);
            }

            var kvs2 = source.nodes.GetKeyValueArrays(Allocator.Temp);
            for (var i = 0; i < kvs2.Length; i++) {
                this.nodes.Add(kvs2.Keys[i], kvs2.Values[i]);
            }

            kvs.Dispose();
            kvs2.Dispose();
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
        /// Draws debug gizmos of all the octants in the tree
        /// </summary>
        public void DrawGizmos() {
            var root = new ExtentsBounds(this.boundsCenter, this.boundsExtents);
            this.GizmosNext(1, root, 0);
        }

        private void GizmosNext(uint nodeId, in ExtentsBounds quarterSizeBounds, int parentDepth) {
            parentDepth++;

            for (var i = 0; i < 8; i++) {
                var quadId = NativeOctree<T>.GetOctantId(nodeId, i);
                if (this.nodes.TryGetValue(quadId, out var count)) {
                    this.Gizmos(quadId, ExtentsBounds.GetOctant(quarterSizeBounds, i), count, parentDepth);
                }
            }
        }

        private void Gizmos(uint nodeId, in ExtentsBounds quarterSizeBounds, int objectCount, int depth) {
            // Are we in a leaf node?
            if (objectCount <= this.objectsPerNode || depth == this.maxDepth) {
                UnityEngine.Gizmos.DrawWireCube((Vector3)quarterSizeBounds.nodeCenter, (Vector3)quarterSizeBounds.nodeExtents * 2);
                return;
            }

            this.GizmosNext(
                nodeId,
                quarterSizeBounds,
                depth);
        }

    }

}