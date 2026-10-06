namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    // A memo hit must propagate the same dependency set as the original analysis.
    // Otherwise an outer persisted result could silently forget a shared helper.
    internal sealed class ILDependencyCapture : IDisposable {
        [ThreadStatic] private static ILDependencyCapture current;
        private readonly ILDependencyCapture previous;
        private HashSet<MethodBase> methods;
        internal MethodBase[] Methods {
            get {
                if (this.methods == null || this.methods.Count == 0) return Array.Empty<MethodBase>();
                var result = new MethodBase[this.methods.Count];
                this.methods.CopyTo(result);
                return result;
            }
        }

        internal ILDependencyCapture() { this.previous = current; current = this; }
        internal static void Observe(MethodBase method) {
            if (current == null || method == null) return;
            (current.methods ??= new HashSet<MethodBase>()).Add(method);
        }
        internal static void Observe(IEnumerable<MethodBase> methods) {
            if (current == null || methods == null) return;
            foreach (var method in methods) Observe(method);
        }
        public void Dispose() {
            current = this.previous;
            if (current == null || this.methods == null || this.methods.Count == 0) return;
            // Propagate all dependencies without an interface enumerator and a
            // thread-static lookup for every method. Do not share mutable sets.
            if (current.methods == null) current.methods = new HashSet<MethodBase>(this.methods);
            else current.methods.UnionWith(this.methods);
        }
    }
}
