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
        /// Reports a violation of the out of range invariant.
        /// </summary>
        public class OutOfRangeException : System.Exception {

            /// <summary>
            /// Initializes <c>OutOfRangeException</c> from the supplied str.
            /// </summary>
            public OutOfRangeException(string str) : base(str) { }

            /// <summary>
            /// Throws the diagnostic exception represented by <c>E.OutOfRangeException</c>.
            /// </summary>
            [HIDE_CALLSTACK][IgnoreProfiler]
            public static void Throw(int index, int startIndex, int count) {
                ThrowNotBurst(index, startIndex, count);
                throw new OutOfRangeException("Out of range");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK][IgnoreProfiler]
            private static void ThrowNotBurst(int index, int startIndex, int count) => throw new OutOfRangeException(Exception.Format($"index {index} out of range [{startIndex}..{count - 1}]"));

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Checks the range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void RANGE(in int index, int startIndex, in int length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw(index, startIndex, length);
            
        }

        /// <summary>
        /// Checks the range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void RANGE(in int index, uint startIndex, in uint length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw(index, (int)startIndex, (int)length);
            
        }

        /// <summary>
        /// Checks the range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void RANGE(in uint index, uint startIndex, in uint length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw((int)index, (int)startIndex, (int)length);
            
        }

        /// <summary>
        /// Checks the range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static unsafe void RANGE(byte* position, byte* low, byte* high) {
            
            if (position >= low && position < high) return;
            OutOfRangeException.Throw((int)(high - position), 0, (int)(high - low));
            
        }

        /// <summary>
        /// Checks the range inverse invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void RANGE_INVERSE(uint index, uint length) {
            
            if (index >= length) return;
            OutOfRangeException.Throw((int)index, 0, (int)length);
            
        }

        /// <summary>
        /// Checks the out of range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void OUT_OF_RANGE() {
            OutOfRangeException.Throw(0, 0, 0);
        }

        /// <summary>
        /// Checks the out of range invariant when the corresponding safety checks are enabled.
        /// </summary>
        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK][IgnoreProfiler]
        public static void OUT_OF_RANGE(int index, int startIndex, int count) {
            OutOfRangeException.Throw(index, startIndex, count);
        }

    }

}