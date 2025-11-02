
namespace ME.BECS {
    
    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
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

    [IgnoreProfiler]
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

    [IgnoreProfiler]
    public static class API {

        public static QueryBuilder Query(in World world, JobHandle dependsOn = default) {
            return API.Query(QueryContext.Create(in world), dependsOn);
        }

        public static QueryBuilder Query(in SystemContext systemContext) {
            return API.Query((QueryContext)systemContext, systemContext.dependsOn);
        }

        public static QueryBuilder Query(in SystemContext systemContext, JobHandle dependsOn) {
            return API.Query((QueryContext)systemContext, JobHandle.CombineDependencies(systemContext.dependsOn, dependsOn));
        }

        [IgnoreProfiler]
        public static QueryBuilder Query(in QueryContext queryContext, JobHandle dependsOn = default) {

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
                builderDependsOn = dependsOn,
            };
            builder.Without<IsInactive>();
            
            return builder;
            
        }

        [IgnoreProfiler]
        internal static QueryBuilder MakeStaticQuery(in QueryContext queryContext, JobHandle dependsOn) {

            var allocator = WorldsPersistentAllocator.allocatorPersistent.Get(queryContext.worldId).Allocator.ToAllocator;
            var builder = new QueryBuilder {
                queryData = _makeDefault(new QueryData(), allocator),
                commandBuffer = _makeDefault(new CommandBuffer {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }, allocator),
                compose = new ArchetypeQueries.QueryCompose().Initialize(allocator),
                isCreated = true,
                builderDependsOn = dependsOn,
                allocator = allocator,
                scheduleMode = Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single,
            };
            builder.Without<IsInactive>();
            return builder;
            
        }

    }

}