#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;
    using Unity.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Units;

    /// <summary>
    /// Coordinates create during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct CreateSystem : IAwake, IUpdate {

        /// <summary>
        /// Map position used by the associated spatial operation.
        /// </summary>
        public float2 mapPosition;
        /// <summary>
        /// Map size used by <c>CreateSystem</c>.
        /// </summary>
        public float2 mapSize;
        /// <summary>
        /// Resolution used by <c>CreateSystem</c>.
        /// </summary>
        public tfloat resolution;
        /// <summary>
        /// Pathfinding graph id used to locate the associated entry.
        /// </summary>
        public uint pathfindingGraphId;
        internal Ent heights;

        /// <summary>
        /// Returns heights.
        /// </summary>
        [INLINE(256)]
        public readonly Ent GetHeights() => this.heights;
        
        /// <summary>
        /// Executes create work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CreateJob : IJobForAspects<TeamAspect> {

            /// <summary>
            /// Fow size used by <c>CreateSystem.CreateJob</c>.
            /// </summary>
            public uint2 fowSize;
            
            /// <summary>
            /// Processes create using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TeamAspect aspect) {

                var map = new FogOfWarComponent() {
                    nodes = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                    explored = new MemArrayAuto<byte>(aspect.ent, this.fowSize.x * this.fowSize.y * FogOfWarUtils.BYTES_PER_NODE),
                };
                aspect.ent.Set(map);
                
            }

        }

        /// <summary>
        /// Executes clean up work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct CleanUpJob : IJobForAspects<TeamAspect> {
            
            /// <summary>
            /// Processes clean up using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TeamAspect player) {
                
                var fow = player.ent.Read<FogOfWarComponent>();
                fow.nodes.Clear();

            }

        }

        /// <summary>
        /// Executes update height work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct UpdateHeightJob : IJobParallelFor {

            /// <summary>
            /// Dirty chunks used by <c>CreateSystem.UpdateHeightJob</c>.
            /// </summary>
            public MemArrayAuto<ulong> dirtyChunks;
            /// <summary>
            /// World used by the containing operation.
            /// </summary>
            public World world;
            /// <summary>
            /// Height samples used by the geometry or graph.
            /// </summary>
            public Ent heights;
            /// <summary>
            /// Graph used by the associated operation.
            /// </summary>
            public Ent graph;

            /// <summary>
            /// Processes update height using the supplied job inputs.
            /// </summary>
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

        /// <summary>
        /// Initializes create system state from the supplied context.
        /// </summary>
        public void OnAwake(ref SystemContext context) {
            
            // for each player
            // create fog of war
            var pathfinding = context.world.GetSystem<BuildGraphSystem>();
            var fowSize = math.max(8u, (uint2)(this.mapSize * this.resolution));
            var heights = Ent.New<SingletonEntityType>(in context, editorName: "FOW");
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

        /// <summary>
        /// Updates create system using the current inputs and execution context.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<float3> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<UnityEngine.Rect> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisibleAny(in PlayerAspect player, in MemArrayAuto<RectUInt> points) {

            var team = player.readTeam;
            return this.IsVisibleAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is visible any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<float3> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<UnityEngine.Rect> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExploredAny(in PlayerAspect player, in MemArrayAuto<RectUInt> points) {

            var team = player.readTeam;
            return this.IsExploredAny(in team, in points);
            
        }

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is explored any.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
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

        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisible(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsVisible(in props, in fow, pos.x, pos.y);

        }

        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisible(in PlayerAspect player, in Ent unit) => this.IsVisible(player.readTeam, in unit);

        /// <summary>
        /// Tests whether the context is visible.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsVisible(in PlayerAspect player, in float3 position) => this.IsVisible(player.readTeam, in position);

        /// <summary>
        /// Tests whether the context is explored.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExplored(in Ent team, in float3 position) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, position);
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        /// <summary>
        /// Tests whether the context is explored.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExplored(in Ent team, in Ent unit) {
            
            ref readonly var fow = ref team.Read<FogOfWarComponent>();
            ref readonly var props = ref this.heights.Read<FogOfWarStaticComponent>();
            var pos = FogOfWarUtils.WorldToFogMapPosition(in props, unit.GetAspect<TransformAspect>().GetWorldMatrixPosition());
            return FogOfWarUtils.IsExplored(in props, in fow, pos.x, pos.y);

        }

        /// <summary>
        /// Tests whether the context is explored.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExplored(in PlayerAspect player, in float3 position) => this.IsExplored(player.readTeam, in position);

        /// <summary>
        /// Tests whether the context is explored.
        /// </summary>
        [INLINE(256)]
        public readonly bool IsExplored(in PlayerAspect player, in Ent unit) => this.IsExplored(player.readTeam, in unit);

    }

}