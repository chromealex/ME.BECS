namespace ME.BECS.Editor {
    // Coalesced input refresh. Successful export and successful compilation are
    // separate states; the project analysis receipt plus compiled input hashes
    // provide restart-safe evidence without injecting code hashes into inputs.
    [UnityEditor.InitializeOnLoad]
    public static class SourceGeneratorInputRefresh {
        private const string DeferredGraphsKey = "ME.BECS.GraphInputs.ManualGraphs";
        private const string PendingKey = "ME.BECS.GraphInputs.Pending";
        private const string FingerprintKey = "ME.BECS.GraphInputs.ExportedFingerprint";
        private const string FailedKey = "ME.BECS.GraphInputs.ExportIncomplete";
        private const string AutomaticAttemptKey = "ME.BECS.GraphInputs.AutomaticAttempt";
        private static double due;
        private static bool exporting;
        private static uint requestVersion;
        private static SourceGeneratorInputAnalysis background;
        private static System.Action<bool> backgroundCompleted;
        private static uint backgroundVersion;
        private static bool discardBackground, changedDuringAnalysis, reloadLocked, publishing;
        private static int backgroundProgress = -1;
        // One continuous bar for the whole export: background IL analysis, the
        // Runtime and Editor publication on the Editor thread, file writes and import.
        internal const float AnalysisEnd = 0.40f, RuntimeEnd = 0.60f, EditorEnd = 0.95f, WriteEnd = 0.98f;
        private static double nextBackgroundReport;
        private static CodeGenerator.PendingPublication pendingPublication;
        private static System.Threading.Tasks.Task publicationTask;
        private static bool publicationAssetEditing;
        private static int publicationWritten;
        private static long publicationWriteMilliseconds;
        internal static bool IsAnalyzing => background != null && !publishing;

        internal static bool HasUnfinishedExport => exporting ||
            UnityEditor.SessionState.GetBool(PendingKey, false) || UnityEditor.SessionState.GetBool(FailedKey, false);

        static SourceGeneratorInputRefresh() {
            UnityEditor.EditorApplication.update += Update;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CodeGenerator.ExportCompleted += OnExportCompleted;
            CodeGenerator.InputRefreshRequested += Request;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += _ => {
                if (IsAnalyzing) Request();
            };
            // A domain reload can still interrupt the export (the Test Runner and other
            // forced synchronous recompiles ignore LockReloadAssemblies). FinishBackground
            // never runs then, so the attempt stamp of this fingerprint would survive in
            // SessionState and ShouldExportAutomatically would refuse to retry the very
            // same, never-completed input until something else changed. Keep the failed
            // flag (inputs may be partial) but make the next domain retry.
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () => {
                if (background == null && publicationTask == null) return;
                UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
                UnityEditor.SessionState.SetBool(PendingKey, true);
                background?.Cancel();
            };
            UnityEditor.EditorApplication.quitting += () => {
                background?.Cancel();
                if (reloadLocked) { reloadLocked = false; UnityEditor.EditorApplication.UnlockReloadAssemblies(); }
            };
            // Recheck after reload/restart even when the old auto-codegen menu is
            // disabled. Current compiled inputs need no export or file writes.
            Request();
        }

        private static void OnExportCompleted(bool successful) {
            MarkExportStarted();
            if (successful) RecordSuccessfulExport();
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state) {
            if (state != UnityEditor.PlayModeStateChange.ExitingEditMode) return;
            if (TryValidateReady(out var reason)) return;
            UnityEditor.EditorApplication.isPlaying = false;
            if (!HasUnfinishedExport) Request();
            UnityEngine.Debug.LogWarning("[ME.BECS] Source inputs are pending, failed or not yet compiled. " + reason +
                " Wait for export/compilation, or retry Compile in the graph window, before entering Play Mode.");
        }

        // A queued freshness comparison is not an unfinished export. In batch
        // mode Update intentionally never runs, so startup Pending alone must not
        // veto an already compiled, current snapshot. Never export from this gate.
        internal static bool TryValidateReady(out string reason) {
            var version = requestVersion;
            // Local receipts are disposable and are never checked into the
            // framework. A fresh build machine may prove the published data by
            // read-only IL analysis; it may not publish/recompile during a build.
            if (UnityEngine.Application.isBatchMode && !exporting && !UnityEditor.SessionState.GetBool(FailedKey, false) &&
                !UnityEditor.EditorApplication.isCompiling && !UnityEditor.EditorApplication.isUpdating &&
                !SourceGeneratorAnalysisReceipt.IsCurrent(Fingerprint()) &&
                !SourceGeneratorInputManifest.TryAnalyzePublishedInputs(out reason)) return false;
            if (!CheckReady(exporting, UnityEditor.SessionState.GetBool(FailedKey, false), () => {
                    var current = SourceGeneratorGraphSnapshot.TryValidateCurrent(out var detail);
                    return (current, detail);
                }, out reason)) return false;
            if (version != requestVersion) {
                reason = "Input refresh was requested during the freshness check. Retry after imports settle.";
                return false;
            }
            UnityEditor.SessionState.SetBool(PendingKey, false);
            UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
            return true;
        }

        internal static bool CheckReady(bool exporting, bool failed,
            System.Func<(bool current, string reason)> readCompiledSnapshot, out string reason) {
            if (exporting || failed) {
                reason = exporting ? "Source input export is still running." :
                    "Source input export failed or was interrupted. Complete input export in the Editor before running the simulation or building.";
                return false;
            }
            var snapshot = readCompiledSnapshot();
            reason = snapshot.reason;
            return snapshot.current;
        }

        // Explicit retry for menus and graph UI; keep failure state even when the
        // exporter declines before raising ExportCompleted (batch/package setup).
        public static bool TryExport() => TryExportExplicit(false);

        public static bool TryRebuild() => TryExportExplicit(true);

        // UI callers receive acceptance immediately; completion means publication,
        // never merely finishing the worker or successfully compiling Unity code.
        public static bool RequestExport(System.Action<bool> completed = null, bool rebuild = false) {
            if (exporting || UnityEditor.BuildPipeline.isBuildingPlayer || UnityEngine.Application.isBatchMode || UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData")) return false;
            UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
            UnityEditor.SessionState.SetBool(PendingKey, false);
            try {
                var fingerprint = Fingerprint();
                UnityEditor.SessionState.SetString(AutomaticAttemptKey, fingerprint);
                StartBackground(fingerprint, rebuild, completed);
                return true;
            } catch (System.Exception exception) {
                MarkExportStarted();
                UnityEngine.Debug.LogException(exception);
                return false;
            }
        }

        private static void StartBackground(string fingerprint, bool rebuild, System.Action<bool> completed) {
            exporting = true;
            MarkExportStarted();
            backgroundVersion = requestVersion;
            discardBackground = changedDuringAnalysis = false;
            backgroundCompleted = completed;
            try {
                UnityEditor.EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                // Synchronous: the publication phases report while the Editor thread is busy.
                backgroundProgress = UnityEditor.Progress.Start("ME.BECS source inputs", "Capture input snapshot",
                    UnityEditor.Progress.Options.Managed | UnityEditor.Progress.Options.Synchronous);
                UnityEditor.Progress.RegisterCancelCallback(backgroundProgress, () => {
                    if (publishing || background == null) return false;
                    discardBackground = true;
                    background.Cancel();
                    return true;
                });
                background = new SourceGeneratorInputAnalysis(fingerprint, rebuild);
                nextBackgroundReport = 0d;
            } catch {
                FinishBackground(false, UnityEditor.Progress.Status.Failed, notify: false);
                throw;
            }
        }

        internal static bool CanPublishAnalysis(uint capturedVersion, uint currentVersion, bool discarded,
            string capturedFingerprint, string currentFingerprint, bool editorBusy) =>
            !discarded && !editorBusy && capturedVersion == currentVersion &&
            !string.IsNullOrEmpty(capturedFingerprint) && capturedFingerprint == currentFingerprint;

        private static void PollBackground() {
            if (publicationSteps != null) { StepPublication(); return; }
            if (publicationTask != null) { PollPublication(); return; }
            background.PumpMainThread();
            if (!background.IsCompleted) {
                if (UnityEditor.EditorApplication.timeSinceStartup >= nextBackgroundReport) {
                    nextBackgroundReport = UnityEditor.EditorApplication.timeSinceStartup + 0.15d;
                    if (UnityEditor.Progress.Exists(backgroundProgress) &&
                        UnityEditor.Progress.GetStatus(backgroundProgress) == UnityEditor.Progress.Status.Running) {
                        // Fraction only: per-stage step counts reset the bar on every stage.
                        // Work-item counts stay in the description text.
                        var description = "IL analysis: " + background.Description + "\n" +
                            background.ElapsedSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "s";
                        UnityEditor.Progress.Report(backgroundProgress, AnalysisEnd * System.Math.Min(1f, background.Progress / 0.98f), description);
                    }
                }
                return;
            }
            var success = false;
            var status = UnityEditor.Progress.Status.Failed;
            try {
                var result = background.TakeResult(); // IsCompleted was checked: no blocking wait.
                if (UnityEditor.Progress.Exists(backgroundProgress))
                    UnityEditor.Progress.Report(backgroundProgress, AnalysisEnd, "IL summaries ready. Validating input snapshot before publication.");
                var busy = UnityEditor.BuildPipeline.isBuildingPlayer || UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ||
                    UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
                var fingerprint = discardBackground || busy ? null : Fingerprint();
                if (!CanPublishAnalysis(backgroundVersion, requestVersion, discardBackground, result.fingerprint, fingerprint, busy)) {
                    if (!discardBackground) changedDuringAnalysis = true;
                    status = UnityEditor.Progress.Status.Canceled;
                    UnityEngine.Debug.Log("[ME.BECS] Background IL analysis was cancelled or superseded; no inputs were published.");
                    return;
                }
                publishing = true;
                if (UnityEditor.Progress.Exists(backgroundProgress)) {
                    UnityEditor.Progress.UnregisterCancelCallback(backgroundProgress);
                    UnityEditor.Progress.Report(backgroundProgress, AnalysisEnd, "Publishing Runtime inputs on the Editor thread.");
                }
                // The Editor-thread preparation runs as resumable steps across Editor
                // frames (StepPublication) instead of freezing the Editor for all of it.
                pendingPublication = null;
                publicationSteps = CodeGenerator.PrepareBackgroundPublicationSteps(result, value => pendingPublication = value);
            } catch (System.OperationCanceledException) {
                status = UnityEditor.Progress.Status.Canceled;
                UnityEngine.Debug.Log("[ME.BECS] Background IL analysis cancelled; no inputs were published.");
            } catch (System.Exception exception) {
                UnityEngine.Debug.LogException(exception);
            } finally {
                if (changedDuringAnalysis) {
                    UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
                    UnityEditor.SessionState.SetBool(PendingKey, true);
                    due = UnityEditor.EditorApplication.timeSinceStartup + 0.5d;
                }
                if (publicationTask == null && publicationSteps == null) {
                    try { EndPublication(false); }
                    finally { FinishBackground(success, status, notify: true); }
                }
            }
        }

        private static System.Collections.IEnumerator publicationSteps;
        private const double PublicationSliceMilliseconds = 25d;

        // Advance the Editor-thread publication by a frame-sized slice. Reload is
        // locked and publication is not cancellable; a stage that changes inputs
        // midway is detected by the final fingerprint checks and publishes nothing.
        private static void StepPublication() {
            var done = false;
            var failed = false;
            CodeGeneratorTimings.outerProgress = backgroundProgress + 1;
            CodeGeneratorTimings.ResumeAll();
            var slice = System.Diagnostics.Stopwatch.StartNew();
            try {
                while (slice.Elapsed.TotalMilliseconds < PublicationSliceMilliseconds) {
                    if (!publicationSteps.MoveNext()) { done = true; break; }
                }
            } catch (System.Exception exception) {
                UnityEngine.Debug.LogException(exception);
                failed = true;
            } finally {
                CodeGeneratorTimings.SuspendAll();
                CodeGeneratorTimings.outerProgress = 0;
            }
            if (!done && !failed) return;
            var steps = publicationSteps;
            publicationSteps = null;
            try { (steps as System.IDisposable)?.Dispose(); }
            catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); failed = true; }
            if (!failed && pendingPublication == null) {
                UnityEngine.Debug.LogException(new System.InvalidOperationException("Source input preparation did not complete. See Console for diagnostics."));
                failed = true;
            }
            if (!failed) {
                try {
                    UnityEditor.AssetDatabase.StartAssetEditing();
                    publicationAssetEditing = true;
                    var files = pendingPublication.files;
                    System.Threading.Interlocked.Exchange(ref publicationWritten, 0);
                    publicationTask = System.Threading.Tasks.Task.Run(() => {
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        try {
                            SourceGeneratorSystemFragments.WritePublicationFilesWithProgress(files,
                                count => System.Threading.Interlocked.Exchange(ref publicationWritten, count));
                        } finally { System.Threading.Interlocked.Exchange(ref publicationWriteMilliseconds, watch.ElapsedMilliseconds); }
                    });
                    if (UnityEditor.Progress.Exists(backgroundProgress))
                        UnityEditor.Progress.Report(backgroundProgress, EditorEnd, "Writing source inputs on background worker (Runtime / Editor).");
                    return;
                } catch (System.Exception exception) {
                    UnityEngine.Debug.LogException(exception);
                }
            }
            // Inputs rewritten underneath the sliced publication (a git checkout/restore,
            // another tool, a manual delete) make validation fail on a half-written
            // folder. That is not a stable failure: retry once the files settle.
            if (failed && InputsChangedRecently(15d)) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Source input files changed while inputs were being published; retrying the export.");
                UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
                UnityEditor.SessionState.SetBool(PendingKey, true);
                due = UnityEditor.EditorApplication.timeSinceStartup + 3d;
            }
            try { EndPublication(false); }
            finally { FinishBackground(false, UnityEditor.Progress.Status.Failed, notify: true); }
        }

        private static bool InputsChangedRecently(double seconds) {
            try {
                var directory = System.IO.Path.GetDirectoryName(SourceGeneratorInputTransport.InputPath(false));
                if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory)) return true;
                var threshold = System.DateTime.UtcNow.AddSeconds(-seconds);
                foreach (var path in System.IO.Directory.EnumerateFileSystemEntries(directory, "*", System.IO.SearchOption.AllDirectories))
                    if (System.IO.File.GetLastWriteTimeUtc(path) >= threshold) return true;
                return false;
            } catch (System.Exception) { return true; }
        }

        private static void EndPublication(bool written) {
            var pending = pendingPublication;
            pendingPublication = null;
            try {
                if (publicationAssetEditing) {
                    publicationAssetEditing = false;
                    UnityEditor.AssetDatabase.StopAssetEditing();
                }
            } catch {
                pending?.Complete(false);
                throw;
            }
            pending?.Complete(written);
        }

        private static void PollPublication() {
            if (!publicationTask.IsCompleted) {
                if (pendingPublication.files.Length > 0 && UnityEditor.EditorApplication.timeSinceStartup >= nextBackgroundReport && UnityEditor.Progress.Exists(backgroundProgress)) {
                    nextBackgroundReport = UnityEditor.EditorApplication.timeSinceStartup + 0.15d;
                    var written = System.Threading.Volatile.Read(ref publicationWritten);
                    var total = pendingPublication.files.Length;
                    UnityEditor.Progress.Report(backgroundProgress, EditorEnd + (WriteEnd - EditorEnd) * written / (float)total,
                        "Writing source inputs on background worker: " + written + "/" + total + " files.");
                }
                return;
            }
            var success = false;
            try {
                publicationTask.GetAwaiter().GetResult(); // Completed: never block the Editor.
                if (UnityEditor.Progress.Exists(backgroundProgress))
                    UnityEditor.Progress.Report(backgroundProgress, WriteEnd, "Importing source inputs on the Editor thread.");
                var fileCount = pendingPublication.files.Length;
                var importWatch = System.Diagnostics.Stopwatch.StartNew();
                SourceGeneratorSystemFragments.ImportPublicationFiles(pendingPublication.files);
                EndPublication(true);
                success = true;
                UnityEngine.Debug.Log("[ME.BECS] Source input publication: files=" + fileCount +
                    "; worker write=" + System.Threading.Interlocked.Read(ref publicationWriteMilliseconds) +
                    " ms; Editor import/validation=" + importWatch.ElapsedMilliseconds + " ms.");
            } catch (System.Exception exception) {
                UnityEngine.Debug.LogException(exception);
            } finally {
                publicationTask = null;
                try { EndPublication(false); }
                finally { FinishBackground(success, success ? UnityEditor.Progress.Status.Succeeded : UnityEditor.Progress.Status.Failed, notify: true); }
            }
        }

        private static void FinishBackground(bool successful, UnityEditor.Progress.Status status, bool notify) {
            var callback = backgroundCompleted;
            backgroundCompleted = null;
            background?.Dispose();
            background = null;
            exporting = publishing = false;
            try {
                if (backgroundProgress >= 0 && UnityEditor.Progress.Exists(backgroundProgress) &&
                    UnityEditor.Progress.GetStatus(backgroundProgress) == UnityEditor.Progress.Status.Running)
                    UnityEditor.Progress.Finish(backgroundProgress, status);
                backgroundProgress = -1;
            } catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
            finally {
                if (reloadLocked) { reloadLocked = false; UnityEditor.EditorApplication.UnlockReloadAssemblies(); }
            }
            // A completion handler may enqueue a new analysis. It must not
            // inherit (or accidentally release) the previous reload lock.
            if (notify && callback != null) {
                try { callback(successful); }
                catch (System.Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
        }

        private static bool TryExportExplicit(bool rebuild) {
            if (exporting) return false;
            if (UnityEditor.BuildPipeline.isBuildingPlayer || UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for Player build/compilation/import to finish and leave Play Mode before exporting inputs.");
                return false;
            }
            exporting = true;
            UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
            UnityEditor.SessionState.SetBool(PendingKey, false);
            try { return ExportCore(rebuild); }
            finally { exporting = false; }
        }

        private static bool ExportCore(bool rebuild = false) {
            MarkExportStarted();
            return CodeGenerator.TryRegenerateBurstAOT(forced: true, cleanCache: rebuild);
        }

        private static void MarkExportStarted() => UnityEditor.SessionState.SetBool(FailedKey, true);

        // Studio edits are saved separately from publishing compiler inputs. Keep this
        // across consumer reloads so saving an asset cannot silently compile its draft.
        public static void DeferGraphCompilation(ME.BECS.FeaturesGraph.SystemsGraph graph) {
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(graph));
            if (string.IsNullOrEmpty(guid)) return;
            var entries = new System.Collections.Generic.HashSet<string>(UnityEditor.SessionState.GetString(DeferredGraphsKey, "").Split(';'));
            entries.Remove(""); entries.Add(guid);
            UnityEditor.SessionState.SetString(DeferredGraphsKey, string.Join(";", entries));
        }

        public static bool IsGraphCompilationDeferred(ME.BECS.FeaturesGraph.SystemsGraph graph) {
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(graph));
            return !string.IsNullOrEmpty(guid) && System.Array.IndexOf(UnityEditor.SessionState.GetString(DeferredGraphsKey, "").Split(';'), guid) >= 0;
        }

        private static bool HasDeferredGraphs() {
            foreach (var guid in UnityEditor.SessionState.GetString(DeferredGraphsKey, "").Split(';'))
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(UnityEditor.AssetDatabase.GUIDToAssetPath(guid))) return true;
            return false;
        }

        public static void Request() {
            unchecked { ++requestVersion; }
            if (IsAnalyzing) {
                discardBackground = changedDuringAnalysis = true;
                background.Cancel();
            }
            UnityEditor.SessionState.SetBool(PendingKey, true);
            due = UnityEditor.EditorApplication.timeSinceStartup + 0.5d;
        }

        private static string Fingerprint() => SourceGeneratorGraphSnapshot.GetCurrent();

        private static void RecordSuccessfulExport() {
            var snapshot = CodeGenerator.LastExportedGraphSnapshot;
            if (string.IsNullOrEmpty(snapshot)) throw new System.InvalidOperationException("Successful export has no graph snapshot.");
            UnityEditor.SessionState.SetString(FingerprintKey, snapshot);
            UnityEditor.SessionState.EraseString(DeferredGraphsKey);
            // Do not erase a newer request raised by imports during export. The next
            // pass compares its fingerprint and either exports or clears it cheaply.
            UnityEditor.SessionState.SetBool(FailedKey, false);
        }

        internal static bool ShouldExportAutomatically(string fingerprint, string exported, string attempted, bool failed, bool recovery) =>
            (failed || recovery || fingerprint != exported) && fingerprint != attempted;

        private static void Update() {
            if (background != null) { PollBackground(); return; }
            // Keep PendingKey set during a build: asset callbacks may request a refresh,
            // but publication must not invalidate the inputs used by that build.
            if (exporting || UnityEditor.BuildPipeline.isBuildingPlayer || HasDeferredGraphs() || !UnityEditor.SessionState.GetBool(PendingKey, false) ||
                UnityEditor.EditorApplication.timeSinceStartup < due || UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.Application.isBatchMode || UnityEditor.EditorPrefs.HasKey("ME.BECS.Editor.AwaitPackageImportData")) return;
            // A failed export is retried on the next change/manual compile, not every
            // editor frame. Requests raised during export remain coalesced.
            UnityEditor.SessionState.SetBool(PendingKey, false);
            exporting = true;
            try {
                var fingerprint = Fingerprint();
                var recovery = SourceGeneratorGraphSnapshot.HasRecoveryInputs() || !SourceGeneratorAnalysisReceipt.IsCurrent(fingerprint);
                if (!UnityEditor.SessionState.GetBool(FailedKey, false) && SourceGeneratorGraphSnapshot.IsCompiledCurrent(fingerprint, out _)) {
                    UnityEditor.SessionState.EraseString(AutomaticAttemptKey);
                    UnityEditor.SessionState.SetString(FingerprintKey, fingerprint);
                    return;
                }
                // A failed export may have published some files. Even reverting the
                // graph to the old fingerprint requires a complete successful export.
                // Imports of unrelated assets must not retry a failed export of
                // exactly the same input. SessionState survives consumer reloads;
                // explicit Compile/Rebuild clears this attempt stamp.
                if (!ShouldExportAutomatically(fingerprint, UnityEditor.SessionState.GetString(FingerprintKey, ""),
                        UnityEditor.SessionState.GetString(AutomaticAttemptKey, ""), UnityEditor.SessionState.GetBool(FailedKey, false), recovery)) return;
                UnityEditor.SessionState.SetString(AutomaticAttemptKey, fingerprint);
                StartBackground(fingerprint, false, null);
            } catch (System.Exception exception) {
                MarkExportStarted();
                UnityEngine.Debug.LogException(exception);
            } finally { if (background == null) exporting = false; }
        }
    }

    internal sealed class SourceGeneratorInputTargetChange : UnityEditor.Build.IActiveBuildTargetChanged {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(UnityEditor.BuildTarget previousTarget, UnityEditor.BuildTarget newTarget) =>
            SourceGeneratorInputRefresh.Request();
    }

    internal sealed class SourceGeneratorInputPostprocessor : UnityEditor.AssetPostprocessor {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
            foreach (var paths in new[] { imported, deleted, moved, movedFrom }) {
                foreach (var path in paths) {
                    if (SourceGeneratorGraphSnapshot.CanAffectAssetInputs(path)) {
                        SourceGeneratorInputRefresh.Request();
                        return;
                    }
                }
            }
        }
    }

    internal sealed class SourceGeneratorInputSaveProcessor : UnityEditor.AssetModificationProcessor {
        private static string[] OnWillSaveAssets(string[] paths) {
            foreach (var path in paths) {
                if (!SourceGeneratorGraphSnapshot.CanAffectAssetInputs(path)) continue;
                SourceGeneratorInputRefresh.Request();
                break;
            }
            return paths;
        }
    }
}
