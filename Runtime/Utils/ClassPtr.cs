namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [IgnoreProfiler]
    public unsafe struct ClassPtr<T> : System.IEquatable<ClassPtr<T>> where T : class {

        [NativeDisableUnsafePtrRestriction]
        private System.IntPtr ptr;
        [NativeDisableUnsafePtrRestriction]
        private System.Runtime.InteropServices.GCHandle gcHandle;

        public bool IsValid => this.ptr.ToPointer() != null;

        public T Value {
            get {
                if (this.gcHandle.IsAllocated == false) return null;
                return (T)this.gcHandle.Target;
            }
        }

        [INLINE(256)]
        public ClassPtr(T data) {
            this.gcHandle = (data != null ? System.Runtime.InteropServices.GCHandle.Alloc(data, System.Runtime.InteropServices.GCHandleType.Normal) : default);
            this.ptr = System.Runtime.InteropServices.GCHandle.ToIntPtr(this.gcHandle);
        }

        [INLINE(256)]
        public void Dispose() {
            if (this.gcHandle.IsAllocated == true) {
                this.gcHandle.Free();
            }
            this = default;
        }

        [INLINE(256)]
        public bool Equals(ClassPtr<T> other) {
            return other.ptr == ptr;
        }
    }

}