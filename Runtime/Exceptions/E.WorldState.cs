namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the world state invariant.
        /// </summary>
        public unsafe class WorldStateException : System.Exception {

            /// <summary>
            /// Initializes <c>WorldStateException</c> from the supplied str.
            /// </summary>
            public WorldStateException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.WorldStateException</c>.
            /// </summary>
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
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static unsafe partial class E {

        /// <summary>
        /// Checks the is in tick invariant when the corresponding safety checks are enabled.
        /// </summary>
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

        /// <summary>
        /// Checks the is not in tick invariant when the corresponding safety checks are enabled.
        /// </summary>
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