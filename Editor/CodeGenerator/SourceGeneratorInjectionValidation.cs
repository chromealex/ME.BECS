namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal static class SourceGeneratorInjectionValidation {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Injection Coverage")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before checking injection coverage.");
                return;
            }
            var details = new StringBuilder();
            var discovered = new Dictionary<Type, HashSet<Type>>();
            var graphs = 0;
            var systemSelected = 0;
            var systemFallback = 0;
            var jobSelected = 0;
            var jobFallback = 0;
            var errors = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph")) {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                try {
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<FeaturesGraph.SystemsGraph>(path);
                    if (graph == null) throw new InvalidOperationException("Graph asset could not be loaded.");
                    if (graph.isInnerGraph) continue;
                    ++graphs;
                    var layout = SourceGeneratorInputManifest.GetGraphSystems(graph);
                    var graphTypes = new HashSet<Type>(layout.Select(item => item.type));
                    var jobs = new HashSet<Type>();
                    details.AppendLine("Graph: " + path);
                    foreach (var system in graphTypes.OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal)) {
                        var fields = system.GetFields(Fields);
                        var hasBool = fields.Any(f => f.FieldType == typeof(bool));
                        foreach (var field in fields.Where(f => typeof(IInject).IsAssignableFrom(f.FieldType))) {
                            var standard = field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(InjectSystem<>);
                            var targetPresent = standard && field.FieldType.GenericTypeArguments[0].IsVisible && graphTypes.Contains(field.FieldType.GenericTypeArguments[0]);
                            var selected = !hasBool && targetPresent && !field.IsInitOnly &&
                                (field.IsPublic || SourceGeneratorInputManifest.TryGetPrivateSystemInjectionMethod(field, out _));
                            if (selected) ++systemSelected; else ++systemFallback;
                            details.AppendLine("  system " + system.AssemblyQualifiedName + " :: " + field.Name + " — " +
                                (selected ? "source-selected" : hasBool ? "blocked: bool layout guard" : !standard ? "fallback: custom IInject" :
                                 !targetPresent ? "blocked: target absent or inaccessible" : field.IsInitOnly ? "fallback: readonly" : "fallback: partial setter unavailable"));
                        }
                        if (!discovered.TryGetValue(system, out var systemJobs)) {
                            systemJobs = new HashSet<Type>();
                            foreach (var lifecycle in SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(system))
                                SourceGeneratorScheduledJobsValidation.Collect(lifecycle, systemJobs);
                            discovered.Add(system, systemJobs);
                        }
                        jobs.UnionWith(systemJobs);
                    }
                    foreach (var job in jobs.OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal)) {
                        var fields = job.GetFields(Fields);
                        var injected = fields.Where(f => typeof(IInject).IsAssignableFrom(f.FieldType) || Attribute.IsDefined(f, typeof(InjectDeltaTimeAttribute))).ToArray();
                        if (injected.Length == 0) continue;
                        var graphPlan = SourceGeneratorInputManifest.TryGetGraphJobFields(job, layout, out _);
                        var deltaPlan = !graphPlan && SourceGeneratorInputManifest.TryGetJobDeltaTimeRegistration(job, out _);
                        if (graphPlan || deltaPlan) ++jobSelected; else ++jobFallback;
                        details.AppendLine("  job " + job.AssemblyQualifiedName + " — " +
                            (graphPlan ? "source-selected graph callback" : deltaPlan ? "source-selected delta callback" : "fallback/unavailable"));
                        if (!graphPlan && !deltaPlan) {
                            if (fields.Any(f => f.FieldType == typeof(bool))) details.AppendLine("    bool layout guard blocks registration");
                            if (!job.IsVisible || job.ContainsGenericParameters) details.AppendLine("    job is inaccessible or open generic");
                            foreach (var field in injected) details.AppendLine("    " + field.Name + ": " + field.FieldType.AssemblyQualifiedName +
                                (field.IsPublic ? " public" : " non-public") + (field.IsInitOnly ? " readonly" : ""));
                        }
                    }
                } catch (Exception exception) {
                    ++errors;
                    details.AppendLine("ERROR " + path + ": " + exception);
                }
            }
            SourceGeneratorReport.Publish("InjectionCoverage",
                "Injection coverage (selection only): graphs=" + graphs + "; system fields selected=" + systemSelected +
                ", fallback/blocked=" + systemFallback + "; graph/job pairs selected=" + jobSelected + ", fallback/blocked=" + jobFallback +
                "; graph errors=" + errors + ".\nFresh graph/IL discovery; registry, manifests and generated files NOT read/written. " +
                "Patch/registration methods NOT invoked. This is NOT generated-code, Burst or runtime validation.", details.ToString());
        }
    }
}
