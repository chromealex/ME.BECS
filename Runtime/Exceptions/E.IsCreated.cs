namespace ME.BECS {

    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public unsafe class NotCreatedException : System.Exception {

            public NotCreatedException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(QueryBuilder obj) {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            [HIDE_CALLSTACK]
            public static void Throw<T>(T obj) {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            [HIDE_CALLSTACK]
            public static void Throw<T>(T* obj) where T : unmanaged {
                ThrowNotBurst(obj);
                throw new NotCreatedException("Object is not created");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(T obj) => throw new NotCreatedException($"{Exception.Format(typeof(T).Name)} is not created");

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(QueryBuilder obj) => throw new NotCreatedException($"{Exception.Format("QueryBuilder")} is not created");

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(T* obj) where T : unmanaged => throw new NotCreatedException($"{Exception.Format(typeof(T).Name)} is not created");

        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<T>(T obj) where T : unmanaged, IIsCreated {
            if (obj.IsCreated == true) return;
            NotCreatedException.Throw(obj);
        }

    }

    public static unsafe partial class E {
        
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(in World world) {
            if (world.state.ptr != null) return;
            NotCreatedException.Throw(world);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(in Query query) {
            if (query.isCreated == true) return;
            NotCreatedException.Throw(query);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(in QueryBuilder queryBuilder) {
            if (queryBuilder.isCreated == true) return;
            NotCreatedException.Throw(queryBuilder);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(in QueryBuilderStatic queryBuilder) {
            if (queryBuilder.isCreated == true) return;
            NotCreatedException.Throw(queryBuilder);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(in QueryBuilderDispose queryBuilder) {
            if (queryBuilder.isCreated == true) return;
            NotCreatedException.Throw(queryBuilder);
        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<K, V>(EquatableDictionary<K, V> dic) where K : unmanaged, System.IEquatable<K> where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<V>(UIntDictionary<V> dic) where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<V>(ULongDictionary<V> dic) where V : unmanaged {
            if (dic.isCreated == true) return;
            NotCreatedException.Throw(dic);
        }

    }

    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(UIntHashSet list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(UIntPairHashSet list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(UIntHashSet* list) {
            if (list->IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<T>(List<T> list) where T : unmanaged {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED(UIntListHash list) {
            if (list.IsCreated == true) return;
            NotCreatedException.Throw(list);
        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_CREATED<T>(MemArray<T> arr) where T : unmanaged {
            if (arr.IsCreated == true) return;
            NotCreatedException.Throw(arr);
        }

    }

}