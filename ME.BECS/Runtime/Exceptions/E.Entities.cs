namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class EntityNotAliveException : System.Exception {

            public EntityNotAliveException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(Ent ent) {
                ThrowNotBurst(ent);
                throw new EntityNotAliveException("Entity is not alive");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Ent ent) => throw new EntityNotAliveException(Exception.Format($"{ent} is not alive"));

        }

        public class EntityIsEmptyException : System.Exception {

            public EntityIsEmptyException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(Ent ent) {
                ThrowNotBurst(ent);
                throw new EntityIsEmptyException("Entity is empty");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(Ent ent) => throw new EntityIsEmptyException(Exception.Format($"{ent} is empty"));

        }

    }

    public static partial class E {

        [Conditional(COND.EXCEPTIONS_ENTITIES)]
        [HIDE_CALLSTACK]
        public static void IS_ALIVE(in Ent ent) {
            if (ent == default) EntityIsEmptyException.Throw(ent);
            if (ent.IsAlive() == true) return;
            EntityNotAliveException.Throw(ent);
        }

    }

}