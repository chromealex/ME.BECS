#define NO_INLINE
namespace ME.BECS.Pathfinding {
    
    #if NO_INLINE
    using INLINE = NoInlineAttribute;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Mathematics;
    using ME.BECS.Transforms;
    using Unity.Jobs;
    using ME.BECS.Jobs;

    public static unsafe class Graph {

        public const byte TARGET_BYTE = 255;
        public const byte LOS_BYTE = 254;
        public const byte UNWALKABLE = 255;
        public const float UNWALKABLE_COST = float.MaxValue * 0.5f;
        
        public struct TempNode {

            public static TempNode Invalid => new TempNode() { chunkIndex = uint.MaxValue, nodeIndex = uint.MaxValue };

            public uint chunkIndex;
            public uint nodeIndex;

            [INLINE(256)]
            public bool IsValid() {

                return this.chunkIndex != uint.MaxValue && this.nodeIndex != uint.MaxValue;

            }

        }

        public struct ChunkPathInfo {

            public uint length;
            public PathState pathState;

        }

        [INLINE(256)]
        public static JobHandle UpdatePath(in World world, MemArrayAuto<bool> chunksToUpdate, ref Path path, JobHandle dependsOn) {

            // build flow field for chunk
            var needToRepath = new Unity.Collections.NativeReference<bool>(true, Unity.Collections.Allocator.TempJob);
            dependsOn = Graph.PathUpdate(in world, ref path, in path.graph, chunksToUpdate, path.filter, needToRepath, dependsOn);
            dependsOn = needToRepath.Dispose(dependsOn);
            return dependsOn;

        }

        [INLINE(256)]
        internal static JobHandle PathUpdate(in World world, ref Path path, in Ent graph, MemArrayAuto<bool> chunksToUpdate, in Filter filter, Unity.Collections.NativeReference<bool> updateRequired, JobHandle dependsOn = default) {

            CollectStartPortals(in world, ref path, in graph, chunksToUpdate);
            
            // construct missed chunks
            dependsOn = new PathJob() {
                needToRepath = updateRequired,
                world = world,
                graph = graph,
                filter = filter,
                path = path,
            }.Schedule(dependsOn);
            
            return dependsOn;

        }

        [INLINE(256)]
        private static void CollectStartPortals(in World world, ref Path path, in Ent graph, MemArrayAuto<bool> chunksToUpdate) {
            
            var root = graph.Read<RootGraphComponent>();

            var toPoint = path.to;

            // collect all different unique areas in this chunk
            path.from.As(in world.state->allocator).Clear();
            var pointsAreas = new Unity.Collections.NativeHashMap<uint2, Portal>(4, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < chunksToUpdate.Length; ++i) {
                if (chunksToUpdate[i] == true) {
                    var chunk = root.chunks[world.state, i];
                    for (uint j = 0; j < chunk.portals.list.Count; ++j) {
                        var portal = chunk.portals.list[world.state, j];
                        var key = new uint2(portal.portalInfo.chunkIndex, portal.area);
                        if (pointsAreas.TryAdd(key, portal) == false) {
                            var d = math.lengthsq(portal.position - toPoint);
                            var dist = math.lengthsq(pointsAreas[key].position - toPoint);
                            if (d < dist) {
                                pointsAreas[key] = portal;
                            }
                        }
                    }
                }
            }

            foreach (var kv in pointsAreas) {
                path.from.As(in world.state->allocator).Add(ref world.state->allocator, kv.Value.position);
            }

        }

        [INLINE(256)]
        public static void PathUpdateSync(in World world, ref Path path, in Ent graph, MemArrayAuto<bool> chunksToUpdate, in Filter filter, Unity.Collections.NativeReference<bool> updateRequired) {

            CollectStartPortals(in world, ref path, in graph, chunksToUpdate);
            
            // construct missed chunks
            new PathJob() {
                needToRepath = updateRequired,
                world = world,
                graph = graph,
                filter = filter,
                path = path,
            }.Execute();
            
        }

        [INLINE(256)]
        public static void SetTarget(ref Path path, float3 position, in Filter filter) {
            
            path.to = Graph.ClampPosition(in path.graph, position);
            path.to = GraphUtils.GetNearestNodeByFilter(in path.graph, in path.to, in filter);

        }

        [INLINE(256)]
        public static void MakePath(in World world, out Path path, in Ent graph, in float3 to, in Filter filter) {

            var root = graph.Read<RootGraphComponent>();
            path = new Path() {
                graph = graph,
                to = Graph.ClampPosition(in graph, to),
                filter = filter,
                chunks = new MemArray<Path.Chunk>(ref world.state->allocator, root.width * root.height),
            };
            path.from.Set(ref world.state->allocator, new List<float3>(ref world.state->allocator, 100u));

        }

        [INLINE(256)]
        public static TempNode GetCoordInfo(in RootGraphComponent root, int globalX, int globalY) {
            return GetCoordInfo(globalX, globalY, root.chunkWidth, root.chunkHeight, root.width, root.height);
        }

        [INLINE(256)]
        public static TempNode GetCoordInfo(int globalX, int globalY, uint chunkWidth, uint chunkHeight, uint chunksX, uint chunksY) {

            if (globalX < 0 || globalY < 0) return TempNode.Invalid;
            if (globalX >= chunkWidth * chunksX || globalY >= chunkHeight * chunksY) return TempNode.Invalid;
            var localX = globalX % (int)chunkWidth;
            var localXChunk = (uint)math.floor(globalX / (float)chunkWidth);
            var localY = globalY % (int)chunkHeight;
            var localYChunk = (uint)math.floor(globalY / (float)chunkHeight);
            return new TempNode() {
                chunkIndex = localYChunk * chunksX + localXChunk,
                nodeIndex = (uint)localY * chunkWidth + (uint)localX,
            };

        }

        [INLINE(256)]
        public static int2 GetGlobalCoord(in RootGraphComponent root, float3 position) {

            var chunkIndex = GetChunkIndex(in root, position);
            var nodeIndex = GetNodeIndex(in root, in root.chunks[chunkIndex], position);
            return GetGlobalCoord(chunkIndex, nodeIndex, root.chunkWidth, root.chunkHeight, root.width);
            
        }

        [INLINE(256)]
        public static int2 GetGlobalCoord(uint chunkIndex, uint nodeIndex, uint chunkWidth, uint chunkHeight, uint chunksX) {

            var localX = nodeIndex % chunkWidth;
            var localY = nodeIndex / chunkWidth;
            var localXChunk = chunkIndex % chunksX;
            var localYChunk = chunkIndex / chunksX;
            var globalX = localXChunk * chunkWidth + localX;
            var globalY = localYChunk * chunkHeight + localY;
            return new int2((int)globalX, (int)globalY);

        }
        
        [INLINE(256)]
        public static float3 GetDirection(in World world, float3 position, in Path path, out bool complete) {

            var root = path.graph.Read<RootGraphComponent>();
            var chunkIndex = GetChunkIndex(in root, position, false);
            var chunkComponent = root.chunks[world.state, chunkIndex];
            var nodeIndex = GetNodeIndex(in root, in chunkComponent, position, float3.zero, quaternion.identity, false);
            var chunk = path.chunks[world.state, chunkIndex];
            if (nodeIndex == uint.MaxValue || chunk.flowField.isCreated == false) {
                complete = false;
                return float3.zero;
            }
            var item = chunk.flowField[world.state, nodeIndex];
            complete = (item.direction == Graph.TARGET_BYTE);
            if (item.hasLineOfSight == true) {
                var to = path.to;
                to.y = position.y;
                return math.normalizesafe(to - position);
            }
            return GetDirection(item.direction);

        }

        [INLINE(256)]
        public static float3 GetDirection(byte dir) {

            if (dir == 255) return float3.zero;
            
            var angle = math.lerp(0f, 360f, dir / (float)254) - 45f;
            return math.rotate(quaternion.AxisAngle(new float3(0f, 1f, 0f), math.radians(angle)), new float3(0f, 0f, 1f));
            
        }

        [INLINE(256)]
        public static PathInfo HierarchyPath(State* state, in Ent graph, float3 from, float3 to, in Filter filter, float nodeSize) {

            [INLINE(256)]
            static void TraverseNeighbours(ref Unity.Collections.NativeArray<TempNodeData> temp, 
                                           ref ME.BECS.NativeCollections.NativeMinHeap heap,
                                           in Unity.Collections.NativeArray<uint> chunkPortalsCount, 
                                           in ListAuto<Portal.Connection> list, 
                                           in Unity.Collections.NativeList<Portal> graphNodes,
                                           in Filter filter,
                                           float nodeSize,
                                           float currentNodeCost, 
                                           uint parentIdx,
                                           State* state) {
                
                for (uint i = 0; i < list.Count; ++i) {
                    var n = list[state, i];
                    var key = (int)(chunkPortalsCount[(int)n.portalInfo.chunkIndex] + n.portalInfo.portalIndex);
                    var nTemp = temp[key];
                    if (nTemp.isClosed == true) continue;
                    var cost = currentNodeCost + n.length;
                    if (nTemp.isOpened == false || cost < nTemp.startToCurNodeLen) {
                        nTemp.startToCurNodeLen = cost;
                        nTemp.parent = parentIdx + 1u;
                        if (nTemp.isOpened == false) {
                            heap.Push(new ME.BECS.NativeCollections.MinHeapNode((uint)key, cost));
                            nTemp.isOpened = true;
                        }
                        temp[key] = nTemp;
                    }
                }
                
            }
            
            var pathState = PathState.NotCalculated;
            var root = graph.Read<RootGraphComponent>();
            var portalInfo = GetNearestPortal(state, in root, from, from);
            var toPortalInfo = GetNearestPortal(state, in root, to, to);
            
            // prepare nodes
            var graphNodes = new Unity.Collections.NativeList<Portal>((int)(root.chunks.Length * 4u), Unity.Collections.Allocator.Temp);
            var chunkPortalsCount = new Unity.Collections.NativeArray<uint>((int)(root.chunks.Length), Unity.Collections.Allocator.Temp);
            var count = 0u;
            for (uint i = 0; i < root.chunks.Length; ++i) {
                var chunk = root.chunks[state, i];
                graphNodes.AddRange(chunk.portals.list.GetUnsafePtr(in state->allocator), (int)chunk.portals.list.Count);
                chunkPortalsCount[(int)i] = count;
                count += chunk.portals.list.Count;
            }

            var portalInfoIndex = chunkPortalsCount[(int)portalInfo.chunkIndex] + portalInfo.portalIndex;
            var toPortalInfoIndex = chunkPortalsCount[(int)toPortalInfo.chunkIndex] + toPortalInfo.portalIndex;
            
            /*{
                var p1 = root.chunks[state, portalInfo.chunkIndex].portals.list[state, portalInfo.portalIndex].position;
                var p2 = p1 + math.up() * 10f;
                UnityEngine.Debug.DrawLine(p1, p2);
            }
            {
                var p1 = root.chunks[state, toPortalInfo.chunkIndex].portals.list[state, toPortalInfo.portalIndex].position;
                var p2 = p1 + math.up() * 10f;
                UnityEngine.Debug.DrawLine(p1, p2);
            }*/
            var toPortalNodeArea = root.chunks[state, toPortalInfo.chunkIndex].portals.list[state, toPortalInfo.portalIndex].area;
            Unity.Collections.NativeArray<TempNodeData> temp = default;
            if (portalInfo.IsValid == true && toPortalInfo.IsValid == true) {

                temp = new Unity.Collections.NativeArray<TempNodeData>(graphNodes.Length, Unity.Collections.Allocator.Temp);
                {
                    // recursive find path
                    var heap = new ME.BECS.NativeCollections.NativeMinHeap(graphNodes.Length, Unity.Collections.Allocator.Temp);
                    heap.Push(new ME.BECS.NativeCollections.MinHeapNode(portalInfoIndex, 0f));
                    pathState = PathState.Failed;
                    var tmp = new TempNodeData {
                        startToCurNodeLen = 0f,
                        isOpened = true,
                    };
                    temp[(int)portalInfoIndex] = tmp;
                    while (heap.HasNext() == true) {
                        var index = heap[heap.Pop()].Position;
                        var portal = graphNodes[(int)index];
                        if (portal.portalInfo.chunkIndex == toPortalInfo.chunkIndex &&
                            portal.portalInfo.portalIndex == toPortalInfo.portalIndex) {
                            pathState = PathState.Success;
                            break;
                        }
                        if (portal.portalInfo.chunkIndex == toPortalInfo.chunkIndex && 
                            portal.area == toPortalNodeArea) {

                            temp[(int)toPortalInfoIndex] = new TempNodeData() {
                                parent = index + 1u,
                            };
                            pathState = PathState.Success;
                            break;
                        }
                        
                        var nodeTemp = temp[(int)index];
                        nodeTemp.isClosed = true;
                        temp[(int)index] = nodeTemp;

                        var currentNodeCost = nodeTemp.startToCurNodeLen;

                        TraverseNeighbours(ref temp, ref heap, in chunkPortalsCount, in portal.localNeighbours, in graphNodes, in filter, nodeSize, currentNodeCost, index, state);
                        TraverseNeighbours(ref temp, ref heap, in chunkPortalsCount, in portal.remoteNeighbours, in graphNodes, in filter, nodeSize, currentNodeCost, index, state);
                    }

                    graph.Set(root);
                }

            }
            
            var nodes = new Unity.Collections.NativeList<PortalInfo>(graphNodes.Length, Unity.Collections.Allocator.Temp);
            if (pathState == PathState.Success) {
                var endNodeIndex = toPortalInfoIndex;
                while (temp[(int)endNodeIndex].parent > 0u) {
                    var n = endNodeIndex;
                    endNodeIndex = temp[(int)endNodeIndex].parent - 1u;
                    nodes.Add(graphNodes[(int)n].portalInfo);
                }
                nodes.Add(portalInfo);
                var offset = new float3(0f, 0f, 0f);
                for (uint i = 1; i < nodes.Length; ++i) {
                    var n1 = nodes[(int)i - 1];
                    var n2 = nodes[(int)i];
                    var portal1 = root.chunks[state, n1.chunkIndex].portals.list[state, n1.portalIndex];
                    var portal2 = root.chunks[state, n2.chunkIndex].portals.list[state, n2.portalIndex];
                    var offset2 = offset + new float3(0f, 0.1f, 0f);
                    UnityEngine.Debug.DrawLine(portal1.position + offset, portal2.position + offset2, UnityEngine.Color.yellow);
                    offset += new float3(0f, 0.1f, 0f);
                }
            }

            var pathInfo = new PathInfo() {
                nodes = nodes,
                pathState = pathState,
            };
            return pathInfo;

        }

        private static float3 GetPortalPosition(State* state, in RootGraphComponent root, uint chunkIndex, uint portalIndex) {

            var portal = root.chunks[state, chunkIndex].portals.list[state, portalIndex];
            return portal.position;

        }

        [INLINE(256)]
        private static PortalInfo GetNearestPortal(State* state, in RootGraphComponent root, float3 position, float3 target) {

            var chunkIndex = GetChunkIndex(in root, position);
            if (chunkIndex == uint.MaxValue) return PortalInfo.Invalid;

            uint portalIndex = uint.MaxValue;
            var dist = float.MaxValue;
            var chunk = root.chunks[state, chunkIndex];
            for (uint i = 0; i < chunk.portals.list.Count; ++i) {
                var pos = GetPortalPosition(state, in root, chunkIndex, i);
                var d = math.lengthsq(pos - target);
                if (d < dist) {
                    dist = d;
                    portalIndex = i;
                }
            }

            if (portalIndex == uint.MaxValue) return PortalInfo.Invalid;

            return new PortalInfo() {
                chunkIndex = chunkIndex,
                portalIndex = portalIndex,
            };

        }
        
        [INLINE(256)]
        public static ChunkPathInfo ChunkPath(State* state, in Ent graph, uint chunkIndex, float3 from, float3 to, in Filter filter = default) {

            var root = graph.Read<RootGraphComponent>();
            var chunk = root.chunks[state, chunkIndex];

            var pathState = PathState.NotCalculated;
            var fromNodeIndex = GetNodeIndex(in root, in chunk, from);
            var toNodeIndex = GetNodeIndex(in root, in chunk, to);
            var temp = new Unity.Collections.NativeArray<TempNodeData>((int)chunk.nodes.Length, Unity.Collections.Allocator.Temp);
            {
                // build path
                {
                    var targetPos = GetPosition(in root, in chunk, toNodeIndex);
                    // recursive find path
                    var heap = new ME.BECS.NativeCollections.NativeMinHeap((int)chunk.nodes.Length, Unity.Collections.Allocator.Temp);
                    heap.Push(new ME.BECS.NativeCollections.MinHeapNode(fromNodeIndex, 0f));
                    pathState = PathState.Failed;
                    var tmp = temp[(int)fromNodeIndex];
                    tmp.startToCurNodeLen = 0f;
                    tmp.isOpened = true;
                    temp[(int)fromNodeIndex] = tmp;
                    while (heap.HasNext() == true) {
                        var index = heap[heap.Pop()].Position;
                        //var node = graphInfo.nodes[state, index];
                        if (index == toNodeIndex) {
                            pathState = PathState.Success;
                            break;
                        }
                        
                        var nodeTemp = temp[(int)index];
                        nodeTemp.isClosed = true;
                        temp[(int)index] = nodeTemp;

                        var currentNodeCost = nodeTemp.startToCurNodeLen;
                        for (uint i = 0; i < 4u; ++i) {
                            var dir = GetCoordDirection(i);
                            var nIndex = GetNeighbourIndex(index, dir, root.chunkWidth, root.chunkHeight);
                            var nTemp = temp[(int)nIndex];
                            if (nTemp.isClosed == true) continue;
                            var n = chunk.nodes[state, nIndex];
                            if (filter.IsValid(in n) == false) {
                                continue;
                            }
                            var cost = currentNodeCost + n.cost;
                            var pos = GetPosition(in root, in chunk, nIndex);
                            var dx = math.abs(pos.x - targetPos.x);
                            var dy = math.abs(pos.z - targetPos.z);
                            var distanceToTarget = 0.01f * math.sqrt(dx * dx + dy * dy);
                            cost += distanceToTarget;
                            if (nTemp.isOpened == false || cost < nTemp.startToCurNodeLen) {
                                nTemp.startToCurNodeLen = cost;
                                nTemp.parent = index + 1u;
                                if (nTemp.isOpened == false) {
                                    heap.Push(new ME.BECS.NativeCollections.MinHeapNode(nIndex, cost));
                                    nTemp.isOpened = true;
                                }
                                temp[(int)nIndex] = nTemp;
                            }
                        }
                    }
                }
            }

            var length = 0u;
            if (pathState == PathState.Success) {
                var endNodeIndex = toNodeIndex;
                while (temp[(int)endNodeIndex].parent > 0u) {
                    //var srcNodeIndex = endNodeIndex;
                    endNodeIndex = temp[(int)endNodeIndex].parent - 1u;
                    /*if (endNodeIndex > 0u) {
                        var rnd = Random.CreateFromIndex(chunkIndex);
                        var color = UnityEngine.Color.HSVToRGB(rnd.NextFloat(), 1f, 1f);
                        UnityEngine.Debug.DrawLine(GetPosition(in chunk, endNodeIndex), GetPosition(in chunk, srcNodeIndex), color, 10f);
                    }*/
                    ++length;
                }
            }
            
            var pathInfo = new ChunkPathInfo() {
                pathState = pathState,
                length = length,
            };
            return pathInfo;

        }

        [INLINE(256)]
        internal static uint GetNeighbourIndex(uint baseIndex, int2 direction, uint width, uint height) {
            
            var hasLeft = (baseIndex % width) > 0u;
            var hasRight = (baseIndex % width < (width - 1u));
            var hasDown = (baseIndex > width);
            var hasUp = (baseIndex / width < (height - 1u));

            if (hasLeft == true && hasUp == true && math.all(direction == new int2(-1, 1)) == true) {
                return baseIndex + width - 1u;
            } else if (hasUp == true && math.all(direction == new int2(0, 1)) == true) {
                return baseIndex + width;
            } else if (hasRight == true && hasUp == true && math.all(direction == new int2(1, 1)) == true) {
                return baseIndex + width + 1u;
            } else if (hasLeft == true && math.all(direction == new int2(-1, 0)) == true) {
                return baseIndex - 1u;
            } else if (hasRight == true && math.all(direction == new int2(1, 0)) == true) {
                return baseIndex + 1u;
            } else if (hasDown == true && hasLeft == true && math.all(direction == new int2(-1, -1)) == true) {
                return baseIndex - width - 1u;
            } else if (hasDown == true && math.all(direction == new int2(0, -1)) == true) {
                return baseIndex - width;
            } else if (hasDown == true && hasRight == true && math.all(direction == new int2(1, -1)) == true) {
                return baseIndex - width + 1u;
            }

            return baseIndex;

        }

        [INLINE(256)]
        private static void MoveUp(ref uint index, uint w, uint h) {
            index %= h;
        }
        [INLINE(256)]
        private static void MoveLeft(ref uint index, uint w, uint h) {
            index += w - 1u;
        }
        [INLINE(256)]
        private static void MoveRight(ref uint index, uint w, uint h) {
            index -= w - 1u;
        }
        [INLINE(256)]
        private static void MoveDown(ref uint index, uint w, uint h) {
            index += w * h - w;
        }

        [INLINE(256)]
        private static void MoveLocalUp(ref uint index, uint w, uint h) {
            index += w;
        }
        [INLINE(256)]
        private static void MoveLocalLeft(ref uint index, uint w, uint h) {
            index -= 1u;
        }
        [INLINE(256)]
        private static void MoveLocalRight(ref uint index, uint w, uint h) {
            index += 1u;
        }
        [INLINE(256)]
        private static void MoveLocalDown(ref uint index, uint w, uint h) {
            index -= w;
        }

        [INLINE(256)]
        private static byte GetNeighbourSumDir(in World world, TempNode node, uint width, uint height, MemArray<Path.Chunk> gridChunks, uint chunksX,
                                               uint chunksY, Direction direction) {
            
            var leftDir = (int)direction - 1;
            if (leftDir < 0) leftDir += 8;
            var centerDir = direction;
            var rightDir = (int)direction + 1;
            if (leftDir >= 8) rightDir -= 8;
            var center = GetNeighbourIndex(in world, node, (uint)centerDir, width, height, gridChunks, chunksX, chunksY);
            var left = GetNeighbourIndex(in world, node, (uint)leftDir, width, height, gridChunks, chunksX, chunksY);
            var right = GetNeighbourIndex(in world, node, (uint)rightDir, width, height, gridChunks, chunksX, chunksY);
            var centerCost = center.IsValid() == false ? Graph.UNWALKABLE_COST : gridChunks[world.state, center.chunkIndex].flowField[world.state, center.nodeIndex].bestCost;
            var leftCost = left.IsValid() == false ? Graph.UNWALKABLE_COST : gridChunks[world.state, left.chunkIndex].flowField[world.state, left.nodeIndex].bestCost;
            var rightCost = right.IsValid() == false ? Graph.UNWALKABLE_COST : gridChunks[world.state, right.chunkIndex].flowField[world.state, right.nodeIndex].bestCost;
            
            var max = math.max(leftCost, rightCost);
            var min = math.min(leftCost, rightCost);
            var maxFactor = max;
            var factorLeft = leftCost / maxFactor;
            var factorRight = rightCost / maxFactor;
            var factor = 1f - math.unlerp(centerCost, max, min);
            var sign = math.sign(factorLeft - factorRight);
            var f = sign * math.lerp(0f, 254f / 8f, math.lerp(factorRight * factor, factorLeft * factor, factor + sign * 0.5f));
            if (f < 0f) f += 254f;
            if (f > 254f) f -= 254f;
            return (byte)f;
            
        }

        [INLINE(256)]
        internal static byte GetCommonNeighbourSumDir(in World world, TempNode node, uint direction, uint width, uint height, MemArray<Path.Chunk> gridChunks, uint chunksX,
                                                          uint chunksY) {

            var baseDir = math.lerp(0f, 254f - 254f / 8f, (float)direction / 7u);
            var dir = (Direction)direction;
            if (dir is Direction.UpLeft or Direction.UpRight or Direction.DownLeft or Direction.DownRight) {

                return (byte)(baseDir + GetNeighbourSumDir(in world, node, width, height, gridChunks, chunksX, chunksY, dir));
                
            }

            return (byte)baseDir;

        }

        [INLINE(256)]
        public static int2 GetDirectionBySide(Side side) {

            switch (side) {
                case Side.Up: return new int2(0, 1);
                case Side.Down: return new int2(0, -1);
                case Side.Left: return new int2(-1, 0);
                case Side.Right: return new int2(1, 0);
            }

            return int2.zero;

        }

        [INLINE(256)]
        public static uint GetCoordDirection(uint i) {

            /*
             * 0 1 2
             * 7 * 3
             * 6 5 4
             */

            switch (i) {
                case 0u: return (uint)Direction.Up;
                case 1u: return (uint)Direction.Right;
                case 2u: return (uint)Direction.Down;
                case 3u: return (uint)Direction.Left;
            }

            return (uint)Direction.Up;

        }

        [INLINE(256)]
        internal static TempNode GetNeighbourIndex(in World world, TempNode node, uint direction, uint width, uint height, MemArray<Path.Chunk> gridChunks, uint chunksX, uint chunksY) {

            /*
             * 0 1 2
             * 7 * 3
             * 6 5 4
             */

            // return chunk index and node index in this chunk
            // depends on gridChunks array

            var baseIndex = node.nodeIndex;
            var chunkIndex = node.chunkIndex;
            
            var hasLeft = (baseIndex % width) > 0u;
            var hasRight = (baseIndex % width < (width - 1u));
            var hasDown = (baseIndex > width - 1u);
            var hasUp = (baseIndex / width < (height - 1u));

            var nextIndex = GetNeighbourIndex(baseIndex, direction, width, height);
            if (nextIndex != baseIndex) {
                // we can move in this direction locally
                return new TempNode() {
                    chunkIndex = chunkIndex,
                    nodeIndex = nextIndex,
                };
            }

            int2 offset = default;
            if (direction == (uint)Direction.UpLeft) {
                // move left and up
                // we can't move - try to get neighbour chunk
                offset = new int2(0, 0);
                if (hasLeft == false) {
                    MoveLeft(ref baseIndex, width, height);
                    offset.x = -1;
                } else {
                    MoveLocalLeft(ref baseIndex, width, height);
                }
                if (hasUp == false) {
                    MoveUp(ref baseIndex, width, height);
                    offset.y = 1;
                } else {
                    MoveLocalUp(ref baseIndex, width, height);
                }
            } else if (direction == (uint)Direction.Up) {
                // move up
                // we can't move - try to get neighbour chunk
                offset = new int2(0, 1);
                MoveUp(ref baseIndex, width, height);
            } else if (direction == (uint)Direction.UpRight) {
                // move up and right
                // we can't move - try to get neighbour chunk
                offset = new int2(0, 0);
                if (hasRight == false) {
                    MoveRight(ref baseIndex, width, height);
                    offset.x = 1;
                } else {
                    MoveLocalRight(ref baseIndex, width, height);
                }
                if (hasUp == false) {
                    MoveUp(ref baseIndex, width, height);
                    offset.y = 1;
                } else {
                    MoveLocalUp(ref baseIndex, width, height);
                }
            } else if (direction == (uint)Direction.Left) {
                // move left
                // we can't move - try to get neighbour chunk
                offset = new int2(-1, 0);
                MoveLeft(ref baseIndex, width, height);
            } else if (direction == (uint)Direction.Right) {
                // move right
                // we can't move - try to get neighbour chunk
                offset = new int2(1, 0);
                MoveRight(ref baseIndex, width, height);
            } else if (direction == (uint)Direction.DownLeft) {
                // move down and left
                // we can't move - try to get neighbour chunk
                offset = new int2(0, 0);
                if (hasLeft == false) {
                    MoveLeft(ref baseIndex, width, height);
                    offset.x = -1;
                } else {
                    MoveLocalLeft(ref baseIndex, width, height);
                }
                if (hasDown == false) {
                    MoveDown(ref baseIndex, width, height);
                    offset.y = -1;
                } else {
                    MoveLocalDown(ref baseIndex, width, height);
                }
            } else if (direction == (uint)Direction.Down) {
                // move down
                // we can't move - try to get neighbour chunk
                offset = new int2(0, -1);
                MoveDown(ref baseIndex, width, height);
            } else if (direction == (uint)Direction.DownRight) {
                // move down and right
                // we can't move - try to get neighbour chunk
                offset = new int2(0, 0);
                if (hasRight == false) {
                    MoveRight(ref baseIndex, width, height);
                    offset.x = 1;
                } else {
                    MoveLocalRight(ref baseIndex, width, height);
                }
                if (hasDown == false) {
                    MoveDown(ref baseIndex, width, height);
                    offset.y = -1;
                } else {
                    MoveLocalDown(ref baseIndex, width, height);
                }
            }

            //UnityEngine.Debug.Log(baseIndex + " :: " + direction + " :: " + bIndex);
            var newChunkIndex = GetChunkIndex(chunkIndex, offset.x, offset.y, chunksX, chunksY);
            if (newChunkIndex == uint.MaxValue || (gridChunks.isCreated == true && gridChunks[world.state, newChunkIndex].flowField.isCreated == false)) {
                // there is no chunk in this direction
                return TempNode.Invalid;
            }
            
            return new TempNode() {
                chunkIndex = newChunkIndex,
                nodeIndex = baseIndex,
            };
            
        }
        
        [INLINE(256)]
        internal static uint GetNeighbourIndex(uint baseIndex, uint direction, uint width, uint height) {

            /*
             * 0 1 2
             * 7 * 3
             * 6 5 4
             */

            var hasLeft = (baseIndex % width) > 0u;
            var hasRight = (baseIndex % width < (width - 1u));
            var hasDown = (baseIndex > width - 1u);
            var hasUp = (baseIndex / width < (height - 1u));

            return direction switch {
                (uint)Direction.UpLeft when hasLeft == true && hasUp == true => baseIndex + width - 1u,
                (uint)Direction.Up when hasUp == true => baseIndex + width,
                (uint)Direction.UpRight when hasUp == true && hasRight == true => baseIndex + width + 1u,
                (uint)Direction.Left when hasLeft == true => baseIndex - 1u,
                (uint)Direction.Right when hasRight == true => baseIndex + 1u,
                (uint)Direction.DownLeft when hasDown == true && hasLeft == true => baseIndex - width - 1u,
                (uint)Direction.Down when hasDown == true => baseIndex - width,
                (uint)Direction.DownRight when hasDown == true && hasRight == true => baseIndex - width + 1u,
                _ => baseIndex,
            };

        }

        [INLINE(256)]
        public static float3 GetPosition(State* state, in Ent graphEnt, uint chunkIndex, uint nodeIndex) {

            var root = graphEnt.Read<RootGraphComponent>();
            var chunk = root.chunks[state, chunkIndex];
            return GetPosition(in root, in chunk, nodeIndex);

        }
        
        [INLINE(256)]
        public static float3 GetPosition(in RootGraphComponent root, in ChunkComponent chunk, uint index) {

            var x = index % root.chunkWidth;
            var y = index / root.chunkWidth;
            var offset = new float3(0f);
            //var nodeIndex = GetNodeIndex(in root, x, y);
            //var node = chunk.nodes[root.chunks.ent.World.state, nodeIndex];
            return chunk.center + new float3(x * root.nodeSize + offset.x, 0f, y * root.nodeSize + offset.z);

        }

        [INLINE(256)]
        internal static float3 GetLocalPosition(in RootGraphComponent root, uint index) {

            var x = index % root.chunkWidth;
            var y = index / root.chunkWidth;
            var offset = new float3(0f);
            return new float3(x * root.nodeSize + offset.x, 0f, y * root.nodeSize + offset.z);

        }

        [INLINE(256)]
        public static float3 ClampPosition(in Ent graphEnt, float3 position) {

            var root = graphEnt.Read<RootGraphComponent>();
            var fullWidth = root.width * root.chunkWidth * root.nodeSize;
            var fullHeight = root.height * root.chunkHeight * root.nodeSize;
            var offset = new float3(root.chunkWidth * 0.5f, 0f, root.chunkHeight * 0.5f);

            return new float3(math.clamp(position.x, root.position.x - offset.x, fullWidth + root.position.x - offset.x - root.nodeSize), 0f, math.clamp(position.z, root.position.z - offset.z, fullHeight + root.position.z - offset.z - root.nodeSize));
            
        }

        [INLINE(256)]
        public static uint GetChunkIndex(in RootGraphComponent root, float3 position, bool clamp = true) {
            
            var offset = new float3(root.nodeSize * 0.5f);
            offset.y = 0f;
            var width = root.width * root.chunkWidth;
            var height = root.height * root.chunkHeight;
            var localPos = position - root.position + offset;
            if (clamp == true) {
                localPos.x = math.clamp(localPos.x, 0f, (width - 1u) * root.nodeSize);
                localPos.z = math.clamp(localPos.z, 0f, (height - 1u) * root.nodeSize);
            }
            var chunkX = (int)(localPos.x / root.chunkWidth);
            var chunkY = (int)(localPos.z / root.chunkHeight);
            if (chunkX < 0 || chunkY < 0) return uint.MaxValue;
            if (chunkX >= root.width || chunkY >= root.height) return uint.MaxValue;
            
            return (uint)chunkY * root.width + (uint)chunkX;

        }

        [INLINE(256)]
        internal static uint GetChunkIndex(uint chunkIndex, int x, int y, uint chunksX, uint chunksY) {

            var chunkX = chunkIndex % chunksX;
            var chunkY = chunkIndex / chunksX;
            if (chunkX + x < 0 || chunkX + x >= chunksX) return uint.MaxValue;
            if (chunkY + y < 0 || chunkY + y >= chunksY) return uint.MaxValue;
            
            var offset = y * (int)chunksX + x;
            if ((int)chunkIndex + offset < 0 ||
                chunkIndex + offset >= chunksX * chunksY) {
                return uint.MaxValue;
            }
            
            return chunkIndex + (uint)(offset);

        }

        [INLINE(256)]
        internal static uint GetNodeIndex(in RootGraphComponent root, in ChunkComponent chunk, float3 pos, float3 rotPoint, quaternion rotation, bool clamp = true) {
            return GetNodeIndex(in root, in chunk, math.mul(rotation, pos - rotPoint) + rotPoint, clamp);
        }

        [INLINE(256)]
        internal static uint GetNodeIndex(in RootGraphComponent root, in ChunkComponent chunk, float3 pos, bool clamp = true) {

            var offset = new float3(root.nodeSize * 0.5f);
            offset.y = 0f;
            var localPos = pos - chunk.center + offset;
            if (clamp == true) {
                localPos.x = math.clamp(localPos.x, 0f, (root.chunkWidth - 1u) * root.nodeSize);
                localPos.z = math.clamp(localPos.z, 0f, (root.chunkHeight - 1u) * root.nodeSize);
            }

            var x = (int)(localPos.x / root.nodeSize);
            var z = (int)(localPos.z / root.nodeSize);
            if (x < 0 || z < 0) return uint.MaxValue;
            if (x >= root.chunkWidth || z >= root.chunkHeight) return uint.MaxValue;
            
            return (uint)z * root.chunkWidth + (uint)x;

        }

        [INLINE(256)]
        internal static uint GetNodeIndex(in RootGraphComponent root, uint x, uint y) {
            return y * root.chunkWidth + x;
        }

        [INLINE(256)]
        public static JobHandle UpdateObstacles(in World world, in Ent graph, Unity.Collections.NativeArray<bool> changedChunks, JobHandle dependsOn) {

            // update chunks
            var root = graph.Read<RootGraphComponent>();
            var buildChunks = new UpdateChunksJob() {
                graph = graph,
                world = world,
                chunks = root.chunks,
                changedChunks = changedChunks,
            }.Schedule(dependsOn);

            var results = new Unity.Collections.NativeList<ResultItem>((int)(root.chunks.Length * root.chunks.Length), Unity.Collections.Allocator.TempJob);
            // calculate portal connections inside chunk
            var localJobHandle = new CalculateConnectionsJob() {
                world = world,
                graph = graph,
                chunks = root.chunks,
                results = results.AsParallelWriter(),
                changedChunks = changedChunks,
            }.Schedule((int)root.chunks.Length, (int)JobUtils.GetScheduleBatchCount(root.chunks.Length), buildChunks);
            var addConnectionsHandle = new AddConnectionsJob() {
                world = world,
                graph = graph,
                results = results,
            }.Schedule(localJobHandle);
            dependsOn = results.Dispose(addConnectionsHandle);
            return dependsOn;

        }

        [INLINE(256)]
        public static JobHandle Build(in World world, in Heights heights, out Ent graph, in GraphProperties properties, in ME.BECS.Units.AgentType agentConfig, JobHandle dependsOn = default) {

            graph = Ent.New();
            graph.Set<TransformAspect>();
            return Build(in graph, in heights, in world, in properties, in agentConfig, dependsOn);

        }

        [INLINE(256)]
        private static JobHandle Build(in Ent graph, in Heights heights, in World world, in GraphProperties properties, in ME.BECS.Units.AgentType agentConfig, JobHandle dependsOn = default) {

            var chunks = new MemArrayAuto<ChunkComponent>(in graph, properties.chunksCountX * properties.chunksCountY);
            graph.Set(new RootGraphComponent() {
                chunks = chunks,
                agentRadius = agentConfig.radius,
                agentMaxSlope = agentConfig.maxSlope,
                properties = properties,
            });

            var results = new Unity.Collections.NativeList<ResultItem>((int)(chunks.Length * chunks.Length), Unity.Collections.Allocator.TempJob);
            var changedChunks = new Unity.Collections.NativeArray<bool>(1, Unity.Collections.Allocator.TempJob);
            var buildChunks = new BuildChunksJob() {
                graph = graph,
                world = world,
                chunks = chunks,
                heights = heights,
            }.Schedule(dependsOn);
            var buildSlopes = new BuildSlopeJob() {
                graph = graph,
                world = world,
                chunks = chunks,
            }.Schedule(buildChunks);
            // calculate portal connections inside chunk
            var localJobHandle = new CalculateConnectionsJob() {
                world = world,
                graph = graph,
                chunks = chunks,
                results = results.AsParallelWriter(),
                changedChunks = changedChunks,
            }.Schedule((int)chunks.Length, (int)JobUtils.GetScheduleBatchCount(chunks.Length), buildSlopes);
            var addConnectionsHandle = new AddConnectionsJob() {
                world = world,
                graph = graph,
                results = results,
            }.Schedule(localJobHandle);
            return JobHandle.CombineDependencies(changedChunks.Dispose(localJobHandle), results.Dispose(addConnectionsHandle));

        }

        [INLINE(256)]
        private static void ApplyMapBorder(in World world, ref MemArray<Node> nodes, uint startNodeIndex, uint size, int2 direction, uint width, uint height) {

            var nodeIndex = startNodeIndex;
            for (uint i = 0u; i <= size; ++i) {
                nodes[world.state, nodeIndex].cost = Graph.UNWALKABLE;
                nodeIndex = GetNeighbourIndex(nodeIndex, direction, width, height);
            }

        }

        [INLINE(256)]
        public static ChunkComponent CreateChunk(in World world, in Heights heights, in Ent graph, uint chunkIndex, float3 center) {

            var root = graph.Read<RootGraphComponent>();
            var nodes = new MemArray<Node>(ref world.state->allocator, root.chunkWidth * root.chunkHeight);
            {
                // initialize nodes
                for (uint i = 0; i < nodes.Length; ++i) {
                    var localPos = GetLocalPosition(in root, i);
                    var pos = localPos + center;
                    var height = heights.GetHeight(pos, out var normal);
                    nodes[world.state, i] = new Node() {
                        cost = 1,
                        height = height,
                        normal = normal,
                    };
                }
                
                if (chunkIndex < root.width) ApplyMapBorder(in world, ref nodes, 0u, root.chunkWidth, new int2(1, 0), root.chunkWidth, root.chunkHeight);
                if (chunkIndex % root.width == 0u) ApplyMapBorder(in world, ref nodes, 0u, root.chunkHeight, new int2(0, 1), root.chunkWidth, root.chunkHeight);
                if ((chunkIndex + 1u) % root.width == 0u) ApplyMapBorder(in world, ref nodes, root.chunkWidth * root.chunkHeight - 1u, root.chunkWidth, new int2(0, -1), root.chunkWidth, root.chunkHeight);
                if (chunkIndex >= (root.width * (root.height - 1u))) ApplyMapBorder(in world, ref nodes, root.chunkWidth * root.chunkHeight - 1u, root.chunkHeight, new int2(-1, 0), root.chunkWidth, root.chunkHeight);
            }

            var graphComponent = new ChunkComponent() {
                center = center,
                nodes = nodes,
                cache = ChunkCache.Create(world.state, nodes.Length * 4u),
                obstaclesQuery = Ent.New(),
            };
            graphComponent.obstaclesQuery.SetParent(graph);
            graphComponent.obstaclesQuery.Set(new ChunkObstacleQuery());
            var tr = graphComponent.obstaclesQuery.GetOrCreateAspect<TransformAspect>();
            tr.position = center;
            var query = graphComponent.obstaclesQuery.GetOrCreateAspect<QuadTreeQueryAspect>();
            var size = new float3(root.chunkWidth * root.nodeSize * 0.5f, 0f, root.chunkHeight * root.nodeSize * 0.5f);
            query.query.ignoreY = true;
            query.query.treeMask = 0;//1 << buildGraphSystem.obstaclesTreeIndex;
            query.query.range = math.length(size) * 2f;
            
            UpdateChunk(in world, in graph, chunkIndex, ref graphComponent, default, true);
            
            return graphComponent;
            
        }
        
        [INLINE(256)]
        public static void Stamp(in World world, in RootGraphComponent root, in ChunkComponent chunkComponent, float3 position, quaternion rotation, float3 size, byte cost) {
            var posMin = position - size * 0.5f;
            var posMax = position + size * 0.5f;
            for (float x = posMin.x; x <= posMax.x; x += root.nodeSize * 0.5f) {
                for (float y = posMin.z; y <= posMax.z; y += root.nodeSize * 0.5f) {
                    var worldPos = new float3(x, 0f, y);
                    var graphPos = math.mul(rotation, worldPos - position) + position;
                    var nodeIndex = GetNodeIndex(in root, in chunkComponent, graphPos, clamp: false);
                    if (nodeIndex == uint.MaxValue) continue;
                    ref var node = ref chunkComponent.nodes[world.state, nodeIndex];
                    node.cost = cost;
                }
            }
        }

        [INLINE(256)]
        public static bool UpdateChunk(in World world, in Ent graph, uint chunkIndex, ref ChunkComponent chunkComponent, Unity.Collections.NativeArray<bool> changedChunks, bool forced = false) {
            
            var root = graph.Read<RootGraphComponent>();
            var changed = false;
            // apply obstacles
            {
                var marker = new Unity.Profiling.ProfilerMarker("Apply Obstacles");
                marker.Begin();
                UnityEngine.Rect chunkBounds;
                {
                    var min = GetPosition(in root, in chunkComponent, 0u);
                    var max = GetPosition(in root, in chunkComponent, chunkComponent.nodes.Length - 1u);
                    chunkBounds = UnityEngine.Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                }

                var agentRadius = root.agentRadius;
                var worldCopy = world;
                var obstacleSizeOffset = new float2(agentRadius * 2f);
                var query = chunkComponent.obstaclesQuery.GetAspect<QuadTreeQueryAspect>();
                for (uint i = 0u; i < query.results.results.Count; ++i) {
                    var obstacleEnt = query.results.results[world.state, i];
                    var obstacle = obstacleEnt.Read<GraphMaskComponent>();
                    if (obstacle.isDirty == false) continue;
                    
                    var tr = obstacleEnt.GetAspect<TransformAspect>();
                    var position = tr.position;
                    var rotation = tr.rotation;
                    var pos = new float3(position.x + obstacle.offset.x, 0f, position.z + obstacle.offset.y);
                    var size = new float3(obstacle.size.x + obstacleSizeOffset.x, 0f, obstacle.size.y + obstacleSizeOffset.y);

                    // aabb min-max
                    var p1 = math.mul(rotation, pos - size * 0.5f - position) + position;
                    var p2 = math.mul(rotation, pos + new float3(-size.x, 0f, size.z) * 0.5f - position) + position;
                    var p3 = math.mul(rotation, pos + size * 0.5f - position) + position;
                    var p4 = math.mul(rotation, pos + new float3(size.x, 0f, -size.z) * 0.5f - position) + position;
                    var min = new float2(math.min(math.min(p1.x, p2.x), math.min(p3.x, p4.x)), math.min(math.min(p1.z, p2.z), math.min(p3.z, p4.z)));
                    var max = new float2(math.max(math.max(p1.x, p2.x), math.max(p3.x, p4.x)), math.max(math.max(p1.z, p2.z), math.max(p3.z, p4.z)));
                    var aabb = new UnityEngine.Rect(min, max - min);

                    // intersection check
                    if (chunkBounds.Overlaps(aabb) == false) continue;

                    var posMin = pos - size * 0.5f;
                    var posMax = pos + size * 0.5f;
                    for (float x = posMin.x; x <= posMax.x; x += root.nodeSize * 0.5f) {
                        for (float y = posMin.z; y <= posMax.z; y += root.nodeSize * 0.5f) {
                            var worldPos = new float3(x, 0f, y);
                            var graphPos = math.mul(rotation, worldPos - position) + position;
                            var nodeIndex = GetNodeIndex(in root, in chunkComponent, graphPos, clamp: false);
                            if (nodeIndex == uint.MaxValue) continue;
                            ref var node = ref chunkComponent.nodes[worldCopy.state, nodeIndex];
                            if (obstacle.cost > node.cost) {
                                node.cost = obstacle.cost;
                                node.height = obstacle.height;
                                changed = true;
                            }
                        }
                    }
                }

                marker.End();
            }
            
            if (forced == true || changed == true) {
                // calculate portals
                var marker = new Unity.Profiling.ProfilerMarker("Calculate Portals");
                marker.Begin();
                chunkComponent.cache.InvalidateCache(ref world.state->allocator, in chunkComponent);
                CalculatePortals(in graph, chunkIndex, ref chunkComponent, in world, changedChunks);
                marker.End();
            }
            
            if (changedChunks.IsCreated == true && changed == true) {
                changedChunks[(int)chunkIndex] = true;
            }
            
            return changed;
            
        }

        [INLINE(256)]
        internal static uint GetNeighbourChunkIndex(uint chunkIndex, Side side, uint chunksX, uint chunksY) {
            uint chunkIdx = uint.MaxValue;
            if (side == Side.Down) {
                chunkIdx = GetChunkIndex(chunkIndex, 0, -1, chunksX, chunksY);
            } else if (side == Side.Up) {
                chunkIdx = GetChunkIndex(chunkIndex, 0, 1, chunksX, chunksY);
            } else if (side == Side.Left) {
                chunkIdx = GetChunkIndex(chunkIndex, -1, 0, chunksX, chunksY);
            } else if (side == Side.Right) {
                chunkIdx = GetChunkIndex(chunkIndex, 1, 0, chunksX, chunksY);
            }

            return chunkIdx;
        }

        [INLINE(256)]
        internal static bool HasPortal(in World world, in ChunkComponent chunk, in Portal neighbourPortal, out uint portalIndex) {

            portalIndex = uint.MaxValue;
            var found = false;
            for (uint i = 0; i < chunk.portals.list.Count; ++i) {
                var portal = chunk.portals.list[world.state, i];
                // look up for opposite side
                var sideCheck = false;
                if (neighbourPortal.side == Side.Down && portal.side == Side.Up) {
                    sideCheck = true;
                } else if (neighbourPortal.side == Side.Up && portal.side == Side.Down) {
                    sideCheck = true;
                } else if (neighbourPortal.side == Side.Left && portal.side == Side.Right) {
                    sideCheck = true;
                } else if (neighbourPortal.side == Side.Right && portal.side == Side.Left) {
                    sideCheck = true;
                }

                if (sideCheck == true) {
                    // check if connection overlaps
                    found = (neighbourPortal.rangeEnd > portal.rangeStart && neighbourPortal.rangeStart < portal.rangeEnd);
                    if (found == true) {
                        portalIndex = i;
                        break;
                    }
                }
            }

            return found;

        }

        [INLINE(256)]
        private static void CalculatePortals(in Ent graph, uint chunkIndex, ref ChunkComponent chunkComponent, in World world, Unity.Collections.NativeArray<bool> changedChunks) {

            if (chunkComponent.portals.list.isCreated == false) chunkComponent.portals.list = new List<Portal>(ref world.state->allocator, 10u);
            // clean up neighbours for each portal of this chunk
            var root = graph.Read<RootGraphComponent>();
            for (uint i = 0; i < chunkComponent.portals.list.Count; ++i) {
                ref var portal = ref chunkComponent.portals.list[world.state, i];
                // for each remote neighbour - clean up
                for (uint j = 0; j < portal.remoteNeighbours.Count; ++j) {
                    var remoteInfo = portal.remoteNeighbours[j];
                    var sourceInfo = remoteInfo.portalInfo;
                    ref var remotePortal = ref root.chunks[world.state, sourceInfo.chunkIndex].portals.list[world.state, sourceInfo.portalIndex];
                    remotePortal.remoteNeighbours.Clear();
                    if (changedChunks.IsCreated == true) {
                        changedChunks[(int)remotePortal.portalInfo.chunkIndex] = true;
                    }
                }
                portal.remoteNeighbours.Dispose();
                portal.localNeighbours.Dispose();
            }

            chunkComponent.portals.list.Clear();
            var area = 0u;
            CalculatePortalsAlongAxis(in graph, chunkIndex, in root, ref chunkComponent, in world, root.chunkWidth, 1u, 0u, 0u, Side.Down, ref area);
            CalculatePortalsAlongAxis(in graph, chunkIndex, in root, ref chunkComponent, in world, root.chunkWidth, 1u, 0u, root.chunkHeight - 1u, Side.Up, ref area);
            CalculatePortalsAlongAxis(in graph, chunkIndex, in root, ref chunkComponent, in world, root.chunkHeight, 0u, 1u, 0u, Side.Left, ref area);
            CalculatePortalsAlongAxis(in graph, chunkIndex, in root, ref chunkComponent, in world, root.chunkHeight, 0u, 1u, root.chunkWidth - 1u, Side.Right, ref area);
            
        }

        [INLINE(256)]
        private static void CalculatePortalsAlongAxis(in Ent graph, uint chunkIndex, in RootGraphComponent root, ref ChunkComponent chunkComponent, in World world, uint length, uint xMultiplier, uint yMultiplier, uint axisOffset, Side side, ref uint area) {

            if (GetNeighbourChunkIndex(chunkIndex, side, root.width, root.height) == uint.MaxValue) return;
            
            var rangeStarted = false;
            var rangeStart = 0u;
            for (uint x = 0u; x < length; ++x) {
                var idx = GetNodeIndex(in root, xMultiplier * x + yMultiplier * axisOffset, yMultiplier * x + xMultiplier * axisOffset);
                var node = chunkComponent.nodes[world.state, idx];
                if (node.walkable == true && rangeStarted == false) {
                    // remember walkable node
                    rangeStart = x;
                    rangeStarted = true;
                }

                if ((node.walkable == false || x == length - 1u) && rangeStarted == true) {
                    // close range
                    var middlePoint = (x + rangeStart) / 2u;
                    var size = x - rangeStart + 1u;
                    var rangeIdx = GetNodeIndex(in root, xMultiplier * middlePoint + yMultiplier * axisOffset, yMultiplier * middlePoint + xMultiplier * axisOffset);
                    var pos = GetPosition(in root, in chunkComponent, rangeIdx);
                    chunkComponent.portals.list.Add(ref world.state->allocator, new Portal() {
                        area = ++area,
                        portalInfo = new PortalInfo() { chunkIndex = chunkIndex, portalIndex = chunkComponent.portals.list.Count },
                        position = pos,
                        rangeStartNodeIndex = GetNodeIndex(in root, xMultiplier * rangeStart + yMultiplier * axisOffset, yMultiplier * rangeStart + xMultiplier * axisOffset),
                        rangeStart = rangeStart,
                        size = size,
                        side = side,
                        axis = new uint2(xMultiplier, yMultiplier),
                        remoteNeighbours = new ListAuto<Portal.Connection>(graph, 1u),
                        localNeighbours = new ListAuto<Portal.Connection>(graph, 4u),
                    });
                    rangeStarted = false;
                }
            }
            
        }

        public struct GizmosParameters {

            public bool drawNormals;

        }

        [INLINE(256)]
        public static void DrawGizmos(Path path, GizmosParameters parameters) {

            if (path.IsCreated == false) return;

            var world = path.graph.World;
            
            var offset = new float3(0f, 0.02f, 0f);
            for (uint i = 0; i < path.chunks.Length; ++i) {
                var chunk = path.chunks[world.state, i];
                var chunkIndex = chunk.index;
                for (uint j = 0; j < chunk.flowField.Length; ++j) {
                    var nodeIndex = j;
                    var item = chunk.flowField[world.state, j];
                    var pos = Graph.GetPosition(world.state, in path.graph, chunkIndex, nodeIndex) + offset;
                    pos.y = path.graph.Read<RootGraphComponent>().chunks[chunkIndex].nodes[world.state, nodeIndex].height + offset.y;
                    if (item.direction == Graph.LOS_BYTE && item.hasLineOfSight == true) {
                        var dir3d = math.normalizesafe(path.to - pos);
                        UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                        Graph.DrawGizmosArrow(pos - dir3d * 0.25f, dir3d * 0.5f);
                    } else {
                        UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
                        Graph.DrawGizmosArrow(pos - Graph.GetDirection(item.direction) * 0.25f, Graph.GetDirection(item.direction) * 0.5f);
                    }
                    /*if (Unity.Mathematics.math.lengthsq(pos - root.chunks[world.state, chunkIndex].center) <= 100f) {
                        var node = path.chunks[world.state, chunkIndex].bestCost[world.state, nodeIndex];
                        UnityEditor.Handles.Label(pos, node.ToString() + "\n" + dir);
                    }*/
                }
            }
            
        }

        [INLINE(256)]
        public static void DrawGizmos(Ent graph, GizmosParameters parameters) {

            var offset = new float3(0f, 0.02f, 0f);
            var root = graph.Read<RootGraphComponent>();
            var state = graph.World.state;
            for (uint i = 0; i < root.chunks.Length; ++i) {

                var chunk = root.chunks[state, i];
                
                var color = UnityEngine.Color.white;
                UnityEngine.Gizmos.color = color;
                DrawGizmosLevel(state, graph, i, in chunk, in root, color, offset, parameters);

                var portals = chunk.portals;
                for (uint j = 0; j < portals.list.Count; ++j) {
                    var portal = portals.list[state, j];
                    {
                        var xMultiplier = portal.axis.x;
                        var yMultiplier = portal.axis.y;
                        var size = portal.size;
                        var c = UnityEngine.Color.yellow;
                        c.a = 0.3f;
                        UnityEngine.Gizmos.color = c;
                        UnityEngine.Gizmos.DrawCube(portal.position + offset, new UnityEngine.Vector3(xMultiplier * size, 2f, yMultiplier * size));
                        
                        c = UnityEngine.Color.HSVToRGB(Random.CreateFromIndex(portal.area).NextFloat(), 1f, 1f);
                        c.a = 0.3f;
                        UnityEngine.Gizmos.color = c;
                        UnityEngine.Gizmos.DrawCube(portal.position + offset, UnityEngine.Vector3.one);
                    }

                    // draw hierarchy graph
                    {
                        // local connections
                        for (uint n = 0u; n < portal.localNeighbours.Count; ++n) {
                            var neighbour = portal.localNeighbours[n];
                            var info = neighbour.portalInfo;
                            var targetPortal = root.chunks[state, info.chunkIndex].portals.list[state, info.portalIndex];
                            var c = UnityEngine.Color.HSVToRGB(Random.CreateFromIndex(portal.area).NextFloat(), 1f, 1f);
                            c.a = 1f;
                            UnityEngine.Gizmos.color = c;
                            var dir = (targetPortal.position - portal.position);
                            var dirNorm = math.normalizesafe(dir);
                            DrawGizmosArrow(portal.position + dirNorm * 1f + offset, dir - dirNorm * 2f + offset);
                        }
                        
                        // remote connections
                        for (uint n = 0u; n < portal.remoteNeighbours.Count; ++n) {
                            var neighbour = portal.remoteNeighbours[n];
                            var info = neighbour.portalInfo;
                            var targetPortal = root.chunks[state, info.chunkIndex].portals.list[state, info.portalIndex];
                            var c = UnityEngine.Color.white;
                            UnityEngine.Gizmos.color = c;
                            var dir = (targetPortal.position - portal.position);
                            DrawGizmosArrow(portal.position + offset, dir + offset);
                        }
                    }

                }
                
            }
            
        }

        [INLINE(256)]
        private static void DrawGizmosLevel(State* state, in Ent graph, uint chunkIndex, in ChunkComponent chunk, in RootGraphComponent rootGraph, UnityEngine.Color color, float3 offsetBase, GizmosParameters parameters) {
            
            var cellSize = new float3(rootGraph.nodeSize, 0f, rootGraph.nodeSize);
            for (uint i = 0; i < rootGraph.chunkWidth; ++i) {
                for (uint j = 0; j < rootGraph.chunkHeight; ++j) {
                    var index = i + j * rootGraph.chunkWidth;
                    var node = chunk.nodes[state, index];
                    var offset = new float3(i * cellSize.x, node.height, j * cellSize.z) + offsetBase;
                    color.a = 0.05f;
                    UnityEngine.Gizmos.color = color;
                    UnityEngine.Gizmos.DrawWireCube(chunk.center + offset, cellSize);
                    UnityEngine.Gizmos.color = UnityEngine.Color.Lerp(UnityEngine.Color.clear, UnityEngine.Color.red, node.cost / (float)255 * 0.8f);
                    var h = math.max(0.01f, node.height);
                    offset.y = h * 0.5f + offsetBase.y;
                    var size = cellSize * 0.5f;
                    size.y = h;
                    UnityEngine.Gizmos.DrawCube(chunk.center + offset, size);
                    if (parameters.drawNormals == true) {
                        UnityEngine.Gizmos.color = UnityEngine.Color.white;
                        UnityEngine.Gizmos.DrawRay(chunk.center + offset, node.normal);
                    }
                }
            }

        }

        [INLINE(256)]
        public static void DrawGizmosArrow(UnityEngine.Vector3 pos, UnityEngine.Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
            if (direction == UnityEngine.Vector3.zero) return;
            UnityEngine.Gizmos.DrawRay(pos, direction);
       
            UnityEngine.Vector3 right = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0f, 180f + arrowHeadAngle, 0f) * new UnityEngine.Vector3(0f, 0f, 1f);
            UnityEngine.Vector3 left = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0f, 180f - arrowHeadAngle, 0f) * new UnityEngine.Vector3(0f, 0f, 1f);
            UnityEngine.Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            UnityEngine.Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }

        [INLINE(256)]
        public static bool IsSlopeValid(float maxSlopeAngle, float nodeHeight, float neighbourNodeHeight, float nodeSize) {

            var delta = math.abs(neighbourNodeHeight - nodeHeight);
            var angle = delta / nodeSize * 45f;
            return angle <= maxSlopeAngle;

        }

    }

}