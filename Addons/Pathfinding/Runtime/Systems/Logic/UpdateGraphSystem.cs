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

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using Unity.Jobs;
    using ME.BECS.Transforms;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule update a pathfinding graph.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct UpdateGraphSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct ResetPathJob : IJobForComponents<TargetPathComponent> {

            public Ent graph;
            public Unity.Collections.NativeArray<ulong> dirtyChunks;
            public World world;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TargetPathComponent path) {

                if (path.path.graph != this.graph) return;
                
                for (uint i = 0; i < this.dirtyChunks.Length; ++i) {
                    if (this.dirtyChunks[(int)i] != this.world.CurrentTick) continue;
                    ref var chunk = ref path.path.chunks[this.world.state, i];
                    ref var flowField = ref chunk.flowField;
                    if (flowField.IsCreated == true) {
                        flowField.Dispose(ref this.world.state.ptr->allocator);
                    }
                }

            }

        }

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdateGraphMaskJob : IJobForComponents<GraphMaskComponent, GraphMaskRuntimeComponent> {

            public BuildGraphSystem graphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref GraphMaskComponent obstacle, ref GraphMaskRuntimeComponent obstacleRuntime) {

                ent.Remove<IsGraphMaskDirtyComponent>();
                
                var tr = ent.GetAspect<TransformAspect>();
                var position = tr.position;
                var rotation = tr.rotation;
                
                // update graphs and set dirty chunks
                var world = ent.World;
                var dirtyTick = world.CurrentTick;
                for (uint g = 0u; g < this.graphSystem.graphs.Length; ++g) {

                    if ((obstacle.graphMask & (1 << (int)g)) == 0) continue;
                    
                    var graph = this.graphSystem.graphs[ent.World.state, g];
                    var root = graph.Read<RootGraphComponent>();
                    var agentRadius = root.agentRadius;
                    if (obstacle.ignoreGraphRadius == 1) {
                        agentRadius = 0f;
                    }
                    var obstacleSizeOffset = new float2(agentRadius * 2f);
                    //var obstacleSizeOffsetHeight = new float2(-agentRadius);
                    var pos = new float3(position.x + obstacle.offset.x, 0f, position.z + obstacle.offset.y);
                    var obstacleBounds = Graph.GetObstacleRect(pos, in rotation, obstacle.size + obstacleSizeOffset);
                    var size3d = new float3(obstacle.size.x + obstacleSizeOffset.x, 0f, obstacle.size.y + obstacleSizeOffset.y);
                    //var heightSize3d = new float3(obstacle.size.x + obstacleSizeOffsetHeight.x, 0f, obstacle.size.y + obstacleSizeOffsetHeight.y);
                    var obstacleSize = GraphUtils.SnapSize(obstacle.size) * 2f;
                    obstacleSize += new float2(obstacleSizeOffset.x, obstacleSizeOffset.y);
                    var posMin = pos - size3d * 0.5f;
                    var posMax = pos + size3d * 0.5f;
                    //var posMinHeight = pos - heightSize3d * 0.5f;
                    //var posMaxHeight = pos + heightSize3d * 0.5f;

                    // get chunks list by bounds
                    var chunks = Graph.GetChunksByBounds(in root, in obstacleBounds, Constants.ALLOCATOR_TEMP);
                    foreach (var chunkIndex in chunks) {

                        if (chunkIndex == uint.MaxValue) continue;
                        var chunkComponent = root.chunks[chunkIndex];
                        root.changedChunks[chunkIndex] = dirtyTick;
                        Rect chunkBounds;
                        {
                            var min = Graph.GetPosition(in root, in chunkComponent, 0u);
                            var max = Graph.GetPosition(in root, in chunkComponent, chunkComponent.nodes.Length - 1u);
                            chunkBounds = Rect.MinMaxRect(min.x, min.z, max.x, max.z);
                        }

                        // intersection check
                        if (chunkBounds.Overlaps(obstacleBounds) == false) continue;

                        {
                            for (tfloat x = posMin.x; x <= posMax.x; x += root.nodeSize * 0.45f) {
                                for (tfloat y = posMin.z; y <= posMax.z; y += root.nodeSize * 0.45f) {
                                    var worldPos = new float3(x, 0f, y);
                                    var graphPos = math.mul(rotation, worldPos - position) + position;
                                    var nodeIndex = Graph.GetNodeIndex(in root, in chunkComponent, graphPos, clamp: false);
                                    if (nodeIndex == uint.MaxValue) continue;
                                    ref var node = ref chunkComponent.nodes[world.state, nodeIndex];
                                    if (obstacle.cost > node.cost) {
                                        {
                                            var localPos = worldPos - position;
                                            localPos.x += obstacleSize.x * 0.5f;
                                            localPos.z += obstacleSize.y * 0.5f;
                                            var newHeight = GraphUtils.GetObstacleHeight(in localPos, in obstacleRuntime.heights, in obstacleSize, obstacle.heightsSizeX);
                                            var totalHeight = newHeight + position.y;
                                            if (newHeight >= 0f && totalHeight > node.height) {
                                                obstacleRuntime.nodesLock.Lock();
                                                obstacleRuntime.nodes.Add(new GraphNodeMemory(in graph, new Graph.TempNode() { chunkIndex = chunkIndex, nodeIndex = nodeIndex }, in node));
                                                obstacleRuntime.nodesLock.Unlock();
                                                JobUtils.SetIfGreater(ref node.cost, obstacle.cost);
                                                JobUtils.SetIfGreater(ref node.height, totalHeight);
                                            }
                                        }
                                        node.obstacleChannel = obstacle.obstacleChannel;
                                    }
                                }
                            }
                        }
                    }
                    chunks.Dispose();
                }
                
            }

        }
        
        public unsafe void OnUpdate(ref SystemContext context) {

            var graphSystem = context.world.GetSystem<BuildGraphSystem>();
            var graphMaskUpdate = context.Query().With<GraphMaskComponent>().With<IsGraphMaskDirtyComponent>().AsParallel().Schedule<UpdateGraphMaskJob, GraphMaskComponent, GraphMaskRuntimeComponent>(new UpdateGraphMaskJob() {
                graphSystem = graphSystem,
            });
            
            var dependencies = new NativeArray<Unity.Jobs.JobHandle>((int)graphSystem.graphs.Length, Constants.ALLOCATOR_TEMP);
            for (var i = 0; i < graphSystem.graphs.Length; ++i) {
                var graphEnt = graphSystem.graphs[context.world.state, i];
                var changedChunks = graphEnt.Read<RootGraphComponent>().changedChunks;
                var tempDirty = new NativeArray<ulong>((int)changedChunks.Length, Constants.ALLOCATOR_TEMPJOB);
                _memcpy(changedChunks.GetUnsafePtr(), (safe_ptr)tempDirty.GetUnsafePtr(), TSize<ulong>.size * changedChunks.Length);
                var dependsOn = Graph.UpdateObstacles(in context.world, in graphEnt, in tempDirty, graphMaskUpdate);
                // reset all changed chunks in all existing paths
                dependsOn = context.Query(dependsOn).AsUnsafe().AsParallel().Schedule<ResetPathJob, TargetPathComponent>(new ResetPathJob() {
                    graph = graphEnt,
                    world = context.world,
                    dirtyChunks = tempDirty,
                });
                dependsOn = tempDirty.Dispose(dependsOn);
                dependencies[i] = dependsOn;
            }

            var resultDep = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            dependencies.Dispose();
            context.SetDependency(resultDep);

        }

    }

}