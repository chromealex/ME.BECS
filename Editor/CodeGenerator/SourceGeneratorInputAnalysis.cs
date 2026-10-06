namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Discovery = Systems.SystemDependenciesCodeGenerator;

    // One worker owns all mutable IL state. Only completed results cross back to
    // the Editor thread; neither native progress nor publication runs here.
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
        private long nextReport;
        private long stageStarted;
        private readonly ILMainThreadMetadata mainThread;

        internal bool IsCompleted => this.task.IsCompleted;
        internal string Description => Volatile.Read(ref this.detail);
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
            var environment = ILAnalysisEnvironment.Capture();
            if (fingerprint != SourceGeneratorGraphSnapshot.GetCurrent())
                throw new InvalidOperationException("Input assets changed while capturing the IL analysis snapshot. Retry export.");
            this.task = System.Threading.Tasks.Task.Run(() => this.Analyze(fingerprint, codeFingerprint, runtime, editor, assemblies, environment, rebuild));
        }

        private void Stage(string name, float value) {
            this.cancellation.Token.ThrowIfCancellationRequested();
            var now = this.watch.ElapsedMilliseconds;
            if (this.stageStarted != 0)
                UnityEngine.Debug.Log("[ME.BECS] Background IL stage: " + this.stage + " — " + (now - this.stageStarted) + " ms.");
            this.stageStarted = now;
            this.stage = name;
            Volatile.Write(ref this.detail, name);
            Volatile.Write(ref this.progress, value);
        }

        private void Report(MethodBase method) {
            if (method == null || this.watch.ElapsedMilliseconds < this.nextReport) return;
            this.nextReport = this.watch.ElapsedMilliseconds + 150;
            Volatile.Write(ref this.detail, this.stage + "\n" + method.DeclaringType?.FullName + "." + method.Name);
        }

        private Result Analyze(string fingerprint, string codeFingerprint, Discovery.RuntimeDiscoveryInputs roots,
            Discovery.UsedObjects editor, List<AssemblyInfo> assemblies, ILAnalysisEnvironment environment, bool rebuild) {
            using var lookup = SourceGeneratorBridge.BeginLookupScope();
            using var analysis = new ILAnalysisSession(this.cancellation.Token, this.Report, this.mainThread.Read);
            using var persistent = new ILPersistentAnalysis(rebuild, environment);
            this.Stage("Discover Runtime types (IL)", 0.02f);
            var runtime = Discovery.AnalyzeRuntimeDiscovery(roots);
            var preparedJobs = new HashSet<Type>();
            foreach (var profile in new[] { false, true }) {
                var used = profile ? editor : runtime;
                this.Stage((profile ? "Editor" : "Runtime") + " job safety, entities and weights (IL)", profile ? 0.6f : 0.35f);
                new Jobs.JobsEarlyInitCodeGenerator {
                    editorAssembly = profile, asms = assemblies, jobTypes = new List<Type>(used.jobTypes),
                }.PrepareAnalysis(preparedJobs);
            }
            this.Stage("Editor system dependencies (IL)", 0.8f);
            var systems = new List<Type>(editor.systems);
            CodeGenerator.PatchSystemsList(systems);
            systems.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(true, type, assemblies, true));
            new Discovery { systems = systems }.PrepareAnalysis();
            this.Stage("Save IL summaries", 0.98f);
            return new Result { fingerprint = fingerprint, codeFingerprint = codeFingerprint, runtime = runtime, editor = editor,
                memo = analysis.Detach(), persistent = persistent.CaptureSnapshot(), rebuild = rebuild };
        }

        internal Result TakeResult() {
            if (!this.IsCompleted) throw new InvalidOperationException("IL analysis has not completed; never wait on the Editor thread.");
            return this.task.GetAwaiter().GetResult();
        }

        public void Dispose() {
            if (!this.IsCompleted) throw new InvalidOperationException("Cancel and poll IL analysis before disposing it.");
            this.cancellation.Dispose();
        }
    }
}
