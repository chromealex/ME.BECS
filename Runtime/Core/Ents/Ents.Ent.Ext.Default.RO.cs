namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static partial class EntExt {

        [INLINE(256)]
        public static bool IsAlive(in this EntRO ent) => ent.ent.IsAlive();

        [INLINE(256)]
        public static bool IsEmpty(in this EntRO ent) => ent.ent.IsEmpty();

    }

}