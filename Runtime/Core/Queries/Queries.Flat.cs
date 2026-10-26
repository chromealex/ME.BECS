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
        public unsafe struct QueryCompose {

            internal UnsafeList<uint> with;
            internal UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>> withAny;
            internal UnsafeList<uint> without;
            private Unity.Collections.Allocator allocator;
            private bool borrowed;

            internal QueryCompose Borrow(Unity.Collections.Allocator executionAllocator) {
                var result = this;
                result.allocator = executionAllocator;
                result.borrowed = true;
                return result;
            }

            private void MakeWritable() {
                if (this.borrowed == false) {
                    return;
                }
                this.with = Copy(this.with, this.allocator);
                this.withAny = Copy(this.withAny, this.allocator);
                this.without = Copy(this.without, this.allocator);
                this.borrowed = false;
            }

            private static UnsafeList<T> Copy<T>(UnsafeList<T> source, Unity.Collections.Allocator allocator) where T : unmanaged {
                if (source.IsCreated == false) {
                    return default;
                }
                var result = new UnsafeList<T>(System.Math.Max(1, source.Length), allocator);
                result.AddRange(source.Ptr, source.Length);
                return result;
            }

            private static bool Contains(UnsafeList<uint> list, uint typeId) {
                for (int i = 0; i < list.Length; ++i) {
                    if (list[i] == typeId) {
                        return true;
                    }
                }
                return false;
            }

            internal void Deduplicate() {
                Deduplicate(ref this.with);
                Deduplicate(ref this.without);
            }

            private static void Deduplicate(ref UnsafeList<uint> list) {
                for (int i = 0; i < list.Length; ++i) {
                    for (int j = list.Length - 1; j > i; --j) {
                        if (list[j] == list[i]) {
                            list.RemoveAt(j);
                        }
                    }
                }
            }
            
            /// <summary>
            /// Initializes query compose state from the supplied context.
            /// </summary>
            [INLINE(256)]
            public QueryCompose Initialize(Unity.Collections.Allocator allocator) {
                this.allocator = allocator;
                this.borrowed = false;
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
                if (this.borrowed == true) {
                    this = default;
                    return;
                }
                this.with.Dispose();
                if (this.withAny.IsCreated == true) this.withAny.Dispose();
                if (this.without.IsCreated == true) this.without.Dispose();
            }
            
            /// <summary>
            /// Requires the specified component or filter in the query.
            /// </summary>
            [INLINE(256)]
            public void With<T>() where T : unmanaged, IComponentBase {
                var typeId = StaticTypes<T>.typeId;
                if (this.borrowed == true && Contains(this.with, typeId) == true) {
                    return;
                }
                this.MakeWritable();
                this.with.Add(typeId);
            }

            /// <summary>
            /// Accepts entities matching at least one of the specified component types.
            /// </summary>
            [INLINE(256)]
            public void WithAny<T0, T1>() where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
                this.MakeWritable();
                if (this.withAny.IsCreated == false) {
                    this.withAny = new UnsafeList<System.Collections.Generic.KeyValuePair<uint, uint>>(1, this.allocator);
                }
                this.withAny.Add(new System.Collections.Generic.KeyValuePair<uint, uint>(StaticTypes<T0>.typeId, StaticTypes<T1>.typeId));
            }

            /// <summary>
            /// Excludes entities matching the specified component or filter.
            /// </summary>
            [INLINE(256)]
            public void Without<T>() where T : unmanaged, IComponentBase {
                var typeId = StaticTypes<T>.typeId;
                if (this.borrowed == true && Contains(this.without, typeId) == true) {
                    return;
                }
                this.MakeWritable();
                if (this.without.IsCreated == false) {
                    this.without = new UnsafeList<uint>(1, this.allocator);
                }
                this.without.Add(typeId);
            }

            /// <summary>
            /// Adds the component requirements of the specified aspect to the query.
            /// </summary>
            [INLINE(256)]
            public void WithAspect<T>() where T : unmanaged, IAspect {
                var arr = AspectTypeInfo.with.Get(AspectTypeInfo<T>.typeId);
                if (this.borrowed == true) {
                    for (uint i = 0u; i < arr.Length; ++i) {
                        if (Contains(this.with, arr.ptr.ptr[i]) == false) {
                            this.MakeWritable();
                            break;
                        }
                    }
                    if (this.borrowed == true) {
                        return;
                    }
                }
                this.with.AddRange(arr.ptr.ptr, (int)arr.Length);
            }

        }

    }

}
