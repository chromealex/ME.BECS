
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

                ref var fow = ref this.heights.Get<FogOfWarStaticComponent>();
                var chunk = this.graph.chunks[index];
                var maxHeight = 0;
                for (uint i = 0; i < chunk.nodes.Length; ++i) {
                    var nodeHeight = chunk.nodes[this.world.state, i].height;
                    var worldPos = Graph.GetPosition(this.graph, in chunk, i);
                    var height = math.max(nodeHeight, this.pathfinding.heights.GetHeight(worldPos));
                    var heightMap = FogOfWarUtils.WorldToFogMapValue(in fow, height);
                    var xy = FogOfWarUtils.WorldToFogMapPosition(in fow, in worldPos);
                    fow.heights[xy.y * this.fowSize.x + xy.x] = heightMap;
                    if (heightMap > maxHeight) {
                        maxHeight = heightMap;
                    }
                }

                JobUtils.SetIfGreater(ref fow.maxHeight, maxHeight);

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
            context.SetDependency(JobHandle.CombineDependencies(cleanUpHandle, updateHeightHandle));

        }

        [INLINE(256)]
        public bool IsVisible(in PlayerAspect player, in Ent unit) {
            
            if (unit.Has<OwnerComponent>() == true && player.readTeam == UnitUtils.GetTeam(unit.GetAspect<UnitAspect>())) return true;
            ref readonly var fow = ref player.readTeam.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public bool IsVisible(in PlayerAspect player, Ent unit) => this.IsVisible(in player, in unit);

    }

}