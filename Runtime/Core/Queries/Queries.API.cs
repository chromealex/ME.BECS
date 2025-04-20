
namespace ME.BECS {
    
    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public ref struct QueryContext {

        internal safe_ptr<State> state;
        internal ushort worldId;

        public static QueryContext Create(safe_ptr<State> state, ushort worldId) {
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

        public static QueryBuilder Query(this in SystemContext context) {
            return API.Query(in context);
        }

        public static QueryBuilder Query(this in SystemContext context, JobHandle dependsOn) {
            return API.Query(in context, dependsOn);
        }

        public static QueryBuilder Query<T>(this T system, in SystemContext context) where T : unmanaged, ISystem {
            return API.Query(in context);
        }

        public static QueryBuilder Query<T>(this T system, in SystemContext context, JobHandle dependsOn) where T : unmanaged, ISystem {
            return API.Query(in context, dependsOn);
        }

    }

    public static unsafe class API {

        [BURST(CompileSynchronously = true)]
        private struct BuilderArchetypesJob : IJob {

            public safe_ptr<State> state;
            public safe_ptr<QueryData> queryData;
            public Unity.Collections.Allocator allocator;
            
            public void Execute() {

                this.queryData.ptr->archetypesBits = new TempBitArray(in this.state.ptr->allocator, this.state.ptr->archetypes.allArchetypesForQuery, this.allocator);

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

            //dependsOn = Batches.Apply(dependsOn, queryContext.state);
            //dependsOn = Batches.Open(dependsOn, queryContext.state);

            var allocator = WorldsTempAllocator.allocatorTemp.Get(queryContext.worldId).Allocator.ToAllocator;
            var builder = new QueryBuilder {
                queryData = _makeDefault(new QueryData(), allocator),
                commandBuffer = _makeDefault(new CommandBuffer {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }, allocator),
                compose = new ArchetypeQueries.QueryCompose().Initialize(allocator),
                isCreated = true,
                allocator = allocator,
                scheduleMode = Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single,
            };
            
            var job = new BuilderArchetypesJob() {
                state = queryContext.state,
                queryData = builder.queryData,
                allocator = allocator,
            };
            dependsOn = job.Schedule(dependsOn);
            builder.builderDependsOn = dependsOn;//Batches.Close(dependsOn, queryContext.state);
            builder.Without<IsInactive>();
            
            return builder;
            
        }

        internal static QueryBuilder MakeStaticQuery(in QueryContext queryContext, JobHandle dependsOn) {

            //dependsOn = Batches.Apply(dependsOn, queryContext.state);
            //dependsOn = Batches.Open(dependsOn, queryContext.state);

            var builder = new QueryBuilder {
                queryData = _makeDefault(new QueryData(), Constants.ALLOCATOR_PERSISTENT),
                commandBuffer = _makeDefault(new CommandBuffer {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }, Constants.ALLOCATOR_PERSISTENT),
                compose = new ArchetypeQueries.QueryCompose().Initialize(Constants.ALLOCATOR_PERSISTENT),
                isCreated = true,
                builderDependsOn = dependsOn,//Batches.Close(dependsOn, queryContext.state),
                allocator = Constants.ALLOCATOR_PERSISTENT,
                scheduleMode = Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single,
            };
            builder.Without<IsInactive>();
            return builder;
            
        }

    }

}