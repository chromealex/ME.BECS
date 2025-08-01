#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;
    using Unity.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;

    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CreateSystem : IAwake, IUpdate {

        public float2 mapPosition;
        public float2 mapSize;
        public tfloat resolution;
        public uint pathfindingGraphId;
        internal Ent heights;

        [INLINE(256)]
        public readonly Ent GetHeights() => this.heights;
        
        [BURST]
        public struct CreateJob : IJobForAspects<TeamAspect> {

            public uint2 fowSize;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TeamAspect aspect) {

                var map = new FogOfWarComponent() {
                    nodes = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                    explored = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                };
                aspect.ent.Set(map);
                
            }

        }

        [BURST]
        public struct CleanUpJob : IJobForAspects<TeamAspect> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TeamAspect player) {
                
                var fow = player.ent.Read<FogOfWarComponent>();
                fow.nodes.Clear();

            }

        }

        [BURST]
        public struct UpdateHeightJob : IJobParallelFor {

            public MemArrayAuto<ulong> dirtyChunks;
            public World world;
            public Ent heights;
            public Ent graph;

            public void Execute(int index) {
                
                if (this.dirtyChunks.IsCreated == true && this.dirtyChunks[index] != this.world.CurrentTick && this.dirtyChunks[index] != this.world.CurrentTick + 1u && this.dirtyChunks[index] != this.world.CurrentTick - 1u) return;

                var graphData = this.graph.Read<RootGraphComponent>();
                var chunk = graphData.chunks[index];
                ref var fow = ref this.heights.Get<FogOfWarStaticComponent>();
                tfloat maxHeight = 0f;
                for (uint i = 0; i < chunk.nodes.Length; ++i) {
                    var worldPos = Graph.GetPosition(in graphData, in chunk, i);
                    var xy = FogOfWarUtils.WorldToFogMapPosition(in fow, worldPos);
                    var currentHeight = Graph.GetMinHeight(in graphData, (uint)index, i, checkNeighbours: true);
                    var idx = xy.y * fow.size.x + xy.x;
                    fow.heights[idx] = currentHeight;
                    if (currentHeight > maxHeight) {
                        maxHeight = currentHeight;
                    }
                }
                
                JobUtils.SetIfGreater(ref fow.maxHeight, maxHeight);

            }

        }

        public void OnAwake(ref SystemContext context) {
            
            // for each player
            // create fog of war
            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var fowSize = math.max(8u, (uint2)(this.mapSize * this.resolution));
            var heights = Ent.New(in context, editorName: "FOW");
            heights.Set(new FogOfWarStaticComponent() {
                mapPosition = this.mapPosition,
                nodeSize = pathfinding.GetNodeSize() / this.resolution,
                size = fowSize,
                heights = new MemArrayAuto<tfloat>(heights, fowSize.x * fowSize.y),
            });
            this.heights = heights;
            var dependsOn = context.Query().AsParallel().Schedule<CreateJob, TeamAspect>(new CreateJob() {
                fowSize = fowSize,
            });
            
            var firstGraph = pathfinding.GetGraphByTypeId(this.pathfindingGraphId).Read<RootGraphComponent>();
            var updateHeightHandle = new UpdateHeightJob() {
                world = context.world,
                heights = this.heights,
                graph = pathfinding.GetGraphByTypeId(this.pathfindingGraphId),
            }.Schedule((int)firstGraph.chunks.Length, (int)JobUtils.GetScheduleBatchCount(firstGraph.chunks.Length), dependsOn);
            context.SetDependency(updateHeightHandle);
            
            FogOfWarData.Initialize(fowSize.x);

        }

        public void OnUpdate(ref SystemContext context) {

            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var firstGraph = pathfinding.GetGraphByTypeId(this.pathfindingGraphId).Read<RootGraphComponent>();
            
            var cleanUpHandle = context.Query().AsParallel().Schedule<CleanUpJob, TeamAspect>();
            var updateHeightHandle = new UpdateHeightJob() {
                dirtyChunks = firstGraph.changedChunks,
                world = context.world,
                heights = this.heights,
                graph = pathfinding.GetGraphByTypeId(this.pathfindingGraphId),
            }.Schedule((int)firstGraph.chunks.Length, (int)JobUtils.GetScheduleBatchCount(firstGraph.chunks.Length), context.dependsOn);
            context.SetDependency(JobHandle.CombineDependencies(cleanUpHandle, updateHeightHandle));

        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<float3> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<UnityEngine.Rect> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<RectUInt> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in Ent team, in MemArrayAuto<float3> points) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var worldPos = points[i];
                var pos = FogOfWarUtils.WorldToFogMapPosition(in props, in worldPos);
                if (FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y) == true) return true;
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in Ent team, in MemArrayAuto<UnityEngine.Rect> points) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var rect = points[i];
                var min = FogOfWarUtils.WorldToFogMapPosition(in props, new float3(rect.xMin, 0f, rect.yMin));
                var max = FogOfWarUtils.WorldToFogMapPosition(in props, new float3(rect.xMax, 0f, rect.yMax));
                for (uint x = min.x; x < max.x; ++x) {
                    for (uint y = min.y; x < max.y; ++y) {
                        if (FogOfWarUtils.IsVisible(in props, in fow, x, y) == true) return true;
                    }
                }
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsVisibleAny(in Ent team, in MemArrayAuto<RectUInt> points) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var rect = points[i];
                var min = rect.min;
                min.x = math.clamp(min.x, 0u, props.size.x - 1u);
                min.y = math.clamp(min.y, 0u, props.size.y - 1u);
                var max = rect.max;
                max.x = math.clamp(max.x, 0u, props.size.x - 1u);
                max.y = math.clamp(max.y, 0u, props.size.y - 1u);
                for (uint x = min.x; x <= max.x; ++x) {
                    for (uint y = min.y; y <= max.y; ++y) {
                        if (FogOfWarUtils.IsVisible(in props, in fow, x, y) == true) return true;
                    }
                }
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<float3> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<UnityEngine.Rect> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<RectUInt> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in Ent team, in MemArrayAuto<float3> points) {

            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var worldPos = points[i];
                var pos = FogOfWarUtils.WorldToFogMapPosition(in props, in worldPos);
                if (FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y) == true) return true;
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in Ent team, in MemArrayAuto<UnityEngine.Rect> points) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var rect = points[i];
                var min = FogOfWarUtils.WorldToFogMapPosition(in props, new float3(rect.xMin, 0f, rect.yMin));
                var max = FogOfWarUtils.WorldToFogMapPosition(in props, new float3(rect.xMax, 0f, rect.yMax));
                for (uint x = min.x; x < max.x; ++x) {
                    for (uint y = min.y; x < max.y; ++y) {
                        if (FogOfWarUtils.IsExplored(in props, in fow, x, y) == true) return true;
                    }
                }
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsExploredAny(in Ent team, in MemArrayAuto<RectUInt> points) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            for (uint i = 0u; i < points.Length; ++i) {
                var rect = points[i];
                var min = rect.min;
                min.x = math.clamp(min.x, 0u, props.size.x);
                min.y = math.clamp(min.y, 0u, props.size.y);
                var max = rect.max;
                max.x = math.clamp(max.x, 0u, props.size.x);
                max.y = math.clamp(max.y, 0u, props.size.y);
                for (uint x = min.x; x < max.x; ++x) {
                    for (uint y = min.y; y < max.y; ++y) {
                        if (FogOfWarUtils.IsExplored(in props, in fow, x, y) == true) return true;
                    }
                }
            }
            return false;

        }

        [INLINE(256)]
        public readonly bool IsVisible(in Ent team, in Ent unit) {
            
            if (unit.Has<OwnerComponent>() == false || team == UnitUtils.GetTeam(in unit)) return true;
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            if (unit.TryRead(out UnitQuadSizeComponent quadSizeComponent) == true) {
                return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y, quadSizeComponent.size);
            }
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y, unit.Read<NavAgentRuntimeComponent>().properties.radius);

        }

        [INLINE(256)]
        public readonly bool IsVisible(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public readonly bool IsVisible(in PlayerAspect player, in Ent unit) => this.IsVisible(player.readTeam, in unit);

        [INLINE(256)]
        public readonly bool IsVisible(in PlayerAspect player, in float3 position) => this.IsVisible(player.readTeam, in position);

        [INLINE(256)]
        public readonly bool IsExplored(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public readonly bool IsExplored(in Ent team, in Ent unit) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        [INLINE(256)]
        public readonly bool IsExplored(in PlayerAspect player, in float3 position) => this.IsExplored(player.readTeam, in position);

        [INLINE(256)]
        public readonly bool IsExplored(in PlayerAspect player, in Ent unit) => this.IsExplored(player.readTeam, in unit);

    }

}