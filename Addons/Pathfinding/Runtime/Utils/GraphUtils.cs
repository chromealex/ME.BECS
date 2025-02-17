#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using ME.BECS.Transforms;

    public static class GraphUtils {

        [INLINE(256)]
        public static MemArrayAuto<float> GetBuildingHeights(in Ent ent, float height, uint2 size, out uint sizeX) {

            sizeX = size.x;
            var sizeY = size.y;
            var length = size.x * size.y;
            var arr = new MemArrayAuto<float>(in ent, length);
            for (uint i = 0u; i < length; ++i) {
                if (i < sizeX || i % sizeX == 0 || i >= sizeX * (sizeY - 1u) || i % (sizeX - 1u) == 0) {
                    arr[i] = 0f;
                } else {
                    arr[i] = height;
                }
            }
            return arr;

        }

        [INLINE(256)]
        public static float3 SnapWorldPosition(in Ent graph, float3 worldPosition, uint2 size) {

            var offset = float2.zero;
            if (size.x % 2 == 0) {
                offset.x -= 0.5f;
            }
            if (size.y % 2 == 0) {
                offset.y -= 0.5f;
            }
            
            var root = graph.Read<RootGraphComponent>();
            worldPosition.x = math.round(worldPosition.x / root.nodeSize) * root.nodeSize + offset.x;
            worldPosition.z = math.round(worldPosition.z / root.nodeSize) * root.nodeSize + offset.y;
            
            return worldPosition;

        }

        /// <summary>
        /// Returns half-size, snapped to a grid rect size vector
        /// </summary>
        /// <param name="size">Vector with half-width and half-height of a rect</param>
        /// <returns></returns>
        [INLINE(256)]
        public static float2 SnapSize(uint2 size) {

            var offset = SizeOffset(size);
            return new float2(math.floor(size.x * 0.5f), math.floor(size.y * 0.5f)) + offset;

        }

        [INLINE(256)]
        public static float2 SizeOffset(uint2 size) {

            var offset = float2.zero;
            if (size.x % 2 == 0) {
                offset.x -= 0.5f;
            }
            if (size.y % 2 == 0) {
                offset.y -= 0.5f;
            }
            
            return offset;

        }

        [INLINE(256)]
        public static bool IsGraphMaskValid(in BuildGraphSystem system, in float3 position, in quaternion rotation, uint2 size, byte minCost, byte maxCost) {

            for (uint i = 0u; i < system.graphs.Length; ++i) {
                var graph = system.graphs[system.world.state, i];
                if (IsGraphMaskValid(in graph, in position, in rotation, size, minCost, maxCost) == false) return false;
            }

            return true;

        }
        
        [INLINE(256)]
        public static unsafe bool IsGraphMaskValid(in Ent graph, in float3 position, in quaternion rotation, uint2 size, byte minCost, byte maxCost) {

            var root = graph.Read<RootGraphComponent>();
            var state = graph.World.state;
            var pos = position.xz;
            var sizeSnap = SnapSize(size);
            var posMin = pos - sizeSnap;
            var posMax = pos + sizeSnap;
            for (tfloat x = posMin.x; x <= posMax.x; x += root.nodeSize) {
                for (tfloat y = posMin.y; y <= posMax.y; y += root.nodeSize) {
                    var worldPos = new float3(math.floor(x), 0f, math.floor(y));
                    var graphPos = math.mul(rotation, worldPos - position) + position;
                    var globalCoord = Graph.GetGlobalCoord(in root, graphPos);
                    var tempNode = Graph.GetCoordInfo(in root, globalCoord.x, globalCoord.y);
                    ref var node = ref root.chunks[state, tempNode.chunkIndex].nodes[state, tempNode.nodeIndex];
                    if (node.cost >= minCost && node.cost <= maxCost) {
                        // valid
                    } else {
                        return false;
                    }
                }
            }
            return true;

        }

        [INLINE(256)]
        public static void DestroyGraphMask(in Ent ent) {

            { // Apply to graphs
                var mask = ent.Read<GraphMaskRuntimeComponent>();
                mask.Destroy();
            }
            ent.DestroyHierarchy();
            
        }
        
        [INLINE(256)]
        public static Ent CreateGraphMask(in float3 position, in quaternion rotation, uint2 size, byte cost = Graph.UNWALKABLE, ObstacleChannel obstacleChannel = ObstacleChannel.Obstacle, bool ignoreGraphRadius = false, int graphMask = -1, in JobInfo jobInfo = default) {

            var ent = Ent.New(in jobInfo);
            return CreateGraphMask(in ent, in position, in rotation, size, cost, 1f, obstacleChannel, ignoreGraphRadius, graphMask);

        }

        [INLINE(256)]
        public static Ent CreateGraphMask(in Ent ent, in float3 position, in quaternion rotation, uint2 size, byte cost = Graph.UNWALKABLE, ObstacleChannel obstacleChannel = ObstacleChannel.Obstacle, bool ignoreGraphRadius = false, int graphMask = -1) {

            var heights = new MemArrayAuto<tfloat>(in ent, 1u);
            heights[0u] = 1f;
            return CreateGraphMask(in ent, in position, in rotation, size, cost, obstacleChannel, ignoreGraphRadius, heights, 1u, graphMask);

        }

        [INLINE(256)]
        public static Ent CreateGraphMask(in float3 position, in quaternion rotation, uint2 size, byte cost, tfloat height, ObstacleChannel obstacleChannel = ObstacleChannel.Obstacle, bool ignoreGraphRadius = false, int graphMask = -1, in JobInfo jobInfo = default) {

            var ent = Ent.New(in jobInfo);
            return CreateGraphMask(in ent, in position, in rotation, size, cost, height, obstacleChannel, ignoreGraphRadius, graphMask);

        }

        [INLINE(256)]
        public static Ent CreateGraphMask(in Ent ent, in float3 position, in quaternion rotation, uint2 size, byte cost, tfloat height, ObstacleChannel obstacleChannel = ObstacleChannel.Obstacle, bool ignoreGraphRadius = false, int graphMask = -1) {

            var heights = new MemArrayAuto<tfloat>(in ent, 1u);
            heights[0u] = height;
            return CreateGraphMask(in ent, in position, in rotation, size, cost, obstacleChannel, ignoreGraphRadius, heights, 1u, graphMask);

        }

        [INLINE(256)]
        public static Ent CreateGraphMask(in Ent ent, in float3 position, in quaternion rotation, uint2 size, byte cost, ObstacleChannel obstacleChannel, bool ignoreGraphRadius, MemArrayAuto<tfloat> heights, uint heightsSizeX, int graphMask) {

            var obstacle = new GraphMaskComponent() {
                offset = float2.zero,
                size = size,
                heightsSizeX = heightsSizeX,
                ignoreGraphRadius = (byte)(ignoreGraphRadius == true ? 1 : 0),
                cost = cost,
                obstacleChannel = obstacleChannel,
                graphMask = graphMask,
            };
            var runtime = new GraphMaskRuntimeComponent() {
                heights = heights,
                nodes = new ListAuto<GraphNodeMemory>(in ent, size.x * size.y),
            };
            var obstacleTr = ent.GetOrCreateAspect<TransformAspect>();
            obstacleTr.position = position;
            obstacleTr.rotation = rotation;
            ent.Set(obstacle);
            ent.Set(runtime);
            ent.Set(new IsGraphMaskDirtyComponent());
            ent.RegisterAutoDestroy<GraphMaskRuntimeComponent>();
            return ent;

        }

        [INLINE(256)]
        public static unsafe bool GetPositionWithMapBordersNode(out Node node, in Ent graph, in float3 newPos) {

            node = default;
            var root = graph.Read<RootGraphComponent>();
            var state = graph.World.state;
            var chunkIndex = Graph.GetChunkIndex(in root, newPos, false);
            if (chunkIndex == uint.MaxValue) {
                return false;
            }

            var targetChunk = root.chunks[state, chunkIndex];
            var nodeIndex = Graph.GetNodeIndex(in root, in targetChunk, newPos, false);
            if (nodeIndex == uint.MaxValue) {
                return false;
            }
            
            node = targetChunk.nodes[state, nodeIndex];
            return true;
            
        }

        public struct TempNodeTraverse {

            public Graph.TempNode node;
            public Side side;
            public int2 coord;

        }

        [INLINE(256)]
        public static unsafe float3 GetNearestNodeByFilter(in Ent graph, in float3 position, in Filter filter) {

            var root = graph.Read<RootGraphComponent>();
            var world = graph.World;
            var queue = new NativeQueue<TempNodeTraverse>(10, Unity.Collections.Allocator.Temp);
            var globalCoord = Graph.GetGlobalCoord(in root, position);
            var nodeInfo = Graph.GetCoordInfo(in root, globalCoord.x, globalCoord.y);
            queue.Enqueue(new TempNodeTraverse() { node = nodeInfo, coord = globalCoord, side = Side.Down });
            queue.Enqueue(new TempNodeTraverse() { node = nodeInfo, coord = globalCoord, side = Side.Up });
            queue.Enqueue(new TempNodeTraverse() { node = nodeInfo, coord = globalCoord, side = Side.Left });
            queue.Enqueue(new TempNodeTraverse() { node = nodeInfo, coord = globalCoord, side = Side.Right });
            while (queue.Count > 0) {
                var tempNodeInfo = queue.Dequeue();
                var chunk = root.chunks[tempNodeInfo.node.chunkIndex];
                var node = chunk.nodes[world.state, tempNodeInfo.node.nodeIndex];
                if (filter.IsValid(in node) == true) {
                    return Graph.GetPosition(in root, in chunk, tempNodeInfo.node.nodeIndex);
                }

                {
                    var dir = Graph.GetDirectionBySide(tempNodeInfo.side);
                    var x = tempNodeInfo.coord.x + dir.x;
                    var y = tempNodeInfo.coord.y + dir.y;
                    var neighbour = Graph.GetCoordInfo(in root, x, y);
                    if (neighbour.IsValid() == true) {
                        queue.Enqueue(new TempNodeTraverse() {
                            node = neighbour,
                            coord = new int2(x, y),
                            side = tempNodeInfo.side,
                        });
                    }
                }
            }

            return position;

        }

        [INLINE(256)]
        public static float3 GetPositionWithMapBorders(in Ent graph, out float3 collisionDirection, in float3 newPos, in float3 prevPos, in Filter filter = default) {

            collisionDirection = float3.zero;
            /*var root = graph.Read<RootGraphComponent>();
            const float offset = 0.1f;
            const float minOffset = 0.01f;
            var localPos = newPos - root.position;
            if (localPos.x < offset || localPos.y < offset || localPos.x > root.width * root.chunkWidth * root.nodeSize - offset || localPos.z > root.height * root.chunkHeight * root.nodeSize - offset) {
                var resultPos = newPos;
                if (localPos.x < offset) resultPos.x = root.position.x + offset + minOffset;
                if (localPos.z < offset) resultPos.z = root.position.z + offset + minOffset;
                if (localPos.x > root.width * root.chunkWidth * root.nodeSize - offset) resultPos.x = root.position.x + root.width * root.chunkWidth * root.nodeSize - offset - minOffset;
                if (localPos.z > root.height * root.chunkHeight * root.nodeSize - offset) resultPos.z = root.position.z + root.height * root.chunkHeight * root.nodeSize - offset - minOffset;
                return resultPos;
            }*/
            
            // if chunk and node are exist
            if (GetPositionWithMapBordersNode(out var node, in graph, in newPos) == true) {
                if (filter.IsValid(in node) == false) {
                    // if previous pos is not walkable too
                    if (GetPositionWithMapBordersNode(out node, in graph, in prevPos) == true) {
                        if (filter.IsValid(in node) == false) {
                            // default clamping doesn't work
                            // so we need to find nearest walkable node
                            var targetPos = GetNearestNodeByFilter(in graph, in newPos, in filter);
                            collisionDirection = math.normalizesafe(targetPos - prevPos);
                            return prevPos;
                        }
                    }
                    // this node is not walkable by filter - clamp prevPos
                    var p1 = new float3(prevPos.x, 0f, newPos.z);
                    if (GetPositionWithMapBordersClamp(in graph, in p1, in prevPos, out var pos, in filter) == false) {
                        var p2 = new float3(newPos.x, 0f, prevPos.z);
                        if (GetPositionWithMapBordersClamp(in graph, in p2, in prevPos, out pos, in filter) == false) {
                            pos = prevPos;
                        }
                    }

                    return pos;
                }
                return newPos;
            }

            return prevPos;

        }

        [INLINE(256)]
        public static bool GetPositionWithMapBordersClamp(in Ent graph, in float3 newPos, in float3 prevPos, out float3 result, in Filter filter = default) {

            if (GetPositionWithMapBordersNode(out var node, in graph, in newPos) == false) {
                result = prevPos;
                return false;
            }
            if (filter.IsValid(in node) == false) {
                result = prevPos;
                return false;
            }
            
            result = newPos;
            return true;

        }

        [INLINE(256)]
        public static tfloat GetObstacleHeight(in float3 localObstaclePosition, in MemArrayAuto<tfloat> obstacleHeights, in float2 obstacleSize, uint obstacleHeightsSizeX) {
            var obstacleHeightsSizeY = obstacleHeights.Length / obstacleHeightsSizeX;
            var x = (int)(localObstaclePosition.x / obstacleSize.x * obstacleHeightsSizeX);
            var y = (int)(localObstaclePosition.z / obstacleSize.y * obstacleHeightsSizeX);
            if (x < 0) x = 0;
            if (x >= obstacleHeightsSizeX) x = (int)obstacleHeightsSizeX - 1;
            if (y < 0) y = 0;
            if (y >= obstacleHeightsSizeY) y = (int)obstacleHeightsSizeY - 1;
            return obstacleHeights[(uint)y * obstacleHeightsSizeX + (uint)x];
        }

        [INLINE(256)]
        public static tfloat GetObstacleHeight(float3 localObstaclePosition, tfloat[] obstacleHeights, in float2 obstacleSize, uint obstacleHeightsSizeX) {
            var x = (int)(localObstaclePosition.x / obstacleSize.x * obstacleHeightsSizeX);
            var y = (int)(localObstaclePosition.z / obstacleSize.y * obstacleHeightsSizeX);
            if (x < 0) x = 0;
            if (x >= obstacleHeightsSizeX) x = (int)obstacleHeightsSizeX - 1;
            if (y < 0) y = 0;
            if (y >= obstacleHeightsSizeX) y = (int)obstacleHeightsSizeX - 1;
            return obstacleHeights[(uint)y * obstacleHeightsSizeX + (uint)x];
        }

    }

}