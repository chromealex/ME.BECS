namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class OutOfRangeException : System.Exception {

            public OutOfRangeException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(int index, int startIndex, int count) {
                ThrowNotBurst(index, startIndex, count);
                throw new OutOfRangeException("Out of range");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(int index, int startIndex, int count) => throw new OutOfRangeException(Exception.Format($"index {index} out of range [{startIndex}..{count - 1}]"));

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void RANGE(in int index, int startIndex, in int length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw(index, startIndex, length);
            
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void RANGE(in int index, uint startIndex, in uint length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw(index, (int)startIndex, (int)length);
            
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void RANGE(in uint index, uint startIndex, in uint length) {
            
            if (index >= startIndex && index < length) return;
            OutOfRangeException.Throw((int)index, (int)startIndex, (int)length);
            
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static unsafe void RANGE(byte* position, byte* low, byte* high) {
            
            if (position >= low && position < high) return;
            OutOfRangeException.Throw((int)(high - position), 0, (int)(high - low));
            
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void RANGE_INVERSE(uint index, uint length) {
            
            if (index >= length) return;
            OutOfRangeException.Throw((int)index, 0, (int)length);
            
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void OUT_OF_RANGE() {
            OutOfRangeException.Throw(0, 0, 0);
        }

        [Conditional(COND.EXCEPTIONS_COLLECTIONS)]
        [HIDE_CALLSTACK]
        public static void OUT_OF_RANGE(int index, int startIndex, int count) {
            OutOfRangeException.Throw(index, startIndex, count);
        }

    }

}