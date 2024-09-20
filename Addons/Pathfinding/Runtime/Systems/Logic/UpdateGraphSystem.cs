using ME.BECS.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Jobs;
    using ME.BECS.Transforms;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule update a pathfinding graph.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct UpdateGraphSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct ResetPathJob : IJobParallelForComponents<TargetPathComponent> {

            public Unity.Collections.NativeArray<ulong> dirtyChunks;
            public World world;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TargetPathComponent path) {
                
                for (uint i = 0; i < this.dirtyChunks.Length; ++i) {
                    if (this.dirtyChunks[(int)i] != this.world.state->tick) continue;
                    ref var chunk = ref path.path.chunks[this.world.state, i];
                    ref var flowField = ref chunk.flowField;
                    if (flowField.IsCreated == true) flowField.Dispose(ref this.world.state->allocator);
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdateGraphMaskJob : IJobParallelForComponents<GraphMaskComponent> {

            public BuildGraphSystem graphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref GraphMaskComponent obstacle) {

                ent.Remove<IsGraphMaskDirtyComponent>();
                
                var tr = ent.GetAspect<TransformAspect>();
                var position = tr.position;
                var rotation = tr.rotation;
                
                // update graphs and set dirty chunks
                var world = ent.World;
                var dirtyTick = world.state->tick;
                for (uint g = 0u; g < this.graphSystem.graphs.Length; ++g) {

                    var graph = this.graphSystem.graphs[(int)g];
                    var root = graph.Read<RootGraphComponent>();
                    var agentRadius = root.agentRadius;
                    if (obstacle.ignoreGraphRadius == true) {
                        agentRadius = 0f;
                    }
                    var obstacleSizeOffset = new float2(agentRadius * 2f);
                    //var obstacleSizeOffsetHeight = new float2(-agentRadius);
                    var pos = new float3(position.x + obstacle.offset.x, 0f, position.z + obstacle.offset.y);
                    var obstacleBounds = Graph.GetObstacleRect(pos, in rotation, obstacle.size + obstacleSizeOffset);
                    var size3d = new float3(obstacle.size.x + obstacleSizeOffset.x, 0f, obstacle.size.y + obstacleSizeOffset.y);
                    //var heightSize3d = new float3(obstacle.size.x + obstacleSizeOffsetHeight.x, 0f, obstacle.size.y + obstacleSizeOffsetHeight.y);
                    var obstacleSize = GraphUtils.SnapSize(obstacle.size);
                    obstacleSize += new float2(obstacleSizeOffset.x, obstacleSizeOffset.y);
                    var posMin = pos - size3d * 0.5f;
                    var posMax = pos + size3d * 0.5f;
                    //var posMinHeight = pos - heightSize3d * 0.5f;
                    //var posMaxHeight = pos + heightSize3d * 0.5f;

                    // get chunks list by bounds
                    var chunks = Graph.GetChunksByBounds(in root, in obstacleBounds);
                    foreach (var chunkIndex in chunks) {

                        var chunkComponent = root.chunks[chunkIndex];
                        root.changedChunks[chunkIndex] = dirtyTick;
                        UnityEngine.Rect chunkBounds;
                        {
                            var min = Graph.GetPosition(in root, in chunkComponent, 0u);
                            var max = Graph.GetPosition(in root, in chunkComponent, chunkComponent.nodes.Length - 1u);
                            chunkBounds = UnityEngine.Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                        }

                        {
                            
                            // intersection check
                            if (chunkBounds.Overlaps(obstacleBounds) == false) continue;

                            for (float x = posMin.x; x <= posMax.x; x += root.nodeSize * 0.45f) {
                                for (float y = posMin.z; y <= posMax.z; y += root.nodeSize * 0.45f) {
                                    var worldPos = new float3(x, 0f, y);
                                    var graphPos = math.mul(rotation, worldPos - position) + position;
                                    var nodeIndex = Graph.GetNodeIndex(in root, in chunkComponent, graphPos, clamp: false);
                                    if (nodeIndex == uint.MaxValue) continue;
                                    ref var node = ref chunkComponent.nodes[world.state, nodeIndex];
                                    if (obstacle.cost > node.cost) {
                                        //if (x >= posMinHeight.x && x <= posMaxHeight.x &&
                                        //    y >= posMinHeight.z && y <= posMaxHeight.z)
                                        {
                                            //var localPos = math.mul(math.inverse(rotation), graphPos - position - obstacle.offset.x0y());
                                            var localPos = worldPos - position;
                                            localPos.x += obstacleSize.x * 0.5f;
                                            localPos.z += obstacleSize.y * 0.5f;
                                            var newHeight = GraphUtils.GetObstacleHeight(in localPos, in obstacle.heights, in obstacleSize, obstacle.heightsSizeX);
                                            if (newHeight >= 0f && JobUtils.SetIfGreater(ref node.height, newHeight + position.y) == true) {
                                                obstacle.nodesLock.Lock();
                                                obstacle.nodes.Add(new GraphNodeMemory(in graph, new Graph.TempNode() { chunkIndex = chunkIndex, nodeIndex = nodeIndex }, in node));
                                                obstacle.nodesLock.Unlock();
                                                JobUtils.SetIfGreater(ref node.cost, obstacle.cost);
                                            }
                                        }
                                        node.obstacleChannel = obstacle.obstacleChannel;
                                    }
                                }
                            }
                        }
                    }
                }
                
            }

        }

        public unsafe void OnUpdate(ref SystemContext context) {

            var graphSystem = context.world.GetSystem<BuildGraphSystem>();
            var graphMaskUpdate = API.Query(in context, context.dependsOn).With<GraphMaskComponent>().With<IsGraphMaskDirtyComponent>().Schedule<UpdateGraphMaskJob, GraphMaskComponent>(new UpdateGraphMaskJob() {
                graphSystem = graphSystem,
            });
            
            var dependencies = new NativeArray<Unity.Jobs.JobHandle>(graphSystem.graphs.Length, Allocator.Temp);
            for (var i = 0; i < graphSystem.graphs.Length; ++i) {
                var graphEnt = graphSystem.graphs[i];
                var changedChunks = graphEnt.Read<RootGraphComponent>().changedChunks;
                var tempDirty = new NativeArray<ulong>((int)changedChunks.Length, Constants.ALLOCATOR_TEMPJOB);
                _memcpy(changedChunks.GetUnsafePtr(), tempDirty.GetUnsafePtr(), TSize<ulong>.size * changedChunks.Length);
                var dependsOn = Graph.UpdateObstacles(in context.world, in graphEnt, in tempDirty, graphMaskUpdate);
                // reset all changed chunks in all existing paths
                dependsOn = API.Query(in context.world, dependsOn).Schedule<ResetPathJob, TargetPathComponent>(new ResetPathJob() {
                    world = context.world,
                    dirtyChunks = tempDirty,
                });
                dependsOn = tempDirty.Dispose(dependsOn);
                dependencies[i] = dependsOn;
            }

            var resultDep = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            context.SetDependency(resultDep);

        }

    }

}