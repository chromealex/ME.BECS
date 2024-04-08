namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public unsafe class WorldStateException : System.Exception {

            public WorldStateException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(WorldState required, WorldState worldState, State* state) {
                ThrowNotBurst(required, worldState, state);
                throw new OutOfRangeException($"Out of state. Required world state {required}.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(WorldState required, WorldState worldState, State* state) => throw new WorldStateException(Exception.Format($"Out of state. Required world state {required}, current state {worldState}. Update type: {state->updateType}"));

        }

    }
    
    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_IN_TICK(State* state) {

            if (state->mode == WorldMode.Visual ||
                state->tickCheck == 0 ||
                state->updateType == UpdateType.FIXED_UPDATE ||
                state->worldState == WorldState.Initialized ||
                state->worldState == WorldState.BeginTick) {
                return;
            }
            
            WorldStateException.Throw(WorldState.BeginTick, state->worldState, state);
            
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_IN_TICK(State* state) {

            if (state->mode == WorldMode.Visual ||
                state->tickCheck == 0 ||
                state->updateType == UpdateType.UPDATE ||
                state->updateType == UpdateType.LATE_UPDATE ||
                state->worldState == WorldState.Initialized ||
                state->worldState == WorldState.EndTick) {
                return;
            }
            
            WorldStateException.Throw(WorldState.EndTick, state->worldState, state);

        }

    }

}