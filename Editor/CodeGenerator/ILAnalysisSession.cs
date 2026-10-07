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
        private Dictionary<object, object> values;
        private readonly string codeFingerprint;
        private readonly bool rebuild;
        private readonly bool isolated;
        private readonly System.Threading.CancellationToken cancellation;
        private readonly Action<MethodBase> progress;
        private readonly Func<Func<object>, object> readMetadata;
        private bool disposed;
        private sealed class Entry {
            internal object result;
            internal MethodBase[] dependencies;
        }

        internal ILAnalysisSession() {
            this.isolated = true;
            this.values = new Dictionary<object, object>();
            this.previous = current;
            current = this;
        }

        // A worker never borrows the Editor's retained mutable dictionary. The
        // completed memo is handed over once, after the worker Task has ended.
        internal sealed class Snapshot {
            private Dictionary<object, object> values;
            internal Snapshot(Dictionary<object, object> values) { this.values = values; }
            internal Dictionary<object, object> Take() => System.Threading.Interlocked.Exchange(ref this.values, null)
                ?? throw new InvalidOperationException("IL snapshot has already been consumed.");
        }

        internal ILAnalysisSession(System.Threading.CancellationToken cancellation, Action<MethodBase> progress) :
            this(cancellation, progress, null) { }

        internal ILAnalysisSession(System.Threading.CancellationToken cancellation, Action<MethodBase> progress,
            Func<Func<object>, object> readMetadata) : this() {
            this.cancellation = cancellation;
            this.progress = progress;
            this.readMetadata = readMetadata;
        }

        internal ILAnalysisSession(string codeFingerprint, bool rebuild, Snapshot snapshot) {
            if (string.IsNullOrEmpty(codeFingerprint)) throw new ArgumentException("Compiled code fingerprint is required.", nameof(codeFingerprint));
            this.values = snapshot.Take();
            this.isolated = true;
            this.codeFingerprint = codeFingerprint;
            this.rebuild = rebuild;
            this.previous = current;
            current = this;
        }

        internal Snapshot Detach() {
            if (!this.isolated || current != this || this.disposed || this.values == null)
                throw new InvalidOperationException("Only the active isolated IL session can transfer its memo.");
            this.cancellation.ThrowIfCancellationRequested();
            var snapshot = new Snapshot(this.values);
            this.values = null;
            return snapshot;
        }

        internal void MergeWorker(Snapshot snapshot) {
            if (current != this || this.disposed || this.values == null)
                throw new InvalidOperationException("Worker memo requires an active receiving IL session.");
            foreach (var pair in snapshot.Take())
                if (!this.values.ContainsKey(pair.Key)) this.values.Add(pair.Key, pair.Value);
        }

        internal static void Checkpoint(MethodBase method = null) {
            current?.cancellation.ThrowIfCancellationRequested();
            current?.progress?.Invoke(method);
        }

        // IRefOp is extensible: its constructor/getter may call Unity APIs.
        // The worker requests these values from the Editor; it never executes
        // arbitrary user metadata callbacks itself.
        internal static T ReadMetadata<T>(Func<T> read) => current?.readMetadata == null ? read() :
            (T)current.readMetadata(() => read());

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

        internal static string MemorySummary() {
            var values = current?.values;
            long dependencies = 0;
            if (values != null) foreach (Entry entry in values.Values) dependencies += entry.dependencies.Length;
            // This is process-wide managed heap usage, not an attribution to BECS
            // or a forced collection. Dependency slots include repeated references.
            return "managed heap=" + (GC.GetTotalMemory(false) / (1024L * 1024L)) +
                " MiB; IL memo entries=" + (values?.Count ?? 0) + "; dependency reference slots=" + dependencies;
        }

        internal static string[] IndexDeclarations(Assembly[] assemblies) {
            Checkpoint();
            var parent = current;
            var results = new string[assemblies.Length];
            // Only the background export has a live main-thread metadata pump.
            // Synchronous diagnostics must never block that pump with Parallel.For.
            if (assemblies.Length < 2 || parent?.readMetadata == null) {
                for (var index = 0; index < assemblies.Length; ++index)
                    results[index] = ILContentFingerprint.Declarations(assemblies[index]);
                return results;
            }
            var cancellation = parent.cancellation;
            var metadata = parent.readMetadata;
            System.Threading.Tasks.Parallel.For(0, assemblies.Length, new System.Threading.Tasks.ParallelOptions {
                CancellationToken = cancellation,
                MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)),
            }, index => {
                using var isolated = new ILAnalysisSession(cancellation, null, metadata);
                results[index] = ILContentFingerprint.Declarations(assemblies[index]);
            });
            cancellation.ThrowIfCancellationRequested();
            return results;
        }

        internal static T Get<T>(object key, Func<T> create) {
            Checkpoint();
            var session = current;
            if (session == null) return create();
            if (session.values == null) throw new InvalidOperationException("IL memo has already been transferred.");
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
            if (this.disposed) return;
            if (current != this) throw new InvalidOperationException("IL sessions must be disposed on their owning thread in reverse order.");
            this.disposed = true;
            current = this.previous;
            // The export's publication session received the complete background memo.
            // Keep it as the Editor's retained memo for this compiled code, so the
            // next synchronous analysis in this domain (tests, build preflight, a
            // manual export) reuses it instead of re-validating every summary.
            if (this.isolated && this.codeFingerprint != null && this.values != null && this.values.Count > 0 &&
                UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread()) {
                if (retainedFingerprint == this.codeFingerprint && retainedValues != null) {
                    // Entries the Editor already retained win (callers may hold their
                    // results by reference); only add what the export computed.
                    foreach (var pair in this.values)
                        if (!retainedValues.ContainsKey(pair.Key)) retainedValues.Add(pair.Key, pair.Value);
                    this.values.Clear();
                } else {
                    retainedFingerprint = this.codeFingerprint;
                    retainedValues = this.values;
                }
                this.values = null;
                return;
            }
            if (this.isolated) this.values?.Clear();
        }
    }
}
