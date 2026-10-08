namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    // Analysis freshness and compiled input identity are separate evidence. An
    // unchanged semantic manifest need not recompile merely because code changed.
    /// <summary>
    /// Provides graph snapshot for BECS source-generator publication.
    /// </summary>
    public static class SourceGeneratorGraphSnapshot {
        /// <summary>
        /// Metadata key used by <c>SourceGeneratorGraphSnapshot</c>.
        /// </summary>
        public const string MetadataKey = "ME.BECS.GraphInputSnapshot.v1";
        private const string RecoveryMetadataKey = "ME.BECS.InputRecovery.v1";
        private static readonly string[] Consumers = { "runtime", "editor" };

        /// <summary>
        /// Returns current.
        /// </summary>
        public static string GetCurrent() {
            return Combine(GetCompilerSnapshot(), "analysis", new[] { GetCodeFingerprint() });
        }

        internal static string GetCompilerSnapshot() => Combine(GetAssetFingerprint(),
            UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(), Array.Empty<string>());

        // No graph/config/theme values or generated consumer MVIDs here. Consumer
        // dependants use their own content instead of their cascading compile MVID.
        // Loaded script assemblies cannot change inside an IL analysis session (a
        // domain reload ends it), so reuse the identity there: export and preflight
        // asked for it 5-7 times, ~0.5 s each. Asset fingerprints stay uncached.
        internal static string GetCodeFingerprint() =>
            ILAnalysisSession.Get((typeof(SourceGeneratorGraphSnapshot), "code-fingerprint"), ComputeCodeFingerprint);

        private static string ComputeCodeFingerprint() {
            var scriptAssemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor);
            var byName = scriptAssemblies.ToDictionary(script => script.name, StringComparer.Ordinal);
            var dependants = SourceGeneratorCodeIdentity.ConsumerDependants(scriptAssemblies);
            var loaded = SourceGeneratorCodeIdentity.LoadedScripts(AppDomain.CurrentDomain.GetAssemblies(),
                    new HashSet<string>(byName.Keys, StringComparer.Ordinal))
                .GroupBy(assembly => assembly.GetName().Name).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var scripts = new List<string>(SourceGeneratorCodeIdentity.Libraries(scriptAssemblies));
            foreach (var script in scriptAssemblies) {
                // Including consumers makes each input compilation invalidate itself.
                if (SourceGeneratorCodeIdentity.IsConsumer(script.name)) continue;
                scripts.Add(script.name + "\tdefines\t" + string.Join(";", script.defines.OrderBy(value => value, StringComparer.Ordinal)));
                if (!loaded.TryGetValue(script.name, out var assemblies)) {
                    scripts.Add(script.name + "\tunloaded");
                    continue;
                }
                if (assemblies.Length != 1) throw new InvalidOperationException("Ambiguous loaded script assembly: " + script.name);
                var assembly = assemblies[0];
                scripts.Add(assembly.FullName + "\t" + SourceGeneratorCodeIdentity.Stamp(assembly, script, dependants));
            }
            TraceCodeInputs(scripts);
            return Combine("ME.BECS.ILInputs.v3",
                UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(), scripts);
        }

        private static void TraceCodeInputs(IEnumerable<string> scripts) {
            const string key = "ME.BECS.GraphInputs.CodeIdentityDetails";
            var current = string.Join("\n", scripts.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
            var previous = UnityEditor.SessionState.GetString(key, "");
            if (current == previous) return;
            UnityEditor.SessionState.SetString(key, current);
            if (previous.Length == 0) return;
            var before = previous.Split('\n');
            var after = current.Split('\n');
            var changes = before.Except(after, StringComparer.Ordinal).Select(value => "- " + value)
                .Concat(after.Except(before, StringComparer.Ordinal).Select(value => "+ " + value)).ToArray();
            UnityEngine.Debug.Log("[ME.BECS] Source code input identity changed (" + changes.Length + " entries):\n" +
                string.Join("\n", changes.Take(20)) + (changes.Length > 20 ? "\n..." : ""));
        }

        private static bool HasCompilerRecovery(Assembly[] assemblies) => assemblies
            .Where(assembly => !assembly.IsDynamic && assembly.GetType("ME.BECS.SourceGenerated.IncompleteInputGuard", false) != null)
            .Any(assembly => assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                .Any(attribute => attribute.Key == RecoveryMetadataKey));

        internal static bool HasRecoveryInputs() {
            try {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                return HasCompilerRecovery(assemblies) ||
                    !SourceGeneratorInputCatalog.TryGet(false, assemblies, out _, out _) ||
                    !SourceGeneratorInputCatalog.TryGet(true, assemblies, out _, out _) ||
                    !SourceGeneratorSystemFragments.ValidateCompiledAssemblies(assemblies, out _);
            } catch (Exception) {
                // Bad/stale compiled evidence requires an export. It must not
                // throw before automatic recovery can reach the exporter.
                return true;
            }
        }

        private static string GetAssetFingerprint() {
            var configurationTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:EntityConfig")) {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<EntityConfig>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (config == null) throw new InvalidOperationException("Missing entity config: " + guid);
                void Add(System.Collections.IEnumerable values, string kind) {
                    if (values == null) throw new InvalidOperationException("Missing config " + kind + " collection: " + guid);
                    foreach (var value in values) {
                        if (value == null) throw new InvalidOperationException("Missing config " + kind + " type: " + guid);
                        configurationTypes.Add(kind + "\t" + value.GetType().AssemblyQualifiedName);
                    }
                }
                // Match runtime discovery: values are loaded separately at runtime;
                // only selected component/aspect types affect generated registration.
                Add(config.data.components, "C");
                Add(config.staticData.components, "C");
                Add(config.sharedData.components, "C");
                Add(config.aspects.components, "A");
            }
            var themes = new StringBuilder();
            new ThemesCodeGenerator { editorAssembly = true }.AppendSourceGeneratorInputs(themes);
            return CombineAssets(SourceGeneratorGraphTopology.GetProjectCompilationFingerprint(), configurationTypes, themes.ToString());
        }

        internal static string CombineAssets(string graphs, IEnumerable<string> configurationTypes, string themes) {
            var text = new StringBuilder("ME.BECS.AssetInputSnapshot.v1\n").Append(graphs).Append('\n');
            foreach (var type in configurationTypes.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                text.Append(type).Append('\n');
            text.Append("themes\n").Append(themes);
            return ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(text.ToString());
        }

        // Import/save callbacks only request a coalesced comparison. Unrelated
        // assets need no export when their effective input fingerprint is unchanged.
        /// <summary>
        /// Tests whether the context can affect asset inputs.
        /// </summary>
        public static bool CanAffectAssetInputs(string path) {
            if (string.IsNullOrEmpty(path)) return false;
            path = path.Replace('\\', '/');
            if (path == "Assets/ME.BECS.Gen" || path.StartsWith("Assets/ME.BECS.Gen/", StringComparison.Ordinal)) return false;
            if (path.StartsWith(SourceGeneratorInputTransport.ProjectDirectory + "/", StringComparison.Ordinal) &&
                path.EndsWith(".additionalfile", StringComparison.OrdinalIgnoreCase)) return true;
            return path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(System.IO.Path.GetExtension(path)); // folder move/delete can change custom theme paths
        }

        internal static string Combine(string graphs, string target, IEnumerable<string> scripts) {
            var text = new StringBuilder("ME.BECS.GraphInputSnapshot.v1\n").Append(graphs).Append('\n').Append(target).Append('\n');
            foreach (var script in scripts.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                text.Append(script).Append('\n');
            return ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(text.ToString());
        }

        /// <summary>
        /// Attempts to validate current and reports whether the operation succeeded.
        /// </summary>
        public static bool TryValidateCurrent(out string reason) {
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) {
                reason = "Wait for script compilation and asset import to finish.";
                return false;
            }
            try { return IsCompiledCurrent(GetCurrent(), out reason); }
            catch (Exception exception) { reason = "Cannot inspect graph inputs: " + exception.Message; return false; }
        }

        /// <summary>
        /// Tests whether the context is compiled current.
        /// </summary>
        public static bool IsCompiledCurrent(string expected, out string reason) {
            try {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                if (HasCompilerRecovery(assemblies)) {
                    reason = "Compiler input recovery is incomplete. Wait for input export and successful compilation.";
                    return false;
                }
                if (!SourceGeneratorAnalysisReceipt.TryValidateCompiled(expected, assemblies, out var compilerSnapshot, out reason)) return false;
                var snapshots = new KeyValuePair<string, string[]>[2];
                foreach (var editor in new[] { false, true }) {
                    if (!SourceGeneratorInputCatalog.TryGet(editor, assemblies, out var catalog, out reason)) return false;
                    snapshots[editor ? 1 : 0] = new KeyValuePair<string, string[]>(catalog.Profile, new[] { catalog.Snapshot });
                }
                return ValidateCompiled(compilerSnapshot, snapshots, out reason) && SourceGeneratorSystemFragments.ValidateCompiledAssemblies(assemblies, out reason);
            } catch (Exception exception) { reason = "Cannot inspect compiled graph input snapshots: " + exception.Message; return false; }
        }

        internal static bool ValidateCompiled(string expected, IEnumerable<KeyValuePair<string, string[]>> snapshots, out string reason) {
            reason = "";
            var values = snapshots.ToArray();
            foreach (var consumer in Consumers) {
                var candidates = values.Where(value => value.Key == consumer).ToArray();
                if (candidates.Length != 1 || candidates[0].Value == null || candidates[0].Value.Length != 1 ||
                    string.IsNullOrEmpty(expected) || candidates[0].Value[0] != expected) {
                    reason = consumer + " has missing, stale or ambiguous graph inputs. Regenerate inputs and wait for successful Unity compilation.";
                    return false;
                }
            }
            return true;
        }
    }
}
