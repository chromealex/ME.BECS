namespace ME.BECS {

    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the not created invariant.
        /// </summary>
        public unsafe class NotCreatedException : System.Exception {

            /// <summary>
            /// Initializes <c>NotCreatedException</c> from the supplied str.
            /// </summary>
            public NotCreatedException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotCreatedException</c>.
            /// </summary>
            [HIDE_CALLSTACK][IgnoreProfiler]
            public static void Throw(QueryBuilder obj) {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotCreatedException</c>.
            /// </summary>
            [HIDE_CALLSTACK][IgnoreProfiler]
            public static void Throw<T>(T obj) {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotCreatedException</c>.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void Throw() {
                throw new NotCreatedException("Object is not created");
            }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.NotCreatedException</c>.
            /// </summary>
            [HIDE_CALLSTACK][IgnoreProfiler]
            public static void Throw<T>(T* obj) where T : unmanaged {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK][IgnoreProfiler]
            private static void ThrowNotBurst<T>(T obj) => throw new NotCreatedException($"{Exception.Format(typeof(T).Name)} is not created");

            [BURST_DISCARD]
            [HIDE_CALLSTACK][IgnoreProfiler]
            private static void ThrowNotBurst(QueryBuilder obj) => throw new NotCreatedException($"{Exception.Format("QueryBuilder")} is not created");

            [BURST_DISCARD]
            [HIDE_CALLSTACK][IgnoreProfiler]
            private static void ThrowNotBurst<T>(T* obj) where T : unmanaged => throw new NotCreatedException($"{Exception.Format(typeof(T).Name)} is not created");

        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<T>(T obj) where T : unmanaged, IIsCreated {
            if (obj.IsCreated == true) return;
            NotCreatedException.Throw(obj);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static unsafe partial class E {
        
        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(in World world) {
            if (world.state.ptr != null) return;
            NotCreatedException.Throw(world);
        }


        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(in QueryBuilder queryBuilder) {
            if (queryBuilder.isCreated == true) return;
            NotCreatedException.Throw(queryBuilder);
        }

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(in QueryBuilderDispose queryBuilder) {
            if (queryBuilder.isCreated == true) return;
            NotCreatedException.Throw(queryBuilder);
        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<K, V>(EquatableDictionary<K, V> dic) where K : unmanaged, System.IEquatable<K> where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<V>(UIntDictionary<V> dic) where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<V>(ULongDictionary<V> dic) where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static unsafe partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(UIntHashSet list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(UIntPairHashSet list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(UIntHashSet* list) {
            if (list->IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<T>(List<T> list) where T : unmanaged {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED(UIntListHash list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the is created invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void IS_CREATED<T>(MemArray<T> arr) where T : unmanaged {
            if (arr.IsCreated == true) return;
            NotCreatedException.Throw(arr);
        }

    }

}