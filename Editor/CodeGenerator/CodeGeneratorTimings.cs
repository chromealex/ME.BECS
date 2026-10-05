namespace ME.BECS.Editor {
    internal sealed class CodeGeneratorTimings : System.IDisposable {
        [System.ThreadStatic] private static CodeGeneratorTimings current;
        private readonly CodeGeneratorTimings previousScope;
        private readonly string target;
        private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        private readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        private double previous;
        private double nextProgress;
        private string stage;
        private string subject;
        private float progress;
        private bool cancellable = true;

        internal CodeGeneratorTimings(bool editor) {
            this.target = editor ? "Editor" : "Runtime";
            this.previousScope = current;
            current = this;
        }

        internal static void Stage(string name, float progress, bool cancellable = true) {
            var scope = current;
            if (scope == null) return;
            if (scope.stage != null) scope.Mark(scope.stage);
            scope.stage = name;
            scope.subject = null;
            scope.progress = progress;
            scope.cancellable = cancellable;
            scope.nextProgress = 0d;
            Work(null);
        }

        internal static void Subject(string name) {
            if (current == null) return;
            current.subject = name;
            Work(null);
        }

        internal static void Work(System.Reflection.MethodBase method) {
            var scope = current;
            if (scope == null || scope.watch.Elapsed.TotalMilliseconds < scope.nextProgress) return;
            var elapsed = scope.watch.Elapsed.TotalSeconds;
            scope.nextProgress = scope.watch.Elapsed.TotalMilliseconds + 150d;
            var detail = scope.target + ": " + scope.stage + " (" + elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s)";
            if (scope.subject != null) detail += "\n" + scope.subject;
            if (method != null) detail += "\n" + method.DeclaringType?.FullName + "." + method.Name;
            if (!scope.cancellable) {
                UnityEditor.EditorUtility.DisplayProgressBar(CodeGenerator.PROGRESS_BAR_CAPTION, detail, scope.progress);
                return;
            }
            if (UnityEditor.EditorUtility.DisplayCancelableProgressBar(CodeGenerator.PROGRESS_BAR_CAPTION, detail, scope.progress))
                throw new System.OperationCanceledException("BECS input export cancelled: " + detail);
        }

        internal void Mark(string stage) {
            var now = this.watch.Elapsed.TotalMilliseconds;
            this.report.Append("  ").Append(stage).Append(": ")
                .Append((now - this.previous).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" ms");
            this.previous = now;
        }

        public void Dispose() {
            this.Mark(this.stage ?? "remaining / asmdef");
            this.watch.Stop();
            current = this.previousScope;
            UnityEditor.EditorUtility.ClearProgressBar();
            UnityEngine.Debug.Log("[ME.BECS] Codegen timings (" + this.target + "): " +
                this.watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " ms elapsed\n" + this.report + "Includes synchronous cache/asset operations; excludes subsequent Unity compilation.");
        }
    }
}
