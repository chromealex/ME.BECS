namespace ME.BECS.Pathfinding {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;

    //[BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a pathfinding graph.")]
    public struct BuildGraphSystem : IAwake, IDestroy {

        public ME.BECS.Addons.ObjectReference<AgentTypesConfig> agentTypesConfig;

        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeArray<Ent> graphs;
        [Unity.Collections.ReadOnlyAttribute]
        public Unity.Collections.NativeArray<ME.BECS.Units.AgentType> types;
        public Heights heights;
        public Unity.Collections.NativeArray<bool> changedChunks;

        public int obstaclesTreeIndex;

        public void OnAwake(ref SystemContext context) {

            Heights heights = default;
            { // terrain case
                var terrain = UnityEngine.Terrain.activeTerrain;
                if (terrain != null) {
                    heights = Heights.Create(terrain.GetPosition(), terrain.terrainData, Unity.Collections.Allocator.Persistent);
                }
            }

            if (heights.IsValid() == false) {
                heights = Heights.CreateDefault(Unity.Collections.Allocator.Persistent);
            }

            this.heights = heights;

            var config = this.agentTypesConfig.Value;
            var graphProperties = config.graphProperties;
            this.graphs = new Unity.Collections.NativeArray<Ent>(config.agentTypes.Length, Unity.Collections.Allocator.Domain);
            this.types = new Unity.Collections.NativeArray<ME.BECS.Units.AgentType>(config.agentTypes.Length, Unity.Collections.Allocator.Domain);
            var dependencies = new Unity.Collections.NativeArray<Unity.Jobs.JobHandle>(config.agentTypes.Length, Unity.Collections.Allocator.Temp);
            for (int i = 0; i < config.agentTypes.Length; ++i) {
                var agentConfig = config.agentTypes[i];
                dependencies[i] = Graph.Build(in context.world, in heights, out var graphEnt, in graphProperties, in agentConfig, context.dependsOn);
                this.graphs[i] = graphEnt;
                this.types[i] = agentConfig;
            }

            var chunksLength = graphProperties.chunksCountX * graphProperties.chunksCountY;
            this.changedChunks = new Unity.Collections.NativeArray<bool>((int)chunksLength, Unity.Collections.Allocator.Persistent);

            var deps = Unity.Jobs.JobHandle.CombineDependencies(dependencies);
            context.SetDependency(deps);
            
        }
        
        public void OnDestroy(ref SystemContext context) {

            this.changedChunks.Dispose();
            this.graphs.Dispose();
            this.types.Dispose();
            this.heights.Dispose();

        }

        [INLINE(256)]
        public readonly Ent GetGraphByTypeId(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.graphs.Length);
            return this.graphs[(int)agentTypeId];
        }

        [INLINE(256)]
        public readonly ME.BECS.Units.AgentType GetAgentProperties(uint agentTypeId) {
            E.RANGE(agentTypeId, 0u, (uint)this.types.Length);
            var type = this.types[(int)agentTypeId];
            type.typeId = agentTypeId;
            return type;
        }

        [INLINE(256)]
        public uint GetTargetsCapacity() {
            return (uint)this.types.Length;
        }

    }

}