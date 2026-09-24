namespace ME.BECS.Editor.Systems {
    using System;
    using System.Globalization;
    using System.Reflection;
    using System.Text;
    using ME.BECS.FeaturesGraph;
    using scg = System.Collections.Generic;

    public static class SourceGeneratorGraphLifecycleValidation {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Graph Lifecycle Calls")]
        private static void Compare() {
            if (UnityEditor.EditorApplication.isCompiling) { UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation."); return; }
            var report = new StringBuilder();
            var plans = new scg.Dictionary<(int, string), scg.List<string[]>>();
            var snapshots = new scg.Dictionary<int, scg.List<string>>();
            var metadataErrors = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.IsDynamic) continue;
                try {
                    foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>()) {
                        if (attribute.Key == "ME.BECS.TypeInput.v1") {
                            var fields = attribute.Value?.Split('\t');
                            if (fields != null && fields.Length == 6 && fields[0] == "runtime" && fields[1] == "graph-topology") {
                                var snapshotId = int.Parse(fields[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                                if (!snapshots.TryGetValue(snapshotId, out var snapshotValues)) snapshots.Add(snapshotId, snapshotValues = new scg.List<string>());
                                snapshotValues.Add(Encoding.UTF8.GetString(Convert.FromBase64String(fields[5])));
                            }
                            continue;
                        }
                        if (attribute.Key != "ME.BECS.GraphLifecyclePlan.v1") continue;
                        var rows = attribute.Value?.Split('\n');
                        if (rows == null || rows.Length < 4 || !int.TryParse(rows[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var id))
                            throw new FormatException("Malformed lifecycle plan");
                        var key = (id, rows[1]);
                        if (!plans.TryGetValue(key, out var values)) plans.Add(key, values = new scg.List<string[]>());
                        values.Add(rows);
                    }
                } catch (Exception exception) { ++metadataErrors; report.AppendLine("Metadata error: " + assembly.FullName + ": " + exception.Message); }
            }
            var compared = 0; var equal = 0; var unavailable = 0; var errors = 0; var dependencyEqual = 0;
            var generator = new SystemsCodeGenerator { burstedTypes = UnityEditor.TypeCache.GetTypesWithAttribute<Unity.Burst.BurstCompileAttribute>() };
            var guids = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (var guid in guids) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null || graph.isInnerGraph) continue;
                foreach (var phase in new[] { Method.Awake, Method.Start, Method.Update, Method.Destroy, Method.DrawGizmos }) {
                    var label = graph.name + " [" + graph.GetId().ToString(CultureInfo.InvariantCulture) + "] " + phase;
                    try {
                        // GetSyncPoint may normalize missing arrays: refuse those assets before tracing.
                        var topology = SourceGeneratorGraphTopology.Serialize(graph);
                        if (topology.Contains("\tunknown\n")) { ++unavailable; report.AppendLine(label + ": sync snapshot unavailable"); continue; }
                        if (!snapshots.TryGetValue(graph.GetId(), out var snapshot) || snapshot.Count != 1 || snapshot[0] != topology) {
                            ++unavailable; report.AppendLine(label + ": missing, duplicate or stale topology snapshot; regenerate inputs"); continue;
                        }
                        if (!plans.TryGetValue((graph.GetId(), phase.ToString()), out var candidates) || candidates.Count != 1 ||
                            candidates[0][2] != "ME.BECS.GraphLifecyclePlan.v1") {
                            ++unavailable; report.AppendLine(label + ": missing, duplicate or unavailable compiled plan"); continue;
                        }
                        var calls = ReadCalls(candidates[0]);
                        var sourceDependencies = LifecycleDependencyTrace.FromPlan(candidates[0]);
                        var editorDependencies = new LifecycleDependencyTrace();
                        var trace = new scg.List<string>();
                        var ignoredContent = new scg.List<string>();
                        var name = "Graph" + EditorUtils.GetCodeName(graph.name);
                        switch (phase) {
                            case Method.Awake: SystemsCodeGenerator.AddGraph<IAwake>(generator, name, 0, "OnAwake", phase, ignoredContent, graph, trace, editorDependencies); break;
                            case Method.Start: SystemsCodeGenerator.AddGraph<IStart>(generator, name, 0, "OnStart", phase, ignoredContent, graph, trace, editorDependencies); break;
                            case Method.Update: SystemsCodeGenerator.AddGraph<IUpdate>(generator, name, 0, "OnUpdate", phase, ignoredContent, graph, trace, editorDependencies); break;
                            case Method.Destroy: SystemsCodeGenerator.AddGraph<IDestroy>(generator, name, 0, "OnDestroy", phase, ignoredContent, graph, trace, editorDependencies); break;
                            case Method.DrawGizmos: SystemsCodeGenerator.AddGraph<IDrawGizmos>(generator, name, 0, "OnDrawGizmos", phase, ignoredContent, graph, trace, editorDependencies); break;
                        }
                        ++compared;
                        var matches = calls.Count == trace.Count;
                        for (var index = 0; index < Math.Max(calls.Count, trace.Count); ++index) {
                            var source = index < calls.Count ? calls[index] : "<missing>";
                            var legacy = index < trace.Count ? trace[index] : "<missing>";
                            if (source == legacy) continue;
                            matches = false;
                            report.AppendLine(label + " call " + index + ": source=" + source + "; Editor=" + legacy);
                        }
                        if (matches) ++equal;
                        var dependenciesMatch = sourceDependencies.Result == editorDependencies.Result &&
                            sourceDependencies.Events.Count == editorDependencies.Events.Count;
                        for (var index = 0; index < Math.Max(sourceDependencies.Events.Count, editorDependencies.Events.Count); ++index) {
                            var source = index < sourceDependencies.Events.Count ? sourceDependencies.Events[index] : "<missing>";
                            var legacy = index < editorDependencies.Events.Count ? editorDependencies.Events[index] : "<missing>";
                            if (source == legacy) continue;
                            dependenciesMatch = false;
                            report.AppendLine(label + " dependency event " + index + ": source=" + source + "; Editor=" + legacy);
                        }
                        if (sourceDependencies.Result != editorDependencies.Result)
                            report.AppendLine(label + " result handle: source=" + sourceDependencies.Result + "; Editor=" + editorDependencies.Result);
                        if (dependenciesMatch) ++dependencyEqual;
                    } catch (Exception exception) { ++errors; report.AppendLine(label + ": " + exception.Message); }
                }
            }
            SourceGeneratorGraphTopology.PublishLifecycleComparison("Lifecycle call traces: compared=" + compared + ", equal=" + equal +
                ", dependency traces equal=" + dependencyEqual +
                ", unavailable=" + unavailable + ", errors=" + errors + ", metadata errors=" + metadataErrors +
                ". Checks call metadata plus symbolic dependency events and final handle, including pass-through batches. Does NOT verify runtime system behavior, Burst execution or stripping. No systems invoked; no C# files read/written.", report.ToString());
        }

        private static scg.List<string> ReadCalls(string[] rows) {
            var result = new scg.List<string>();
            var ordinal = 0;
            var ended = false;
            for (var index = 3; index < rows.Length; ++index) {
                if (rows[index].Length == 0 && index == rows.Length - 1) continue;
                if (ended) throw new FormatException("Trailing lifecycle records");
                var fields = rows[index].Split('\t');
                if (fields.Length == 2 && fields[0] == "result") { ended = true; continue; }
                if (fields.Length != 11 || fields[0] != (ordinal++).ToString(CultureInfo.InvariantCulture) ||
                    (fields[4] != "invoke" && fields[4] != "pass")) throw new FormatException("Malformed lifecycle step");
                if (fields[4] == "invoke") result.Add(string.Join("\t", fields, 5, 6));
            }
            if (!ended) throw new FormatException("Missing lifecycle result");
            return result;
        }
    }
}
