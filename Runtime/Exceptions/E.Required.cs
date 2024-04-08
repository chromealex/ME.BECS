namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;

    public partial class E {

        public class RequiredComponentException : System.Exception {

            public RequiredComponentException(string str) : base(str) { }

            [HIDE_CALLSTACK]
            public static void Throw<T>(in Ent ent) where T : unmanaged, IComponent {
                ThrowNotBurst<T>(in ent);
                throw new RequiredComponentException("Entity has no component, but it is required");
            }

            [BURST_DISCARD]
            [HIDE_CALLSTACK]
            private static void ThrowNotBurst<T>(in Ent ent) => throw new RequiredComponentException(Exception.Format($"{ent.ToString()} has no component {typeof(T)}, but it is required"));

        }

    }
    
    public static partial class E {

        [Conditional(COND.EXCEPTIONS)]
        [HIDE_CALLSTACK]
        public static void REQUIRED<T>(in Ent ent) where T : unmanaged, IComponent {
            
            if (ent.Has<T>(checkEnabled: false) == true) return;
            RequiredComponentException.Throw<T>(in ent);
            
        }

    }

}