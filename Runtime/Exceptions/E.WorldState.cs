namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class WorldStateException : System.Exception {

            public WorldStateException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(WorldState worldState) {
                ThrowNotBurst(worldState);
                throw new OutOfRangeException("Out of state. Required world state WorldState.BeginTick.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(WorldState worldState) => throw new WorldStateException(Exception.Format($"Out of state. Required world state {WorldState.BeginTick}, current state {worldState}"));

        }

    }
    
    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_IN_TICK(State* state) {

            if (state->mode == WorldMode.Visual ||
                state->tickCheck == 0 ||
                state->worldState == WorldState.Initialized ||
                state->worldState == WorldState.BeginTick) {
                return;
            }
            
            WorldStateException.Throw(state->worldState);
            
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_IN_TICK(State* state) {

            if (state->mode == WorldMode.Visual ||
                state->tickCheck == 0 ||
                state->worldState == WorldState.Initialized ||
                state->worldState == WorldState.EndTick) {
                return;
            }
            
            WorldStateException.Throw(state->worldState);

        }

    }

}