
namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;
    using Unity.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CreateSystem : IAwake, IUpdate {

        public float2 mapSize;
        public float resolution;
        internal Ent heights;

        [BURST(CompileSynchronously = true)]
        public struct CreateJob : IJobParallelForAspect<TeamAspect> {

            public uint2 fowSize;
            
            public void Execute(in JobInfo jobInfo, ref TeamAspect aspect) {

                var map = new FogOfWarComponent() {
                    nodes = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                    explored = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                };
                aspect.ent.Set(map);
                
            }

        }

        public void OnAwake(ref SystemContext context) {
            // for each player
            // create fog of war
            var fowSize = math.max(32u, (uint2)(this.mapSize * this.resolution));
            var heights = Ent.New(in context);
            heights.Set(new FogOfWarStaticComponent() {
                worldSize = this.mapSize,
                size = fowSize,
                heights = new MemArrayAuto<float>(heights, fowSize.x * fowSize.y),
            });
            this.heights = heights;
            var dependsOn = context.Query().Schedule<CreateJob, TeamAspect>(new CreateJob() {
                fowSize = fowSize,
            });
            
            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var firstGraph = pathfinding.GetGraphByTypeId(0u).Read<RootGraphComponent>();
            var updateHeightHandle = new UpdateHeightJob() {
                fowSize = fowSize,
                pathfinding = pathfinding,
                world = context.world,
                graph = firstGraph,
                heights = this.heights,
            }.Schedule((int)firstGraph.chunks.Length, (int)JobUtils.GetScheduleBatchCount(firstGraph.chunks.Length), dependsOn);
            context.SetDependency(updateHeightHandle);

        }

        [BURST(CompileSynchronously = true)]
        public struct CleanUpJob : IJobParallelForAspect<TeamAspect> {
            
            public void Execute(in JobInfo jobInfo, ref TeamAspect player) {
                
                var fow = player.ent.Read<FogOfWarComponent>();
                fow.nodes.Clear();

            }

        }

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdateHeightJob : IJobParallelFor {

            public MemArrayAuto<ulong> dirtyChunks;
            public uint2 fowSize;
            public BuildGraphSystem pathfinding;
            public World world;
            public RootGraphComponent graph;
            public Ent heights;

            public void Execute(int index) {
                
                if (this.dirtyChunks.isCreated == true && this.dirtyChunks[index] != this.world.state->tick) return;

                ref var fow = ref this.heights.Get<FogOfWarStaticComponent>();
                var chunk = this.graph.chunks[index];
                var maxHeight = 0f;
                for (uint i = 0; i < chunk.nodes.Length; ++i) {
                    var nodeHeight = chunk.nodes[this.world.state, i].height;
                    var worldPos = Graph.GetPosition(this.graph, in chunk, i);
                    var height = math.max(nodeHeight, this.pathfinding.ReadHeights().GetHeight(worldPos));
                    var xy = FogOfWarUtils.WorldToFogMapPosition(in fow, in worldPos);
                    fow.heights[xy.y * this.fowSize.x + xy.x] = height;
                    if (height > maxHeight) {
                        maxHeight = height;
                    }
                }

                JobUtils.SetIfGreater(ref fow.maxHeight, maxHeight);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var firstGraph = pathfinding.GetGraphByTypeId(0u).Read<RootGraphComponent>();
            
            var fowSize = math.max(32u, (uint2)(this.mapSize * this.resolution));
            var cleanUpHandle = context.Query().Schedule<CleanUpJob, TeamAspect>();
            var updateHeightHandle = new UpdateHeightJob() {
                dirtyChunks = firstGraph.changedChunks,
                fowSize = fowSize,
                pathfinding = pathfinding,
                world = context.world,
                graph = firstGraph,
                heights = this.heights,
            }.Schedule((int)firstGraph.chunks.Length, (int)JobUtils.GetScheduleBatchCount(firstGraph.chunks.Length), context.dependsOn);
            context.SetDependency(JobHandle.CombineDependencies(cleanUpHandle, updateHeightHandle));

        }

        [INLINE(256)]
        public bool IsVisible(in Ent team, in Ent unit) {
            
            if (unit.Has<OwnerComponent>() == false || team == UnitUtils.GetTeam(unit.GetAspect<UnitAspect>())) return true;
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y, unit.Read<NavAgentRuntimeComponent>().properties.radius);

        }

        [INLINE(256)]
        public bool IsVisible(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public bool IsVisible(in PlayerAspect player, in Ent unit) => this.IsVisible(player.readTeam, in unit);

        [INLINE(256)]
        public bool IsVisible(in PlayerAspect player, in float3 position) => this.IsVisible(player.readTeam, in position);

        [INLINE(256)]
        public bool IsExplored(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public bool IsExplored(in Ent team, in Ent unit) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public bool IsExplored(in PlayerAspect player, in float3 position) => this.IsExplored(player.readTeam, in position);

        [INLINE(256)]
        public bool IsExplored(in PlayerAspect player, in Ent unit) => this.IsExplored(player.readTeam, in unit);

    }

}