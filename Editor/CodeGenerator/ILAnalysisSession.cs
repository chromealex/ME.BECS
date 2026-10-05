namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using ME.BECS.Mono.Reflection;

    // Export scopes reuse immutable IL/results while the compiled code is unchanged.
    // Default scopes stay isolated for diagnostics/tests. A domain reload also drops
    // the retained MethodBase identities; they are never serialized to disk.
    internal sealed class ILAnalysisSession : IDisposable {
        [ThreadStatic] private static ILAnalysisSession current;
        private static string retainedFingerprint;
        private static Dictionary<object, object> retainedValues;
        private readonly ILAnalysisSession previous;
        private readonly Dictionary<object, object> values;
        private readonly string codeFingerprint;
        private readonly bool rebuild;
        private sealed class Entry {
            internal object result;
            internal MethodBase[] dependencies;
        }

        internal ILAnalysisSession() {
            this.values = new Dictionary<object, object>();
            this.previous = current;
            current = this;
        }

        internal ILAnalysisSession(string codeFingerprint, bool rebuild) {
            if (string.IsNullOrEmpty(codeFingerprint)) throw new ArgumentException("Compiled code fingerprint is required.", nameof(codeFingerprint));
            if (rebuild || retainedFingerprint != codeFingerprint || retainedValues == null) {
                retainedFingerprint = codeFingerprint;
                retainedValues = new Dictionary<object, object>();
            }
            this.values = retainedValues;
            this.codeFingerprint = codeFingerprint;
            this.rebuild = rebuild;
            this.previous = current;
            current = this;
        }

        internal static string CodeFingerprint => current?.codeFingerprint;
        internal static bool Rebuild => current?.rebuild == true;

        internal static T Get<T>(object key, Func<T> create) {
            var session = current;
            if (session == null) return create();
            if (session.values.TryGetValue(key, out var value)) {
                var cached = (Entry)value;
                ILDependencyCapture.Observe(cached.dependencies);
                return (T)cached.result;
            }
            using var capture = new ILDependencyCapture();
            var result = create();
            session.values.Add(key, new Entry { result = result, dependencies = capture.Methods });
            return result;
        }

        // Readers must not mutate Instruction/branch objects. Analyses that need
        // their own work list copy the array and store flow facts separately.
        internal static Instruction[] Instructions(MethodBase method) {
            ILDependencyCapture.Observe(method);
            return Get((typeof(Instruction), method), () => method.GetInstructions().ToArray());
        }

        public void Dispose() {
            current = this.previous;
            if (this.codeFingerprint == null) this.values.Clear();
        }
    }
}
