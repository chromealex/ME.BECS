namespace ME.BECS.Pathfinding {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Mathematics;
    using ME.BECS.Units;
    
    public static class GraphUtils {

        [INLINE(256)]
        public static bool IsGraphMaskValid(in BuildGraphSystem system, in float3 position, in quaternion rotation, float2 size, byte minCost, byte maxCost) {

            foreach (var graph in system.graphs) {
                if (IsGraphMaskValid(in graph, in position, in rotation, size, minCost, maxCost) == false) return false;
            }

            return true;

        }
        
        [INLINE(256)]
        public static unsafe bool IsGraphMaskValid(in Ent graph, in float3 position, in quaternion rotation, float2 size, byte minCost, byte maxCost) {

            var root = graph.Read<RootGraphComponent>();
            var state = graph.World.state;
            var pos = position.xz;
            var posMin = pos - size * 0.5f;
            var posMax = pos + size * 0.5f;
            for (float x = posMin.x; x <= posMax.x; x += root.nodeSize * 0.5f) {
                for (float y = posMin.y; y <= posMax.y; y += root.nodeSize * 0.5f) {
                    var worldPos = new float3(x, 0f, y);
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
        public static Ent CreateGraphMask(in float3 position, in quaternion rotation, float2 size, byte cost = Graph.UNWALKABLE, float height = 0f) {

            var ent = Ent.New();
            return CreateGraphMask(in ent, in position, in rotation, size, cost, height);

        }

        [INLINE(256)]
        public static Ent CreateGraphMask(in Ent ent, in float3 position, in quaternion rotation, float2 size, byte cost = Graph.UNWALKABLE, float height = 0f) {

            ent.Set<ME.BECS.Transforms.TransformAspect>();
            var tr = ent.GetAspect<ME.BECS.Transforms.TransformAspect>();
            tr.position = position;
            tr.rotation = rotation;
            ent.Set(new GraphMaskComponent() {
                offset = float2.zero,
                size = size,
                height = height,
                cost = cost,
                isDirty = true,
            });
            var aspect = ent.GetAspect<QuadTreeAspect>();
            aspect.quadTreeElement.treeIndex = 1;
            aspect.quadTreeElement.ignoreY = true;
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

    }

}