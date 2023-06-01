namespace ME.BECS {

    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class TypeNotFoundException : System.Exception {

            public TypeNotFoundException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw(string str) {
                ThrowNotBurst(str);
                throw new TypeNotFoundException("Type not found in types list. Select `ME.BECS/Regenerate Assemblies`.");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst(string str) => throw new TypeNotFoundException(Exception.Format(str));

        }

    }

    public partial class E {
        
        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void TYPE_NOT_FOUND_IN_MANAGED_TYPES() {
            E.TypeNotFoundException.Throw("Type not found in types list. Select `ME.BECS/Regenerate Assemblies`.");
        }

    }
    
}