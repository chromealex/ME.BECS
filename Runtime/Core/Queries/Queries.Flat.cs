namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Indexes component membership for entity-query evaluation.
    /// </summary>
    [BURST]
    [IgnoreProfiler]
    #if !BECS_IL2CPP_OPTIONS_DISABLE
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
    #endif
    public static unsafe class FlatQueries {

        /// <summary>
        /// Describes the component filters combined into a query.
        /// </summary>
        [IgnoreProfiler]
        #if !BECS_IL2CPP_OPTIONS_DISABLE
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
        #endif
        public struct QueryCompose {

            internal UnsafeList<uint> with;
            internal UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>> withAny;
            internal UnsafeList<uint> without;
            
            /// <summary>
            /// Initializes query compose state from the supplied context.
            /// </summary>
            [INLINE(256)]
            public QueryCompose Initialize(Unity.Collections.Allocator allocator) {
                this.with = new UnsafeList<uint>(4, allocator);
                this.withAny = default;
                this.without = default;
                return this;
            }

            /// <summary>
            /// Releases the resources owned by this query compose instance.
            /// </summary>
            [INLINE(256)]
            public void Dispose() {
                this.with.Dispose();
                if (this.withAny.IsCreated == true) this.withAny.Dispose();
                if (this.without.IsCreated == true) this.without.Dispose();
            }
            
            /// <summary>
            /// Requires the specified component or filter in the query.
            /// </summary>
            [INLINE(256)]
            public void With<T>() where T : unmanaged, IComponentBase {
                this.with.Add(StaticTypes<T>.typeId);
            }

            /// <summary>
            /// Accepts entities matching at least one of the specified component types.
            /// </summary>
            [INLINE(256)]
            public void WithAny<T0, T1>() where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
                if (this.withAny.IsCreated == false) this.withAny = new UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>>(1, this.with.Allocator);
                this.withAny.Add(new System.Collections.Generic.KeyValuePair<uint, uint>(StaticTypes<T0>.typeId, StaticTypes<T1>.typeId));
            }

            /// <summary>
            /// Excludes entities matching the specified component or filter.
            /// </summary>
            [INLINE(256)]
            public void Without<T>() where T : unmanaged, IComponentBase {
                if (this.without.IsCreated == false) this.without = new UnsafeList<uint>(1, this.with.Allocator);
                this.without.Add(StaticTypes<T>.typeId);
            }

            /// <summary>
            /// Adds the component requirements of the specified aspect to the query.
            /// </summary>
            [INLINE(256)]
            public void WithAspect<T>() where T : unmanaged, IAspect {
                var arr = AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId);
                this.with.AddRange(arr.ptr.ptr, (int)arr.Length);
            }

        }

    }

}
