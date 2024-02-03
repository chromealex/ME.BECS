
namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;
    using Unity.Jobs;

    [BURST(CompileSynchronously = true)]
    public struct CreateSystem : IAwake, IUpdate {

        public float2 mapSize;
        public float resolution;
        internal Ent heights;

        [BURST(CompileSynchronously = true)]
        public struct CreateJob : IJobParallelForAspect<TeamAspect> {

            public uint2 fowSize;
            
            public void Execute(ref TeamAspect aspect) {

                var map = new FogOfWarComponent() {
                    nodes = new MemArrayAuto<int>(aspect.ent, this.fowSize.x * this.fowSize.y),
                };
                aspect.ent.Set(map);
                
            }

        }

        public void OnAwake(ref SystemContext context) {
            
            // for each player
            // create fog of war
            var fowSize = math.max(32u, (uint2)(this.mapSize * this.resolution));
            var heights = Ent.New();
            heights.Set(new FogOfWarStaticComponent() {
                worldSize = this.mapSize,
                size = fowSize,
                heights = new MemArrayAuto<int>(heights, fowSize.x * fowSize.y),
            });
            this.heights = heights;
            var dependsOn = context.Query().ScheduleParallelFor<CreateJob, TeamAspect>(new CreateJob() {
                fowSize = fowSize,
            });
            context.SetDependency(dependsOn);

        }

        [BURST(CompileSynchronously = true)]
        public struct CleanUpJob : IJobParallelForAspect<TeamAspect> {
            
            public void Execute(ref TeamAspect player) {
                
                var fow = player.ent.Read<FogOfWarComponent>();
                fow.nodes.Clear();

            }

        }

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdateHeightJob : IJobParallelFor {

            [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestrictionAttribute]
            [Unity.Collections.ReadOnlyAttribute]
            public Unity.Collections.NativeArray<bool> dirtyChunks;
            public uint2 fowSize;
            public BuildGraphSystem pathfinding;
            public World world;
            public RootGraphComponent graph;
            public Ent heights;

            public void Execute(int index) {
                
                if (this.dirtyChunks[index] == false) return;

                var fow = this.heights.Read<FogOfWarStaticComponent>();
                var chunk = this.graph.chunks[index];
                for (uint i = 0; i < chunk.nodes.Length; ++i) {
                    var nodeHeight = chunk.nodes[this.world.state, i].height;
                    var worldPos = Graph.GetPosition(this.graph, in chunk, i);
                    var height = math.max(nodeHeight, this.pathfinding.heights.GetHeight(worldPos));
                    var heightMap = FogOfWarUtils.WorldToFogMapValue(in fow, height);
                    var xy = FogOfWarUtils.WorldToFogMapPosition(in fow, in worldPos);
                    fow.heights[xy.y * this.fowSize.x + xy.x] = heightMap;
                }

            }

        }

        public void OnUpdate(ref SystemContext context) {

            // clean up data
            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var firstGraph = pathfinding.GetGraphByTypeId(0u).Read<RootGraphComponent>();
            
            var fowSize = math.max(32u, (uint2)(this.mapSize * this.resolution));
            var cleanUpHandle = context.Query().ScheduleParallelFor<CleanUpJob, TeamAspect>();
            var updateHeightHandle = new UpdateHeightJob() {
                dirtyChunks = pathfinding.changedChunks,
                fowSize = fowSize,
                pathfinding = pathfinding,
                world = context.world,
                graph = firstGraph,
                heights = this.heights,
            }.Schedule((int)firstGraph.chunks.Length, (int)JobUtils.GetScheduleBatchCount(firstGraph.chunks.Length), context.dependsOn);
            context.SetDependency(Unity.Jobs.JobHandle.CombineDependencies(cleanUpHandle, updateHeightHandle));

        }

        [INLINE(256)]
        public bool IsVisible(in PlayerAspect player, in Ent unit) {
            
            var fow = player.readTeam.Read<ME.BECS.FogOfWar.FogOfWarComponent>();
            var props = this.heights.Read<ME.BECS.FogOfWar.FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<ME.BECS.Transforms.TransformAspect>().GetWorldMatrixPosition());
            return ME.BECS.FogOfWar.FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y);

        }

    }

}