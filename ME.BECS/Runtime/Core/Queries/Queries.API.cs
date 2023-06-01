
namespace ME.BECS {
    
    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public unsafe ref struct QueryContext {

        internal State* state;
        internal ushort worldId;

        public static QueryContext Create(State* state, ushort worldId) {
            return new QueryContext() { state = state, worldId = worldId, };
        }

        public static QueryContext Create(in World world) {
            return new QueryContext() { state = world.state, worldId = world.id, };
        }
        
        public static explicit operator QueryContext(in SystemContext context) {
            return new QueryContext() { state = context.world.state, worldId = context.world.id, };
        }

    }

    public static class APIExt {

        public static QueryBuilder Query<T>(this T system, in SystemContext context) where T : unmanaged, ISystem {
            return API.Query(context);
        }

        public static QueryBuilder Query<T>(this T system, in SystemContext context, JobHandle dependsOn) where T : unmanaged, ISystem {
            return API.Query(context, dependsOn);
        }

    }

    public static unsafe class API {

        [BURST]
        private struct BuilderArchetypesJob : IJob {

            [NativeDisableUnsafePtrRestriction]
            public State* state;
            [NativeDisableUnsafePtrRestriction]
            public QueryData* queryData;
            
            public void Execute() {

                this.queryData->archetypesBits = new TempBitArray(in this.state->allocator, this.state->archetypes.allArchetypesForQuery, Unity.Collections.Allocator.Persistent);

            }

        }

        public static QueryBuilder Query(in World world, JobHandle dependsOn = default) {
            return API.Query(QueryContext.Create(in world), dependsOn);
        }

        public static QueryBuilder Query(in SystemContext systemContext) {
            return API.Query((QueryContext)systemContext, systemContext.dependsOn);
        }

        public static QueryBuilder Query(in SystemContext systemContext, JobHandle dependsOn) {
            return API.Query((QueryContext)systemContext, JobHandle.CombineDependencies(systemContext.dependsOn, dependsOn));
        }

        public static QueryBuilder Query(in QueryContext queryContext, JobHandle dependsOn = default) {

            dependsOn = Batches.Apply(dependsOn, queryContext.state, queryContext.worldId);
            
            var builder = new QueryBuilder() {
                queryData = _make(new QueryData()),
                commandBuffer = _make(new CommandBuffer() {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }),
                isCreated = true,
            };
            var job = new BuilderArchetypesJob() {
                state = queryContext.state,
                queryData = builder.queryData,
            };
            var jobHandle = job.Schedule(dependsOn);
            builder.builderDependsOn = jobHandle;
            return builder;
            
        }

        internal static QueryBuilder MakeStaticQuery(in QueryContext queryContext, JobHandle dependsOn) {

            dependsOn = Batches.Apply(dependsOn, queryContext.state, queryContext.worldId);

            var builder = new QueryBuilder() {
                queryData = _make(new QueryData()),
                commandBuffer = _make(new CommandBuffer() {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }),
                isCreated = true,
            };
            builder.builderDependsOn = dependsOn;
            return builder;
            
        }

    }

}