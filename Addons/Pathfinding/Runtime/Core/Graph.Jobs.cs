#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Jobs;
    using ME.BECS.Jobs;
    using static Cuts;
    
    public struct ResultItem {

        public uint length;
        public uint from;
        public uint to;
        public uint chunkIndex;
        public uint toChunkIndex;

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct BuildSlopeJob : IJob {

        public Ent graph;
        public World world;
        public MemArrayAuto<ChunkComponent> chunks;

        public void Execute() {

            var root = this.graph.Read<RootGraphComponent>();
            for (uint x = 0u; x < root.width; ++x) {
                for (uint y = 0u; y < root.height; ++y) {
                    var idx = y * root.width + x;
                    var chunk = this.chunks[this.world.state, idx];
                    
                    // apply slope
                    for (uint i = 0u; i < chunk.nodes.Length; ++i) {
                        var node = chunk.nodes[this.world.state, i];
                        for (uint j = 0u; j < 4u; ++j) {
                            var dir = Graph.GetCoordDirection(j);
                            var neighbour = Graph.GetNeighbourIndex(in this.world, new Graph.TempNode() { chunkIndex = idx, nodeIndex = i }, dir, root.chunkWidth, root.chunkHeight, default, root.width, root.height);
                            if (neighbour.IsValid() == false) continue;
                            ref var neighbourNode = ref this.chunks[this.world.state, neighbour.chunkIndex].nodes[this.world.state, neighbour.nodeIndex];
                            if (Graph.IsSlopeValid(root.agentMaxSlope, node.height, neighbourNode.height, root.nodeSize) == false) {
                                // stamp
                                Graph.Stamp(in this.world, in root, in chunk, Graph.GetPosition(in root, in chunk, i), quaternion.identity, new float3(root.agentRadius * 2f, 0f, root.agentRadius * 2f), Graph.UNWALKABLE, ObstacleChannel.Slope);
                            }
                        }
                    }

                }
            }
                
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct BuildChunksJob : IJob {

        public Ent graph;
        public World world;
        public MemArrayAuto<ChunkComponent> chunks;
        public Heights heights;

        public void Execute() {

            var root = this.graph.Read<RootGraphComponent>();
            for (uint x = 0u; x < root.width; ++x) {
                for (uint y = 0u; y < root.height; ++y) {
                    var chunkOffset = new float3(root.nodeSize * root.chunkWidth * x, 0f, root.nodeSize * root.chunkHeight * y);
                    var offset = root.position + chunkOffset;
                    var idx = y * root.width + x;
                    var chunk = Graph.CreateChunk(in this.world, in this.heights, in this.graph, idx, offset);
                    this.chunks[this.world.state, idx] = chunk;
                }
            }
            
        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct UpdateChunksJob : IJob {

        public Ent graph;
        public World world;
        public MemArrayAuto<ChunkComponent> chunks;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction]
        public Unity.Collections.NativeArray<ulong> changedChunks;

        public void Execute() {

            var root = this.graph.Read<RootGraphComponent>();
            for (uint idx = 0u; idx < root.chunks.Length; ++idx) {
                ref var chunk = ref this.chunks[this.world.state, idx];
                Graph.UpdateChunk(in this.world, this.graph, idx, ref chunk, this.changedChunks);
            }
            
            for (uint idx = 0u; idx < root.chunks.Length; ++idx) {
                ref var chunk = ref this.chunks[this.world.state, idx];
                if (this.changedChunks.IsCreated == true && this.changedChunks[(int)idx] != this.world.CurrentTick) continue;
                Graph.BuildPortals(in this.graph, idx, ref chunk, in this.world);
            }

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct CalculateConnectionsJob : Unity.Jobs.IJobParallelFor {

        public World world;
        public Ent graph;
        public MemArrayAuto<ChunkComponent> chunks;
        public Unity.Collections.NativeList<ResultItem>.ParallelWriter results;
        [Unity.Collections.ReadOnlyAttribute]
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute]
        public Unity.Collections.NativeArray<ulong> changedChunks;

        public void Execute(int index) {

            var chunkIndex = (uint)index;
            if (this.changedChunks.IsCreated == false || this.changedChunks[index] == this.world.CurrentTick) {
                // calculate portals connections
                var root = this.graph.Read<RootGraphComponent>();
                var chunk = this.chunks[this.world.state, chunkIndex];
                for (uint i = 0; i < chunk.portals.list.Count; ++i) {
                    var portal = chunk.portals.list[this.world.state, i];
                    for (uint j = i + 1u; j < chunk.portals.list.Count; ++j) {
                        var other = chunk.portals.list[this.world.state, j];
                        var path = Graph.ChunkPath(this.world.state, in this.graph, chunkIndex, portal.position, other.position);
                        if (path.pathState == PathState.Success) {
                            // add local connection
                            this.results.AddNoResize(new ResultItem() {
                                chunkIndex = chunkIndex,
                                toChunkIndex = chunkIndex,
                                length = path.length,
                                from = i,
                                to = j,
                            });
                        }
                    }

                    { // remote connections
                        var neighbourChunkIdx = Graph.GetNeighbourChunkIndex(chunkIndex, portal.side, root.width, root.height);
                        if (neighbourChunkIdx == uint.MaxValue) continue;
                        var neighbourChunk = root.chunks[this.world.state, neighbourChunkIdx];
                        if (Graph.HasPortal(in this.world, in neighbourChunk, in portal, out var neighbourPortalIndex) == true) {
                            // add connection between portals
                            this.results.AddNoResize(new ResultItem() {
                                chunkIndex = chunkIndex,
                                toChunkIndex = neighbourChunkIdx,
                                length = 1u,
                                from = i,
                                to = neighbourPortalIndex,
                            });
                        }
                    }
                }
            }

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct AddConnectionsJob : IJob {

        public World world;
        public Ent graph;
        public Unity.Collections.NativeList<ResultItem> results;
        
        public void Execute() {

            var root = this.graph.Read<RootGraphComponent>();
            foreach (var item in this.results) {

                var fromChunkIndex = item.chunkIndex;
                var toChunkIndex = item.toChunkIndex;
                var fromPortalIndex = item.from;
                var toPortalIndex = item.to;

                if (fromChunkIndex == toChunkIndex) {
                    
                    var chunk = root.chunks[this.world.state, fromChunkIndex];
                    // if chunks are equal - set area from source to dest
                    chunk.portals.list[this.world.state, fromPortalIndex].area = chunk.portals.list[this.world.state, toPortalIndex].area;
                    
                    // add local neighbour
                    chunk.portals.list[this.world.state, fromPortalIndex].localNeighbours.Add(new Portal.Connection() {
                        length = item.length,
                        portalInfo = new PortalInfo() {
                            chunkIndex = toChunkIndex,
                            portalIndex = toPortalIndex,
                        },
                    });
                    chunk.portals.list[this.world.state, toPortalIndex].localNeighbours.Add(new Portal.Connection() {
                        length = item.length,
                        portalInfo = new PortalInfo() {
                            chunkIndex = fromChunkIndex,
                            portalIndex = fromPortalIndex,
                        },
                    });

                } else {
                    
                    var fromChunk = root.chunks[this.world.state, fromChunkIndex];
                    // add remote neighbour
                    fromChunk.portals.list[this.world.state, fromPortalIndex].remoteNeighbours.Add(new Portal.Connection() {
                        length = item.length,
                        portalInfo = new PortalInfo() {
                            chunkIndex = toChunkIndex,
                            portalIndex = toPortalIndex,
                        },
                    });
                    
                }

            }
            this.graph.Set(root);

        }

    }

    [BURST(CompileSynchronously = true)]
    public unsafe struct PathDirectionsJob : IJob {

        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeReference<byte> needToRepath;
        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeList<uint> chunks;
        public Path path;
        public World world;
        public Filter filter;
        public Ent graph;
        
        public void Execute() {

            if (this.needToRepath.Value == 0) return;

            var marker = new Unity.Profiling.ProfilerMarker("Calculate Path Directions");
            marker.Begin();
            var root = this.graph.Read<RootGraphComponent>();
            for (uint index = 0u; index < this.chunks.Length; ++index) {

                var chunkIndex = this.chunks[(int)index];
                var gridChunk = this.path.chunks[this.world.state, chunkIndex];

                var targetChunkIndex = Graph.GetChunkIndex(in root, this.path.to, true);
                var targetChunk = root.chunks[this.world.state, targetChunkIndex];
                var targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, this.path.to, false);

                for (uint n = 0u, size = root.chunkWidth * root.chunkHeight; n < size; ++n) {
                    var item = gridChunk.flowField[this.world.state, (int)n];
                    var minCost = item.bestCost;

                    var curTempNode = new Graph.TempNode() {
                        chunkIndex = chunkIndex,
                        nodeIndex = n,
                    };

                    var dir = Graph.TARGET_BYTE;
                    if (targetChunkIndex == curTempNode.chunkIndex &&
                        targetNodeIndex == curTempNode.nodeIndex) {
                        
                    } else if (item.hasLineOfSight == 0) {
                        for (uint j = 0; j < 8u; ++j) {

                            var neighbourTempNode = Graph.GetNeighbourIndex(in this.world, curTempNode, j, root.chunkWidth, root.chunkHeight, this.path.chunks, root.width, root.height);
                            if (neighbourTempNode.chunkIndex == uint.MaxValue) continue;

                            var localChunk = root.chunks[this.world.state, neighbourTempNode.chunkIndex];
                            var node = localChunk.nodes[this.world.state, neighbourTempNode.nodeIndex];
                            if (this.filter.IsValid(in node) == false) {
                                continue;
                            }

                            var localGridChunk = this.path.chunks[this.world.state, neighbourTempNode.chunkIndex];
                            var cost = localGridChunk.flowField[this.world.state, (int)neighbourTempNode.nodeIndex].bestCost;
                            if (cost <= minCost) {

                                dir = Graph.GetCommonNeighbourSumDir(in this.world, curTempNode, j, root.chunkWidth, root.chunkHeight, this.path.chunks, root.width, root.height);
                                minCost = cost;

                            }

                        }
                    } else {
                        dir = Graph.LOS_BYTE;
                    }

                    gridChunk.flowField[this.world.state, n].direction = dir;
                }

            }
            marker.End();

        }

    }
    
    [BURST(CompileSynchronously = true)]
    public unsafe struct PathJob : IJob {

        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeReference<byte> needToRepath;
        public World world;
        public Ent graph;
        public Filter filter;
        public Path path;

        public void Execute() {

            if (this.needToRepath.Value == 0) return;

            var root = this.graph.Read<RootGraphComponent>();
            
            var to = this.path.to;
            var highLevelPath = new Unity.Profiling.ProfilerMarker("HierarchyPath");
            highLevelPath.Begin();
            var list = this.path.from.As(in this.world.state.ptr->allocator);
            var nodesCount = 0;
            var hierarchyPathList = new Unity.Collections.LowLevel.Unsafe.UnsafeList<PathInfo>((int)list.Count, Unity.Collections.Allocator.Temp);
            for (uint i = 0; i < list.Count; ++i) {
                var hierarchyPath = Graph.HierarchyPath(this.world.state, in this.graph, list[this.world.state, i], to, this.filter, root.nodeSize);
                if (hierarchyPath.pathState == PathState.Success) {
                    hierarchyPathList.Add(hierarchyPath);
                    nodesCount += hierarchyPath.nodes.Length;
                }
            }

            var chunks = new Unity.Collections.NativeList<uint>(nodesCount, Unity.Collections.Allocator.Temp);

            highLevelPath.End();
            foreach (var hierarchyPath in hierarchyPathList) {
                // build ff

                // build ff for each chunk from the end to the beginning
                var collectChunksMarker = new Unity.Profiling.ProfilerMarker("Collect Chunks");
                collectChunksMarker.Begin();
                // 1. collect chunks
                var nodes = hierarchyPath.nodes;
                var chunksToUpdate = new Unity.Collections.NativeList<PortalInfo>(nodes.Length, Unity.Collections.Allocator.Temp);
                {
                    var chunksVisited = new TempBitArray(root.chunks.Length, allocator: Unity.Collections.Allocator.Temp);
                    for (int k = 0; k < nodes.Length; ++k) {

                        var portalInfo = nodes[k];
                        if (chunksVisited.IsSet((int)portalInfo.chunkIndex) == true) continue;
                        chunksVisited.Set((int)portalInfo.chunkIndex, true);

                        var data = this.path.chunks[this.world.state, portalInfo.chunkIndex];
                        if (data.flowField.IsCreated == false) {

                            ref var chunk = ref root.chunks[this.world.state, portalInfo.chunkIndex];
                            /*if (k < nodes.Length - 1) {
                                var nextPortal = nodes[k + 1];
                                if (nextPortal.chunkIndex == portalInfo.chunkIndex) {
                                    if (chunk.cache.TryGetCache(in this.world.state.ptr->allocator, portalInfo, nextPortal, out var cacheChunk) == true) {
                                        //UnityEngine.Debug.Log("USE CACHE: " + portalInfo.chunkIndex);
                                        // use cache
                                        data.index = portalInfo.chunkIndex;
                                        data.flowField.CopyFrom(ref this.world.state.ptr->allocator, in cacheChunk.flowField);
                                        this.path.chunks[this.world.state, portalInfo.chunkIndex] = data;
                                        continue;
                                    }
                                }
                            }*/

                            var createMarker = new Unity.Profiling.ProfilerMarker("Create");
                            createMarker.Begin();
                            data.index = portalInfo.chunkIndex;
                            data.flowField = new MemArray<Path.Chunk.Item>(ref this.world.state.ptr->allocator, chunk.nodes.Length);
                            chunksToUpdate.Add(portalInfo);
                            createMarker.End();
                            var setDefaultMarker = new Unity.Profiling.ProfilerMarker("Set Default");
                            setDefaultMarker.Begin();
                            for (int i = 0; i < chunk.nodes.Length; ++i) {
                                data.flowField[this.world.state, i].bestCost = Graph.UNWALKABLE_COST;
                            }
                            setDefaultMarker.End();
                        }

                        this.path.chunks[this.world.state, portalInfo.chunkIndex] = data;

                    }
                }
                collectChunksMarker.End();

                // 2. update flow field for each chunk
                if (chunksToUpdate.Length > 0) {
                    var chunksVisited = new TempBitArray(root.chunks.Length, allocator: Unity.Collections.Allocator.Temp);
                    var costFieldMarker = new Unity.Profiling.ProfilerMarker("Create cost field");
                    costFieldMarker.Begin();
                    { // Creating cost field

                        float3 targetNodePosition;
                        uint targetChunkIndex;
                        uint targetNodeIndex;
                        {
                            // get target chunk
                            targetChunkIndex = Graph.GetChunkIndex(in root, to, true);
                            var targetChunk = root.chunks[this.world.state, targetChunkIndex];
                            targetNodeIndex = Graph.GetNodeIndex(in root, in targetChunk, to, true);
                            targetNodePosition = Graph.GetPosition(in root, in targetChunk, targetNodeIndex);
                            this.path.chunks[this.world.state, targetChunkIndex].flowField[this.world.state, targetNodeIndex].hasLineOfSight = 1;
                        }

                        for (int i = 0; i < chunksToUpdate.Length; ++i) {
                            var chunkPortalInfo = chunksToUpdate[i];
                            var chunk = root.chunks[this.world.state, chunkPortalInfo.chunkIndex];

                            var queue = new NativeQueue<Graph.TempNode>(4 * 4, Unity.Collections.Allocator.Temp);
                            var nodeIndex = Graph.GetNodeIndex(in root, in chunk, to, true);
                            if (nodeIndex != uint.MaxValue) {

                                var curTempNode = new Graph.TempNode() {
                                    nodeIndex = nodeIndex,
                                    chunkIndex = chunkPortalInfo.chunkIndex,
                                };
                                var targetSet = true;
                                var gridChunk = this.path.chunks[this.world.state, chunkPortalInfo.chunkIndex];
                                if (chunkPortalInfo.chunkIndex == targetChunkIndex) {
                                    // reset cost for node if it is a target chunk
                                    gridChunk.flowField[this.world.state, nodeIndex].bestCost = 0f;
                                    if (chunk.nodes[this.world.state, nodeIndex].walkable == false) {
                                        gridChunk.flowField[this.world.state, nodeIndex].bestCost = Graph.UNWALKABLE_COST;
                                        targetSet = false;
                                    }
                                    queue.Enqueue(curTempNode);
                                } else {
                                    {
                                        // looking for temp node with current chunk on the nodes path
                                        for (int n = 0; n < nodes.Length; ++n) {
                                            var node = nodes[n];
                                            if (node.chunkIndex == chunkPortalInfo.chunkIndex) {
                                                var portal = chunk.portals.list[this.world.state, node.portalIndex];
                                                curTempNode.nodeIndex = portal.rangeStartNodeIndex;
                                                for (uint r = 0u; r < portal.remoteNeighbours.Count; ++r) {
                                                    var remote = portal.remoteNeighbours[this.world.state, r];
                                                    var tempNode = new Graph.TempNode() {
                                                        chunkIndex = remote.portalInfo.chunkIndex,
                                                        nodeIndex = root.chunks[this.world.state, remote.portalInfo.chunkIndex].portals
                                                                        .list[this.world.state, remote.portalInfo.portalIndex].rangeStartNodeIndex,
                                                    };
                                                    queue.Enqueue(tempNode);
                                                }

                                                break;
                                            }
                                        }
                                    }
                                    for (int p = 0; p < nodes.Length; ++p) {
                                        var srcPortalInfo = nodes[p];
                                        //var toPortalNode = root.portalConnections.nodes[this.world.state, portalIndex];
                                        if (p < nodes.Length - 1) {
                                            var nextPortalInfo = nodes[p + 1];
                                            /*{
                                                var p11 = root.chunks[this.world.state, nextPortalInfo.chunkIndex].portals
                                                              .list[this.world.state, nextPortalInfo.portalIndex].position;
                                                var p21 = p11 + new float3(0f, 20f, 0f);
                                                UnityEngine.Debug.DrawLine(p11, p21, UnityEngine.Color.red);
                                            }
                                            {
                                                var p11 = root.chunks[this.world.state, srcPortalInfo.chunkIndex].portals
                                                              .list[this.world.state, srcPortalInfo.portalIndex].position;
                                                var p21 = p11 + new float3(0f, 20f, 0f);
                                                UnityEngine.Debug.DrawLine(p11, p21, UnityEngine.Color.cyan);
                                            }*/
                                            if (nextPortalInfo.chunkIndex != targetChunkIndex) {
                                                // reset cost for all nodes in this portal
                                                var portalChunk = root.chunks[this.world.state, srcPortalInfo.chunkIndex];
                                                var portalInfo = portalChunk.portals.list[this.world.state, srcPortalInfo.portalIndex];
                                                var idx = portalInfo.rangeStartNodeIndex;
                                                for (uint k = 0u; k <= portalInfo.size; ++k) {
                                                    queue.Enqueue(new Graph.TempNode() {
                                                        chunkIndex = srcPortalInfo.chunkIndex,
                                                        nodeIndex = idx,
                                                    });
                                                    /*{
                                                        var p1 = Graph.GetPosition(root.chunks[this.world.state, srcPortalInfo.chunkIndex], idx);
                                                        var p2 = p1 + new float3(0f, 5f, 0f);
                                                        UnityEngine.Debug.DrawLine(p1, p2, UnityEngine.Color.yellow);
                                                    }*/
                                                    idx = Graph.GetNeighbourIndex(idx, (int2)portalInfo.axis, root.chunkWidth, root.chunkHeight);
                                                }

                                                /*{
                                                    var p1 = root.chunks[this.world.state, neighbourTempNode.chunkIndex].portals.list[this.world.state, portalInfo.portalInfo.portalIndex].position;
                                                    var p2 = p1 + new float3(0f, 10f, 0f);
                                                    UnityEngine.Debug.DrawLine(p1, p2, UnityEngine.Color.yellow);
                                                }*/
                                            }
                                        }
                                    }
                                }

                                while (queue.Count > 0) {

                                    curTempNode = queue.Dequeue();
                                    if (chunksVisited.IsSet((int)curTempNode.chunkIndex) == false) {
                                        chunksVisited.Set((int)curTempNode.chunkIndex, true);
                                        chunks.Add(curTempNode.chunkIndex);
                                    }

                                    ref var item = ref this.path.chunks[this.world.state, curTempNode.chunkIndex];
                                    var rootCost = item.flowField[this.world.state, curTempNode.nodeIndex].bestCost;
                                    if (targetSet == false) {
                                        chunk = root.chunks[this.world.state, curTempNode.chunkIndex];
                                        var node = chunk.nodes[this.world.state, curTempNode.nodeIndex];
                                        if (node.walkable == true) {
                                            rootCost = item.flowField[this.world.state, curTempNode.nodeIndex].bestCost = 0f;
                                            targetSet = true;
                                        }
                                    }

                                    if (this.CalculateLOS(in root, ref item, in curTempNode, new Graph.TempNode() { chunkIndex = targetChunkIndex, nodeIndex = targetNodeIndex, }) == 1) {
                                        item.hasLineOfSight = 1;
                                    }
                                    
                                    for (uint j = 0; j < 4u; ++j) {

                                        var localDir = Graph.GetCoordDirection(j);
                                        var neighbourTempNode = Graph.GetNeighbourIndex(in this.world, curTempNode, localDir, root.chunkWidth, root.chunkHeight, this.path.chunks,
                                                                                        root.width, root.height);
                                        if (neighbourTempNode.chunkIndex == uint.MaxValue) continue;

                                        chunk = root.chunks[this.world.state, neighbourTempNode.chunkIndex];
                                        gridChunk = this.path.chunks[this.world.state, neighbourTempNode.chunkIndex];
                                        var neighbor = chunk.nodes[this.world.state, neighbourTempNode.nodeIndex];
                                        if (this.filter.IsValid(in neighbor) == false) {
                                            continue;
                                        }
                                        
                                        var endNodeCost = neighbor.cost + rootCost + 0.01f * math.lengthsq(targetNodePosition - Graph.GetPosition(in root, in chunk, neighbourTempNode.nodeIndex));
                                        ref var ffItem = ref gridChunk.flowField[this.world.state, neighbourTempNode.nodeIndex];
                                        if (endNodeCost < ffItem.bestCost) {
                                            ffItem.bestCost = endNodeCost;
                                            queue.Enqueue(neighbourTempNode);
                                        }
                                    }

                                }
                            }

                            if (chunkPortalInfo.chunkIndex != targetChunkIndex) {
                                // Update cache for this chunk
                                for (int p = 0; p < nodes.Length; ++p) {
                                    var srcPortalInfo = nodes[p];
                                    var pathChunk = this.path.chunks[this.world.state, srcPortalInfo.chunkIndex];
                                    if (pathChunk.hasLineOfSight == 1) continue;
                                    ref var chunkData = ref root.chunks[this.world.state, srcPortalInfo.chunkIndex];
                                    if (p < nodes.Length - 1) {
                                        var nextPortalInfo = nodes[p + 1];
                                        if (srcPortalInfo.chunkIndex == nextPortalInfo.chunkIndex) {
                                            // need to add local portals only
                                            //UnityEngine.Debug.Log("UPDATE CACHE: " + srcPortalInfo.chunkIndex);
                                            chunkData.cache.UpdateCache(ref this.world.state.ptr->allocator, srcPortalInfo, nextPortalInfo,
                                                                        in this.path.chunks[this.world.state, srcPortalInfo.chunkIndex]);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    costFieldMarker.End();
                }
            }

            new PathDirectionsJob() {
                chunks = chunks,
                filter = this.filter,
                graph = this.graph,
                needToRepath = this.needToRepath,
                path = this.path,
                world = this.world,
            }.Execute();

        }
        
        [INLINE(256)]
        private byte CalculateLOS(in RootGraphComponent root, ref Path.Chunk chunk, in Graph.TempNode node, in Graph.TempNode targetNode) {

            [INLINE(256)]
            static byte GetState(safe_ptr<State> state, in Path path, in Graph.TempNode node) {
                var chunk = path.chunks[state, node.chunkIndex];
                if (chunk.flowField.IsCreated == false) return 0;
                return chunk.flowField[state, node.nodeIndex].hasLineOfSight;
            }

            [INLINE(256)]
            static bool IsWalkable(safe_ptr<State> state, int2 at, in RootGraphComponent root) {
                var p = Graph.GetCoordInfo(at.x, at.y, root.chunkWidth, root.chunkHeight, root.width, root.height);
                if (p.IsValid() == false) return false;
                return root.chunks[state, p.chunkIndex].nodes[state, p.nodeIndex].walkable;
            }
            
            var at = Graph.GetGlobalCoord(node.chunkIndex, node.nodeIndex, root.chunkWidth, root.chunkHeight, root.width);
            
            // do not calculate los if any neighbour is unwalkable
            if (IsWalkable(this.world.state, new int2(at.x + 1, at.y), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x + 1, at.y - 1), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x, at.y - 1), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x - 1, at.y - 1), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x - 1, at.y), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x - 1, at.y + 1), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x, at.y + 1), in root) == false) return 0;
            if (IsWalkable(this.world.state, new int2(at.x + 1, at.y + 1), in root) == false) return 0;
            
            var pathEnd = Graph.GetGlobalCoord(targetNode.chunkIndex, targetNode.nodeIndex, root.chunkWidth, root.chunkHeight, root.width);

            var xDif = (int)pathEnd.x - (int)at.x;
            var yDif = (int)pathEnd.y - (int)at.y;

            var xDifAbs = math.abs(xDif);
            var yDifAbs = math.abs(yDif);

            var xDifOne = (int)math.sign(xDif);
            var yDifOne = (int)math.sign(yDif);
            
            var xOffset = xDifOne;
            var yOffset = yDifOne;

            if (xDifAbs * 2 <= yDifAbs) {
                xOffset = 0;
            }
            
            if (yDifAbs * 2 <= xDifAbs) {
                yOffset = 0;
            }
            
            byte hasLos = 0;
            var info = Graph.GetCoordInfo(at.x + xOffset, at.y + yOffset, root.chunkWidth, root.chunkHeight, root.width, root.height);
            if (info.IsValid() == true) {
                hasLos = GetState(this.world.state, in this.path, in info);
            }
            
            var currentNodeInfo = Graph.GetCoordInfo(at.x, at.y, root.chunkWidth, root.chunkHeight, root.width, root.height);
            if (currentNodeInfo.IsValid() == false || root.chunks[this.world.state, currentNodeInfo.chunkIndex].nodes[this.world.state, currentNodeInfo.nodeIndex].cost > 1) {
                hasLos = 0;
            }

            chunk.flowField[this.world.state, node.nodeIndex].hasLineOfSight = hasLos;
            return hasLos;

        }


    }
    
}