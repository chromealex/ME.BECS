namespace ME.BECS {

    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public unsafe class AlreadyCreatedException : System.Exception {

            public AlreadyCreatedException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw<T>(T obj) {
                ThrowNotBurst(obj);
                throw new AlreadyCreatedException("Object is already created");
            }

            [HIDE_CALLSTACK]
            public static void Throw<T>(T* obj) where T : unmanaged {
                ThrowNotBurst(obj);
                throw new AlreadyCreatedException("Object is already created");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(T obj) => throw new AlreadyCreatedException($"{Exception.Format(typeof(T).Name)} is already created");

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(T* obj) where T : unmanaged => throw new AlreadyCreatedException($"{Exception.Format(typeof(T).Name)} is not created");

        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_ALREADY_CREATED<T>(T obj) where T : unmanaged, IIsCreated {
            if (obj.isCreated == false) return;
            NotCreatedException.Throw(obj);
        }

    }

}