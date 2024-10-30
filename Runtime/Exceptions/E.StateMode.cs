namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using static Cuts;

    public partial class E {

        public class ModeException : System.Exception {

            public ModeException(string message) : base(message) { }

            [HIDE_CALLSTACK]
            public static void Throw(WorldMode current, WorldMode required) {
                throw new ModeException($"Mode {current} must be {required} to use this method.");
            }

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_VISUAL_MODE(WorldMode mode) {

            if (mode == WorldMode.Visual) return;
            ModeException.Throw(mode, WorldMode.Visual);

        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_LOGIC_MODE(WorldMode mode) {

            if (mode == WorldMode.Logic) return;
            ModeException.Throw(mode, WorldMode.Logic);

        }

    }

}