using System.Linq;
using ME.BECS.Jobs;

namespace ME.BECS.Pathfinding {
    
    using ME.BECS.Transforms;
    using Unity.Jobs;

    public unsafe class TestGraph : UnityEngine.MonoBehaviour {

        public GraphProperties graphProperties;
        public ME.BECS.Units.AgentType agentProperties;
        public Filter filter;

        public bool drawGizmos;
        public bool buildGraph;
        public bool buildPath;
        public bool updateMove;
        
        [UnityEngine.HeaderAttribute("Path")]
        public UnityEngine.Transform to;
        public UnityEngine.Transform to0;
        public UnityEngine.Transform debug;

        public UnityEngine.Transform[] move;
        public UnityEngine.Vector3[] lastCorrectPosition;
        public float minMass = 1f;
        public float moveSpeed = 1f;

        [UnityEngine.HeaderAttribute("Obstacle")]
        public UnityEngine.Transform[] obstacles;
        public UnityEngine.Transform[] dynamicObstacles;

        public struct Item {

            public Unity.Mathematics.float3 position;
            public Unity.Mathematics.quaternion rotation;
            public Unity.Mathematics.float3 size;

        }

        [Unity.Burst.BurstCompileAttribute]
        public struct Job : Unity.Jobs.IJob {

            public Unity.Collections.NativeList<Item> list;

            public void Execute() {

                foreach (var item in this.list) {

                    var obstacle = Ent.New();
                    obstacle.Set<TransformAspect>();
                    var tr = obstacle.GetAspect<TransformAspect>();
                    tr.position = item.position;
                    tr.rotation = item.rotation;
                    obstacle.Set(new GraphMaskComponent() {
                        offset = Unity.Mathematics.float2.zero,
                        size = new Unity.Mathematics.float2(item.size.x, item.size.z),
                        cost = 255,
                    });

                }

            }

        }

        public float dObstacleTime;
        private float timer;
        private float timerWait;

        public struct PathInvalidateJob : IJob {

            public World world;
            public Unity.Collections.NativeArray<bool> invalidateChunks;
            public Path path;

            public void Execute() {
                
                for (uint i = 0; i < this.invalidateChunks.Length; ++i) {
                    if (this.invalidateChunks[(int)i] == false) continue;
                    ref var ffData = ref this.path.chunks[this.world.state, i].flowField;
                    if (ffData.isCreated == true) ffData.Dispose(ref this.world.state->allocator);
                }
                
            }

        }
        private Unity.Jobs.JobHandle AddDynamicObstacles(float dt, in World world, ref Path path, in Ent graphEnt, Unity.Jobs.JobHandle dependsOn) {

            if (this.timer <= this.dObstacleTime) {
                this.timer += dt;
            }

            if (this.timer >= this.dObstacleTime) {
                this.timerWait += dt;
                if (this.timerWait >= this.dObstacleTime) {
                    this.timerWait = 0f;
                    this.timer = 0f;
                }
                var list = new Unity.Collections.NativeList<Item>(Unity.Collections.Allocator.TempJob);
                foreach (var ob in this.dynamicObstacles) {
                    if (ob.gameObject.activeSelf == false) continue;
                    var bounds = ob.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
                    var size = bounds.size;
                    list.Add(new Item() {
                        position = ob.position + bounds.center,
                        rotation = ob.rotation,
                        size = size,
                    });
                }

                dependsOn = new Job() {
                    list = list,
                }.Schedule(dependsOn);
                var changedChunks = new Unity.Collections.NativeArray<bool>((int)graphEnt.Read<RootGraphComponent>().chunks.Length, Unity.Collections.Allocator.TempJob);
                dependsOn = Graph.UpdateObstacles(in world, in graphEnt, changedChunks, dependsOn);
                dependsOn = new PathInvalidateJob() {
                    world = world,
                    invalidateChunks = changedChunks,
                    path = path,
                }.Schedule(dependsOn);
                /*dependsOn = API.Query(in world, dependsOn).With<TargetComponent>().ScheduleParallelFor<UpdateGraphSystem.UpdatePathJob, TargetComponent>(new UpdateGraphSystem.UpdatePathJob() {
                    world = world,
                    invalidateChunks = changedChunks,
                });*/
                dependsOn = changedChunks.Dispose(dependsOn);
                dependsOn = list.Dispose(dependsOn);
                dependsOn = Batches.Apply(dependsOn, world.state);
            }

            return dependsOn;

        }
        
        public void OnDrawGizmos() {

            Unity.Jobs.LowLevel.Unsafe.JobsUtility.ResetJobWorkerCount();
            var dt = 0.033f;

            var obs = UnityEngine.GameObject.FindGameObjectsWithTag("unit");
            this.obstacles = UnityEngine.GameObject.FindGameObjectsWithTag("obstacle").Select(x => x.transform).ToArray();
            this.move = obs.Select(x => x.transform).ToArray();
            if (this.lastCorrectPosition == null || this.lastCorrectPosition.Length != this.move.Length) System.Array.Resize(ref this.lastCorrectPosition, this.move.Length);

            var world = World.Create();
            foreach (var ob in this.obstacles) {
                if (ob.gameObject.activeSelf == false) continue;
                var bounds = ob.GetComponentInChildren<UnityEngine.MeshFilter>().sharedMesh.bounds;
                var size = bounds.size;
                var obstacle = Ent.New();
                obstacle.Set<TransformAspect>();
                var tr = obstacle.GetAspect<TransformAspect>();
                tr.position = ob.position + bounds.center;
                tr.rotation = ob.rotation;
                obstacle.Set(new GraphMaskComponent() {
                    offset = Unity.Mathematics.float2.zero,//new Unity.Mathematics.float2(bounds.center.x, bounds.center.z),
                    size = new Unity.Mathematics.float2(size.x, size.z),
                    cost = 255,
                });
            }
            Batches.Apply(world.state);

            var markerGraph = new Unity.Profiling.ProfilerMarker("Graph Build");
            markerGraph.Begin();
            Ent graphEnt = default;
            JobHandle jobHandle = default;
            if (this.buildGraph == true) {
                var activeTerrain = UnityEngine.Terrain.activeTerrain;
                var terrain = activeTerrain.terrainData;
                var heights = Heights.Create(activeTerrain.GetPosition(), terrain, Unity.Collections.Allocator.TempJob);
                jobHandle = Graph.Build(in world, in heights, out graphEnt, this.graphProperties, this.agentProperties);
                jobHandle = heights.Dispose(jobHandle);
            }

            // clamp target to graph
            this.to.position = Graph.ClampPosition(in graphEnt, this.to.position);
            
            markerGraph.End();
            UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
            if (this.to != null && this.buildPath == true) {
                var markerPath = new Unity.Profiling.ProfilerMarker("Graph Path");
                markerPath.Begin();
                Graph.MakePath(in world, out var path, in graphEnt, this.to.position, this.filter);
                markerPath.End();
                
                jobHandle.Complete();
                
                var root = graphEnt.Read<RootGraphComponent>();
                var moveChunks = new MemArrayAuto<bool>(graphEnt, root.width * root.height);
                for (var i = 0; i < this.move.Length; ++i) {
                    var move = this.move[i];
                    var pos = move.position;
                    var chunkIndex = Graph.GetChunkIndex(in root, pos, false);
                    if (chunkIndex == uint.MaxValue) continue;
                    moveChunks[(int)chunkIndex] = true;
                }

                /*for (int i = 0; i < moveChunks.Length; ++i) {
                    if (moveChunks[i] == true) {
                        var chunk = root.chunks[world.state, i];
                        var from = chunk.center + new Unity.Mathematics.float3(chunk.width * chunk.nodeSize * 0.5f, 0f, chunk.height * chunk.nodeSize * 0.5f);
                        UnityEngine.Debug.DrawLine(from, from + (Unity.Mathematics.float3)UnityEngine.Vector3.up * 10f);
                    }
                }*/

                Graph.SetTarget(ref path, this.to0.position, this.filter);
                var handle = Graph.UpdatePath(in world, moveChunks, ref path, jobHandle);
                handle = this.AddDynamicObstacles(dt, in world, ref path, in graphEnt, handle);
                handle = Graph.UpdatePath(in world, moveChunks, ref path, handle);

                handle = moveChunks.Dispose(handle);
                
                if (this.move != null && this.updateMove == true) {
                    
                    for (var i = 0; i < this.move.Length; ++i) {
                        var move = this.move[i];
                        var markerMove = new Unity.Profiling.ProfilerMarker("Graph Move");
                        markerMove.Begin();
                        handle.Complete();
                        var dir = Graph.GetDirection(in world, move.position, in path, out var complete);
                        var rb = move.GetComponent<UnityEngine.Rigidbody>();
                        var newPos = (Unity.Mathematics.float3)rb.position + dir * this.moveSpeed;
                        if (Unity.Mathematics.math.all(dir == Unity.Mathematics.float3.zero)) {
                            // next pos is not walkable - move to last correct position
                            rb.MovePosition(this.lastCorrectPosition[i]);
                            rb.mass += 1f * dt;
                        } else {
                            rb.MovePosition(newPos);
                            rb.mass -= 1f * dt * 0.5f;
                            if (rb.mass <= this.minMass) rb.mass = this.minMass;
                            this.lastCorrectPosition[i] = newPos;
                        }

                        if (Unity.Mathematics.math.lengthsq(dir) > 0.01f) rb.MoveRotation(UnityEngine.Quaternion.LookRotation(dir, UnityEngine.Vector3.up));
                        /*rb.velocity = UnityEngine.Vector3.Lerp(rb.velocity, rb.velocity + (UnityEngine.Vector3)dir * this.moveSpeed,
                                                               dt);*/
                        /*rb.MovePosition(UnityEngine.Vector3.Lerp(rb.position, rb.position + (UnityEngine.Vector3)dir * this.moveSpeed,
                                                                 dt));*/
                        markerMove.End();
                    }
                }
                UnityEngine.Physics.Simulate(dt);
                
                handle.Complete();
                if (this.drawGizmos == true) {
                    Graph.DrawGizmos(graphEnt, default);
                    Graph.DrawGizmos(path, default);
                }
                path.Dispose(in world);
            } else {
                jobHandle.Complete();
                if (this.drawGizmos == true) {
                    Graph.DrawGizmos(graphEnt, default);
                }
            }
            
            if (graphEnt.IsAlive() == true) graphEnt.Destroy();
            world.Dispose();

        }

        public static void ForGizmo(UnityEngine.Vector3 pos, UnityEngine.Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
            if (direction == UnityEngine.Vector3.zero) return;
            UnityEngine.Gizmos.DrawRay(pos, direction);
       
            UnityEngine.Vector3 right = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0,180+arrowHeadAngle,0) * new UnityEngine.Vector3(0,0,1);
            UnityEngine.Vector3 left = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0,180-arrowHeadAngle,0) * new UnityEngine.Vector3(0,0,1);
            UnityEngine.Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            UnityEngine.Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }
 
        public static void ForGizmo(UnityEngine.Vector3 pos, UnityEngine.Vector3 direction, UnityEngine.Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f) {
            if (direction == UnityEngine.Vector3.zero) return;
            UnityEngine.Gizmos.color = color;
            UnityEngine.Gizmos.DrawRay(pos, direction);
       
            UnityEngine.Vector3 right = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0,180+arrowHeadAngle,0) * new UnityEngine.Vector3(0,0,1);
            UnityEngine.Vector3 left = UnityEngine.Quaternion.LookRotation(direction) * UnityEngine.Quaternion.Euler(0,180-arrowHeadAngle,0) * new UnityEngine.Vector3(0,0,1);
            UnityEngine.Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
            UnityEngine.Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
        }

        
    }

}