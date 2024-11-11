using Unity.Collections.LowLevel.Unsafe;

namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;

    //[BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a pathfinding graph.")]
    public unsafe struct BuildGraphSystem : IAwake, IDestroy {

        public BECS.ObjectReference<AgentTypesConfig> agentTypesConfig;

        [Unity.Collections.ReadOnlyAttribute]
        internal Unity.Collections.NativeArray<Ent> graphs;
        [NativeDisableUnsafePtrRestriction]
        internal Ent* graphsPtr;
        [Unity.Collections.ReadOnlyAttribute]
        internal Unity.Collections.NativeArray<ME.BECS.Units.AgentType> types;
        internal Heights heights;
        private float nodeSize;

        public readonly float GetNodeSize() => this.nodeSize;
        
        public readonly Heights ReadHeights() => this.heights;

        public void OnAwake(ref SystemContext context) {

            this.nodeSize = this.agentTypesConfig.Value.graphProperties.nodeSize;
            
            Heights heights = default;
            { // terrain case
                var terrain = UnityEngine.Terrain.activeTerrain;
                if (terrain != null) {
                    heights = Heights.Create(terrain.GetPosition(), terrain.terrainData, Constants.ALLOCATOR_PERSISTENT);
                }
            }

            if (heights.IsValid() == false) {
                heights = Heights.CreateDefault(Constants.ALLOCATOR_PERSISTENT);
            }

            this.heights = heights;

            var config = this.agentTypesConfig.Value;
            var graphProperties = config.graphProperties;
            this.graphs = new Unity.Collections.NativeArray<Ent>(config.agentTypes.Length, Constants.ALLOCATOR_DOMAIN);
            this.graphsPtr = (Ent*)this.graphs.GetUnsafePtr();
            this.types = new Unity.Collections.NativeArray<ME.BECS.Units.AgentType>(config.agentTypes.Length, Constants.ALLOCATOR_DOMAIN);
            var dependencies = new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(config.agentTypes.Length, Constants.ALLOCATOR_TEMP);
            for (int i = 0; i < config.agentTypes.Length; ++i) {
                var agentConfig = config.agentTypes[i];
                dependencies[i] = Graph.Build(in context.world, in heights, out var graphEnt, in graphProperties, in agentConfig, context.dependsOn);
                this.graphs[i] = graphEnt;
                this.types[i] = agentConfig;
            }

            var deps = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            context.SetDependency(deps);
            
        }
        
        public void OnDestroy(ref SystemContext context) {

            this.graphs.Dispose();
            this.types.Dispose();
            this.heights.Dispose();

        }

        [INLINE(256)]
        public readonly Ent GetGraphByTypeId(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.graphs.Length);
            return this.graphsPtr[agentTypeId];
        }

        [INLINE(256)]
        public readonly ME.BECS.Units.AgentType GetAgentProperties(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.types.Length);
            var type = this.types[(int)agentTypeId];
            type.typeId = agentTypeId;
            return type;
        }

        [INLINE(256)]
        public readonly uint GetTargetsCapacity() {
            return (uint)this.types.Length;
        }

    }

}