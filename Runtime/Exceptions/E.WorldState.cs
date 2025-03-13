namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public unsafe class WorldStateException : System.Exception {

            public WorldStateException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(WorldState required, WorldState worldState, safe_ptr<State> state) {
                ThrowNotBurst(required, worldState, state);
                throw new OutOfRangeException($"Out of state. Required world state {required}.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(WorldState required, WorldState worldState, safe_ptr<State> state) => throw new WorldStateException(Exception.Format($"Out of state. Required world state {required}, current state {worldState}. Update type: {state.ptr->updateType}"));

        }

    }
    
    public static unsafe partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_IN_TICK(safe_ptr<State> state) {

            if (state.ptr->Mode == WorldMode.Visual ||
                state.ptr->tickCheck == 0 ||
                state.ptr->updateType != UpdateType.FIXED_UPDATE ||
                state.ptr->WorldState == WorldState.Initialized ||
                state.ptr->WorldState == WorldState.BeginTick) {
                return;
            }
            
            WorldStateException.Throw(WorldState.BeginTick, state.ptr->WorldState, state);
            
        }

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void IS_NOT_IN_TICK(safe_ptr<State> state) {

            if (state.ptr->Mode == WorldMode.Visual ||
                state.ptr->tickCheck == 0 ||
                state.ptr->updateType == UpdateType.UPDATE ||
                state.ptr->updateType == UpdateType.LATE_UPDATE ||
                state.ptr->WorldState == WorldState.Initialized ||
                state.ptr->WorldState == WorldState.EndTick) {
                return;
            }
            
            WorldStateException.Throw(WorldState.EndTick, state.ptr->WorldState, state);

        }

    }

}