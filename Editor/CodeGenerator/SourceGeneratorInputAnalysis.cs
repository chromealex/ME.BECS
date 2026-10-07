namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Discovery = Systems.SystemDependenciesCodeGenerator;

    // Each worker owns its mutable IL state. The coordinator merges completed
    // snapshots before returning to the Editor; publication never runs here.
    internal sealed class SourceGeneratorInputAnalysis : IDisposable {
        internal sealed class Result {
            internal string fingerprint, codeFingerprint;
            internal Discovery.UsedObjects runtime, editor;
            internal ILAnalysisSession.Snapshot memo;
            internal ILPersistentAnalysis.Snapshot persistent;
            internal bool rebuild;
        }

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        private readonly Task<Result> task;
        private string stage = "Preparing analysis", detail = "Preparing analysis";
        private float progress;
        private long workCounts;
        private long systemCounts;
        private bool waitingForSystems, systemsRunning;
        internal long WorkCounts => Volatile.Read(ref this.waitingForSystems)
            ? Interlocked.Read(ref this.systemCounts) : Interlocked.Read(ref this.workCounts);
        private long nextReport;
        private long stageStarted;
        private readonly ILMainThreadMetadata mainThread;

        internal bool IsCompleted => this.task.IsCompleted;
        internal string Description {
            get {
                var text = Volatile.Read(ref this.detail);
                if (!Volatile.Read(ref this.systemsRunning)) return text;
                var counts = Interlocked.Read(ref this.systemCounts);
                return text + "\nParallel Editor systems: " + (uint)counts + "/" + (counts >> 32);
            }
        }
        internal float Progress => Volatile.Read(ref this.progress);
        internal double ElapsedSeconds => this.watch.Elapsed.TotalSeconds;
        internal void Cancel() => this.cancellation.Cancel();

        internal void PumpMainThread() => this.mainThread.Pump(4);

        // Called on the Editor thread, before launching the worker.
        internal SourceGeneratorInputAnalysis(string fingerprint, bool rebuild) {
            this.mainThread = new ILMainThreadMetadata(this.cancellation.Token);
            var codeFingerprint = SourceGeneratorGraphSnapshot.GetCodeFingerprint();
            var runtime = Discovery.CaptureRuntimeDiscovery();
            Discovery.GetUsedObjects(true, out var editor);
            var assemblies = EditorUtils.GetAssembliesInfo().Select(assembly => new AssemblyInfo {
                name = assembly.name, isEditor = assembly.isEditor,
                includePlatforms = assembly.includePlatforms == null ? null : (string[])assembly.includePlatforms.Clone(),
                references = assembly.references == null ? null : (string[])assembly.references.Clone(),
            }).ToList();
            // Generic expansion can query UnityEditor.TypeCache. Capture the
            // concrete selection on the Editor thread, before either IL worker.
            var systems = new List<Type>(editor.systems);
            CodeGenerator.PatchSystemsList(systems);
            systems.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(true, type, assemblies, true));
            var environment = ILAnalysisEnvironment.Capture();
            if (fingerprint != SourceGeneratorGraphSnapshot.GetCurrent())
                throw new InvalidOperationException("Input assets changed while capturing the IL analysis snapshot. Retry export.");
            this.task = System.Threading.Tasks.Task.Run(() => this.Analyze(fingerprint, codeFingerprint, runtime, editor, assemblies, systems, environment, rebuild));
        }

        private void Stage(string name, float value) {
            this.cancellation.Token.ThrowIfCancellationRequested();
            var now = this.watch.ElapsedMilliseconds;
            if (this.stageStarted != 0)
                UnityEngine.Debug.Log("[ME.BECS] Background IL stage: " + this.stage + " — " + (now - this.stageStarted) +
                    " ms. " + ILAnalysisSession.MemorySummary());
            else UnityEngine.Debug.Log("[ME.BECS] Background IL starting: " + ILAnalysisSession.MemorySummary());
            this.stageStarted = now;
            this.stage = name;
            Interlocked.Exchange(ref this.workCounts, 0);
            Volatile.Write(ref this.detail, name);
            Volatile.Write(ref this.progress, value);
        }

        private void Report(MethodBase method) {
            if (method == null || this.watch.ElapsedMilliseconds < this.nextReport) return;
            this.nextReport = this.watch.ElapsedMilliseconds + 150;
            Volatile.Write(ref this.detail, this.stage + "\n" + method.DeclaringType?.FullName + "." + method.Name);
        }

        private void ReportUnits(int completed, int total, float start, float end) {
            this.cancellation.Token.ThrowIfCancellationRequested();
            var fraction = total == 0 ? 1f : Math.Min(1f, (float)completed / total);
            Interlocked.Exchange(ref this.workCounts, ((long)total << 32) | (uint)completed);
            Volatile.Write(ref this.progress, start + (end - start) * fraction);
            Volatile.Write(ref this.detail, this.stage + "\nCompleted work items: " + completed + "/" + total + " (not a time estimate)");
        }

        private Result Analyze(string fingerprint, string codeFingerprint, Discovery.RuntimeDiscoveryInputs roots,
            Discovery.UsedObjects editor, List<AssemblyInfo> assemblies, List<Type> systems, ILAnalysisEnvironment environment, bool rebuild) {
            using var lookup = SourceGeneratorBridge.BeginLookupScope();
            using var analysis = new ILAnalysisSession(this.cancellation.Token, this.Report, this.mainThread.Read);
            using var persistent = new ILPersistentAnalysis(rebuild, environment);
            this.Stage("Prepare shared IL cache snapshot", 0.01f);
            var createWorker = persistent.PrepareWorker();
            Volatile.Write(ref this.systemsRunning, true);
            var systemsTask = Task.Run(() => {
                using var workerLookup = SourceGeneratorBridge.BeginLookupScope();
                using var workerMemo = new ILAnalysisSession(this.cancellation.Token, null, this.mainThread.Read);
                using var workerCache = createWorker();
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                try {
                    new Discovery { systems = systems, analysisProgress = (done, total) => {
                        this.cancellation.Token.ThrowIfCancellationRequested();
                        Interlocked.Exchange(ref this.systemCounts, ((long)total << 32) | (uint)done);
                    } }.PrepareAnalysis();
                    return (memo: workerMemo.Detach(), persistent: workerCache.CaptureSnapshot());
                } finally {
                    Volatile.Write(ref this.systemsRunning, false);
                    UnityEngine.Debug.Log("[ME.BECS] Parallel Editor system IL worker ended: " + elapsed.ElapsedMilliseconds + " ms.");
                }
            });
            try {
                this.Stage("Discover Runtime types (IL)", 0.02f);
                var runtime = Discovery.AnalyzeRuntimeDiscoveryWithProgress(roots, false,
                    (done, total) => this.ReportUnits(done, total, 0.02f, 0.35f));
                var preparedJobs = new HashSet<Type>();
                foreach (var profile in new[] { false, true }) {
                    var used = profile ? editor : runtime;
                    this.Stage((profile ? "Editor" : "Runtime") + " job safety, entities and weights (IL)", profile ? 0.6f : 0.35f);
                    new Jobs.JobsEarlyInitCodeGenerator {
                        editorAssembly = profile, asms = assemblies, jobTypes = new List<Type>(used.jobTypes),
                        analysisProgress = (done, total) => this.ReportUnits(done, total, profile ? 0.6f : 0.35f, profile ? 0.8f : 0.6f),
                    }.PrepareAnalysis(preparedJobs);
                }
                this.Stage("Join Editor system dependencies (IL)", 0.8f);
                Volatile.Write(ref this.waitingForSystems, true);
                // Only this background coordinator waits. The Editor keeps pumping
                // metadata requests until the entire coordinator task completes.
                var systemResult = systemsTask.GetAwaiter().GetResult();
                Volatile.Write(ref this.waitingForSystems, false);
                persistent.MergeWorker(systemResult.persistent);
                analysis.MergeWorker(systemResult.memo);
                // No cache write here: the Editor publication receives this snapshot
                // and saves the merged cache once, in the background.
                this.Stage("Hand over IL summaries", 0.98f);
                persistent.DeferPersistence();
                return new Result { fingerprint = fingerprint, codeFingerprint = codeFingerprint, runtime = runtime, editor = editor,
                    memo = analysis.Detach(), persistent = persistent.CaptureSnapshot(), rebuild = rebuild };
            } catch {
                this.cancellation.Cancel();
                // Observe/drain the child even if the coordinator failed first.
                // Cancellation releases queued metadata reads without a pump.
                try { systemsTask.GetAwaiter().GetResult(); } catch { }
                throw;
            } finally {
                Volatile.Write(ref this.waitingForSystems, false);
            }
        }

        internal Result TakeResult() {
            if (!this.IsCompleted) throw new InvalidOperationException("IL analysis has not completed; never wait on the Editor thread.");
            return this.task.GetAwaiter().GetResult();
        }

        public void Dispose() {
            if (!this.IsCompleted) throw new InvalidOperationException("Cancel and poll IL analysis before disposing it.");
            // Cancelled/superseded publication: keep the analyzed summaries anyway.
            if (this.task.Status == TaskStatus.RanToCompletion) this.task.Result.persistent?.PersistIfUnconsumed();
            this.cancellation.Dispose();
        }
    }
}
