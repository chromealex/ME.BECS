namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public static class Utils {

        [INLINE(256)]
        public static int Hash(uint v1, uint v2) {
            int hash = 23;
            hash = hash * 31 + v1.GetHashCode();
            hash = hash * 31 + v2.GetHashCode();
            return hash;
        }

        [INLINE(256)]
        public static int Hash(uint v1) {
            int hash = 23;
            hash = hash * 31 + v1.GetHashCode();
            return hash;
        }

        [INLINE(256)]
        public static int Hash(int v1, int v2, int v3, ulong v4) {
            int hash = 23;
            hash = hash * 31 + v1;
            hash = hash * 31 + v2;
            hash = hash * 31 + v3;
            hash = hash * 31 + v4.GetHashCode();
            return hash;
        }

    }

}