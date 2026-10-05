namespace ME.BECS.Editor {
    // Coalesced input refresh. Successful export and successful compilation are
    // separate states; the project analysis receipt plus compiled input hashes
    // provide restart-safe evidence without injecting code hashes into inputs.
    [UnityEditor.InitializeOnLoad]
    public static class SourceGeneratorInputRefresh {
        private const string PendingKey = "ME.BECS.GraphInputs.Pending";
        private const string FingerprintKey = "ME.BECS.GraphInputs.ExportedFingerprint";
        private const string FailedKey = "ME.BECS.GraphInputs.ExportIncomplete";
        private const string AutomaticAttemptKey = "ME.BECS.GraphInputs.AutomaticAttempt";
        private static double due;
        private static bool exporting;
        private static uint requestVersion;

        internal static bool HasUnfinishedExport => exporting ||
            UnityEditor.SessionState.GetBool(PendingKey, false) || UnityEditor.SessionState.GetBool(FailedKey, false);

        static SourceGeneratorInputRefresh() {
            UnityEditor.EditorApplication.update += Update;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CodeGenerator.ExportCompleted += OnExportCompleted;
            CodeGenerator.InputRefreshRequested += Request;
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

        private static bool TryExportExplicit(bool rebuild) {
            if (exporting) return false;
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation/import to finish and leave Play Mode before exporting inputs.");
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

        public static void Request() {
            unchecked { ++requestVersion; }
            UnityEditor.SessionState.SetBool(PendingKey, true);
            due = UnityEditor.EditorApplication.timeSinceStartup + 0.5d;
        }

        private static string Fingerprint() => SourceGeneratorGraphSnapshot.GetCurrent();

        private static void RecordSuccessfulExport() {
            var snapshot = CodeGenerator.LastExportedGraphSnapshot;
            if (string.IsNullOrEmpty(snapshot)) throw new System.InvalidOperationException("Successful export has no graph snapshot.");
            UnityEditor.SessionState.SetString(FingerprintKey, snapshot);
            // Do not erase a newer request raised by imports during export. The next
            // pass compares its fingerprint and either exports or clears it cheaply.
            UnityEditor.SessionState.SetBool(FailedKey, false);
        }

        internal static bool ShouldExportAutomatically(string fingerprint, string exported, string attempted, bool failed, bool recovery) =>
            (failed || recovery || fingerprint != exported) && fingerprint != attempted;

        private static void Update() {
            if (exporting || !UnityEditor.SessionState.GetBool(PendingKey, false) ||
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
                if (!ExportCore())
                    UnityEngine.Debug.LogError("[ME.BECS] Source input export did not complete. Retry input export before running the simulation.");
            } catch (System.Exception exception) {
                MarkExportStarted();
                UnityEngine.Debug.LogException(exception);
            } finally { exporting = false; }
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
