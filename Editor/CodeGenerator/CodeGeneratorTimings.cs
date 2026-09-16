namespace ME.BECS.Editor {
    internal sealed class CodeGeneratorTimings : System.IDisposable {
        private readonly string target;
        private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        private readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        private double previous;

        internal CodeGeneratorTimings(bool editor) { this.target = editor ? "Editor" : "Runtime"; }

        internal void Mark(string stage) {
            var now = this.watch.Elapsed.TotalMilliseconds;
            this.report.Append("  ").Append(stage).Append(": ")
                .Append((now - this.previous).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" ms");
            this.previous = now;
        }

        public void Dispose() {
            this.Mark("remaining / asmdef");
            this.watch.Stop();
            UnityEngine.Debug.Log("[ME.BECS] Codegen timings (" + this.target + "): " +
                this.watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " ms elapsed\n" + this.report + "Includes synchronous cache/asset operations; excludes subsequent Unity compilation.");
        }
    }
}
