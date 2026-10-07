namespace ME.BECS.Editor {
    internal sealed class CodeGeneratorTimings : System.IDisposable {
        [System.ThreadStatic] private static CodeGeneratorTimings current;
        private readonly CodeGeneratorTimings previousScope;
        private readonly string target;
        private readonly int progressId;
        // Set by the background export: report into its single bar, mapped to
        // this profile's range, instead of starting a separate progress item.
        // Progress id + 1 (0 = none; ThreadStatic fields cannot have initializers).
        [System.ThreadStatic] internal static int outerProgress;
        private readonly bool ownsProgress;
        private readonly float rangeStart, rangeEnd = 1f;
        private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        private readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        private double previous;
        private double nextProgress;
        private string stage;
        private string subject;
        private float progress;
        private bool cancellable = true;
        private bool cancelRequested;
        private bool disposed;
        private UnityEditor.Progress.Status status = UnityEditor.Progress.Status.Failed;

        internal CodeGeneratorTimings(bool editor) {
            this.target = editor ? "Editor" : "Runtime";
            this.previousScope = current;
            // Native background-task/status-bar UI, not a modal progress window.
            // Reporting is synchronous because asset export still runs on Unity's
            // main thread; this option does not move AssetDatabase work to a worker.
            var outer = outerProgress - 1;
            if (outer >= 0 && UnityEditor.Progress.Exists(outer)) {
                this.progressId = outer;
                this.rangeStart = editor ? SourceGeneratorInputRefresh.RuntimeEnd : SourceGeneratorInputRefresh.AnalysisEnd;
                this.rangeEnd = editor ? SourceGeneratorInputRefresh.EditorEnd : SourceGeneratorInputRefresh.RuntimeEnd;
            } else {
                this.ownsProgress = true;
                this.progressId = UnityEngine.Application.isBatchMode ? -1 : UnityEditor.Progress.Start(
                    "ME.BECS source inputs: " + this.target, "Preparing input export",
                    UnityEditor.Progress.Options.Managed | UnityEditor.Progress.Options.Synchronous);
            }
            if (this.progressId >= 0) UnityEditor.Progress.RegisterCancelCallback(this.progressId, this.RequestCancel);
            current = this;
        }

        private bool RequestCancel() {
            if (this.disposed || !this.cancellable) return false;
            this.cancelRequested = true;
            this.status = UnityEditor.Progress.Status.Canceled;
            return true;
        }

        private void ThrowIfCancelled() {
            if (this.cancelRequested) throw new System.OperationCanceledException("BECS input export cancelled: " + this.target + " / " + this.stage);
        }

        internal void Complete() { this.status = UnityEditor.Progress.Status.Succeeded; }
        internal void Cancelled() { this.status = UnityEditor.Progress.Status.Canceled; }

        internal static void Stage(string name, float progress, bool cancellable = true) {
            var scope = current;
            if (scope == null) return;
            scope.ThrowIfCancelled();
            if (scope.stage != null) scope.Mark(scope.stage);
            scope.stage = name;
            scope.subject = null;
            scope.progress = progress;
            if (scope.cancellable != cancellable) {
                scope.cancellable = cancellable;
                if (scope.progressId >= 0 && UnityEditor.Progress.Exists(scope.progressId)) {
                    if (cancellable) UnityEditor.Progress.RegisterCancelCallback(scope.progressId, scope.RequestCancel);
                    else UnityEditor.Progress.UnregisterCancelCallback(scope.progressId);
                }
            }
            scope.nextProgress = 0d;
            Work(null);
        }

        internal static void Subject(string name) {
            if (current == null) return;
            current.subject = name;
            Work(null);
        }

        internal static void Work(System.Reflection.MethodBase method) {
            ILAnalysisSession.Checkpoint(method);
            var scope = current;
            if (scope == null) return;
            scope.ThrowIfCancelled();
            if (scope.watch.Elapsed.TotalMilliseconds < scope.nextProgress) return;
            var elapsed = scope.watch.Elapsed.TotalSeconds;
            scope.nextProgress = scope.watch.Elapsed.TotalMilliseconds + 150d;
            var detail = scope.target + ": " + scope.stage + " (" + elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s)";
            if (scope.subject != null) detail += "\n" + scope.subject;
            if (method != null) detail += "\n" + method.DeclaringType?.FullName + "." + method.Name;
            if (scope.progressId >= 0 && UnityEditor.Progress.Exists(scope.progressId))
                UnityEditor.Progress.Report(scope.progressId, scope.rangeStart + (scope.rangeEnd - scope.rangeStart) * scope.progress, detail);
            // A synchronous UI update may deliver the cancel request above.
            scope.ThrowIfCancelled();
        }

        // Accumulated sub-phase time inside the current stages (diagnostics only).
        private readonly System.Collections.Generic.Dictionary<string, (double ms, int count)> details =
            new System.Collections.Generic.Dictionary<string, (double ms, int count)>(System.StringComparer.Ordinal);

        internal readonly struct Measurement : System.IDisposable {
            private readonly CodeGeneratorTimings scope;
            private readonly string name;
            private readonly long start;
            internal Measurement(CodeGeneratorTimings scope, string name) {
                this.scope = scope; this.name = name;
                this.start = scope == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            }
            public void Dispose() {
                if (this.scope == null) return;
                var ms = (System.Diagnostics.Stopwatch.GetTimestamp() - this.start) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                this.scope.details.TryGetValue(this.name, out var value);
                this.scope.details[this.name] = (value.ms + ms, value.count + 1);
            }
        }

        internal static Measurement Measure(string name) => new Measurement(current, name);

        // The sliced background publication yields to the Editor between stages.
        // Idle frames are excluded from stage and total times.
        private double suspendedAt = -1d, suspendedTotal;

        internal static void SuspendAll() {
            for (var scope = current; scope != null; scope = scope.previousScope)
                if (scope.suspendedAt < 0d) scope.suspendedAt = scope.watch.Elapsed.TotalMilliseconds;
        }

        internal static void ResumeAll() {
            for (var scope = current; scope != null; scope = scope.previousScope) {
                if (scope.suspendedAt < 0d) continue;
                var gap = scope.watch.Elapsed.TotalMilliseconds - scope.suspendedAt;
                scope.previous += gap;
                scope.suspendedTotal += gap;
                scope.suspendedAt = -1d;
            }
        }

        internal void Mark(string stage) {
            var now = this.watch.Elapsed.TotalMilliseconds;
            this.report.Append("  ").Append(stage).Append(": ")
                .Append((now - this.previous).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" ms");
            this.previous = now;
        }

        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            this.Mark(this.stage ?? "remaining / asmdef");
            this.watch.Stop();
            current = this.previousScope;
            if (!this.ownsProgress) {
                // The export owns the shared bar; only drop our cancel hook.
                if (UnityEditor.Progress.Exists(this.progressId)) UnityEditor.Progress.UnregisterCancelCallback(this.progressId);
            } else if (this.progressId >= 0 && UnityEditor.Progress.Exists(this.progressId) &&
                UnityEditor.Progress.GetStatus(this.progressId) == UnityEditor.Progress.Status.Running)
                UnityEditor.Progress.Finish(this.progressId, this.status);
            UnityEngine.Debug.Log("[ME.BECS] Codegen timings (" + this.target + "): " +
                (this.watch.Elapsed.TotalMilliseconds - this.suspendedTotal).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " ms elapsed\n" + this.report + string.Concat(System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.OrderByDescending(this.details, pair => pair.Value.ms),
                    pair => "  · " + pair.Key + ": " + pair.Value.ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                        " ms (" + pair.Value.count + ")\n")) + "Includes synchronous cache/asset operations; excludes subsequent Unity compilation.");
        }
    }
}
