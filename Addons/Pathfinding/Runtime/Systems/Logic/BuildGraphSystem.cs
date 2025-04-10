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
    using Unity.Collections.LowLevel.Unsafe;

    //[BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a pathfinding graph.")]
    public unsafe struct BuildGraphSystem : IAwake, IDestroy {

        public BECS.ObjectReference<AgentTypesConfig> agentTypesConfig;

        internal MemArray<Ent> graphs;
        internal MemArray<ME.BECS.Units.AgentType> types;
        internal Heights heights;
        internal World world;
        private tfloat nodeSize;

        public readonly tfloat GetNodeSize() => this.nodeSize;
        
        public readonly Heights ReadHeights() => this.heights;

        public void OnAwake(ref SystemContext context) {

            context.dependsOn.Complete();
            
            this.nodeSize = this.agentTypesConfig.Value.graphProperties.nodeSize;
            this.world = context.world;
            Heights heights = default;
            { // terrain case
                var terrain = UnityEngine.Terrain.activeTerrain;
                if (terrain != null) {
                    heights = Heights.Create((float3)terrain.GetPosition(), terrain.terrainData, context.world);
                }
            }

            if (heights.IsValid() == false) {
                heights = Heights.CreateDefault(context.world);
            }

            this.heights = heights;

            var config = this.agentTypesConfig.Value;
            var graphProperties = config.graphProperties;
            this.graphs = new MemArray<Ent>(ref context.world.state.ptr->allocator, (uint)config.agentTypes.Length);
            this.types = new MemArray<ME.BECS.Units.AgentType>(ref context.world.state.ptr->allocator, (uint)config.agentTypes.Length);
            var dependencies = new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(config.agentTypes.Length, Constants.ALLOCATOR_TEMP);
            for (int i = 0; i < config.agentTypes.Length; ++i) {
                var agentConfig = config.agentTypes[i];
                dependencies[i] = Graph.Build(in context.world, in heights, out var graphEnt, in graphProperties, in agentConfig, context.dependsOn);
                this.graphs[in context.world.state.ptr->allocator, i] = graphEnt;
                this.types[in context.world.state.ptr->allocator, i] = agentConfig;
            }

            var deps = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            dependencies.Dispose();
            context.SetDependency(deps);
            
        }
        
        public void OnDestroy(ref SystemContext context) {
            
            this.heights.Dispose();

        }

        [INLINE(256)]
        public readonly Ent GetGraphByTypeId(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.graphs.Length);
            return this.graphs[in this.world.state.ptr->allocator, agentTypeId];
        }

        [INLINE(256)]
        public readonly ME.BECS.Units.AgentType GetAgentProperties(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.types.Length);
            var type = this.types[in this.world.state.ptr->allocator, agentTypeId];
            type.typeId = agentTypeId;
            return type;
        }

        [INLINE(256)]
        public readonly uint GetTargetsCapacity() {
            return (uint)this.types.Length;
        }

    }

}