
namespace ME.BECS {
    
    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Carries the state required while composing or evaluating a query.
    /// </summary>
    [IgnoreProfiler]
    public ref struct QueryContext {

        internal safe_ptr<State> state;
        internal ushort worldId;

        /// <summary>
        /// Creates <c>QueryContext</c> using the supplied creation arguments.
        /// </summary>
        public static QueryContext Create(safe_ptr<State> state, ushort worldId) {
            return new QueryContext() { state = state, worldId = worldId, };
        }

        /// <summary>
        /// Creates <c>QueryContext</c> using the supplied creation arguments.
        /// </summary>
        public static QueryContext Create(in World world) {
            return new QueryContext() { state = world.state, worldId = world.id, };
        }
        
        /// <summary>
        /// Converts the supplied value to <c>QueryContext</c>.
        /// </summary>
        public static explicit operator QueryContext(in SystemContext context) {
            return new QueryContext() { state = context.world.state, worldId = context.world.id, };
        }

    }

    /// <summary>
    /// Provides helper operations for api.
    /// </summary>
    [IgnoreProfiler]
    public static class APIExt {

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query(this in SystemContext context, bool withInactive = false) {
            return API.Query(in context, withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query(this in SystemContext context, JobHandle dependsOn, bool withInactive = false) {
            return API.Query(in context, dependsOn, withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query<T>(this T system, in SystemContext context, bool withInactive = false) where T : unmanaged, ISystem {
            return API.Query(in context, withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query<T>(this T system, in SystemContext context, JobHandle dependsOn, bool withInactive = false) where T : unmanaged, ISystem {
            return API.Query(in context, dependsOn, withInactive);
        }

    }

    /// <summary>
    /// Provides api operations within <c>ME.BECS</c>.
    /// </summary>
    [IgnoreProfiler]
    public static class API {

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query(in World world, JobHandle dependsOn = default, bool withInactive = false) {
            return API.Query(QueryContext.Create(in world), dependsOn, withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query(in SystemContext systemContext, bool withInactive = false) {
            return API.Query((QueryContext)systemContext, systemContext.dependsOn, withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        public static QueryBuilder Query(in SystemContext systemContext, JobHandle dependsOn, bool withInactive = false) {
            return API.Query((QueryContext)systemContext, JobHandle.CombineDependencies(systemContext.dependsOn, dependsOn), withInactive);
        }

        /// <summary>
        /// Creates a query over entities in the associated world.
        /// </summary>
        [IgnoreProfiler]
        public static QueryBuilder Query(in QueryContext queryContext, JobHandle dependsOn = default, bool withInactive = false) {

            var allocator = WorldsTempAllocator.allocatorTemp.Get(queryContext.worldId).Allocator.ToAllocator;
            var builder = new QueryBuilder {
                queryData = _makeDefault(new QueryData(), allocator),
                commandBuffer = _makeDefault(new CommandBuffer {
                    state = queryContext.state,
                    worldId = queryContext.worldId,
                }, allocator),
                compose = new FlatQueries.QueryCompose().Initialize(allocator),
                isCreated = true,
                allocator = allocator,
                scheduleMode = Unity.Jobs.LowLevel.Unsafe.ScheduleMode.Single,
                builderDependsOn = dependsOn,
            };
            if (withInactive == false) builder.Without<IsInactive>();
            
            return builder;
            
        }

    }

}