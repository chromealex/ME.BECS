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
        internal MethodBase[] Methods => this.methods == null ? Array.Empty<MethodBase>() : new List<MethodBase>(this.methods).ToArray();

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
            Observe(this.methods);
        }
    }
}
