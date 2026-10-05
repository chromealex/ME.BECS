namespace ME.BECS.Editor.Systems {
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using ME.BECS.FeaturesGraph;
    using scg = System.Collections.Generic;

    public static class SourceGeneratorGraphLifecycleValidation {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Inspect Graph Lifecycle Calls")]
        private static void Inspect() {
            if (UnityEditor.EditorApplication.isCompiling) { UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation."); return; }
            var report = new StringBuilder();
            var plans = new scg.Dictionary<(int, string), scg.List<string[]>>();
            var snapshots = new scg.Dictionary<int, scg.List<string>>();
            var execution = new scg.Dictionary<(int, string), scg.List<string>>();
            var metadataErrors = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(value => value.FullName, StringComparer.Ordinal)) {
                if (assembly.IsDynamic) continue;
                try {
                    foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>()) {
                        if (attribute.Key == "ME.BECS.GraphLifecycleExecution.v1") {
                            var selection = attribute.Value?.Split('\n');
                            if (selection == null || selection.Length != 3 ||
                                !int.TryParse(selection[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var graphId) ||
                                selection[2] != "source-plan") throw new FormatException("Malformed lifecycle execution selection");
                            var executionKey = (graphId, selection[1]);
                            if (!execution.TryGetValue(executionKey, out var owners)) execution.Add(executionKey, owners = new scg.List<string>());
                            owners.Add(assembly.FullName);
                            continue;
                        }
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
            var inspected = 0; var unavailable = 0; var errors = 0;
            var sourceSelected = 0;
            var selectionUnavailable = 0;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
            Array.Sort(guids, StringComparer.Ordinal);
            foreach (var guid in guids) {
                var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null || graph.isInnerGraph) continue;
                foreach (var phase in new[] { Method.Awake, Method.Start, Method.Update, Method.Destroy, Method.DrawGizmos }) {
                    var label = graph.name + " [" + graph.GetId().ToString(CultureInfo.InvariantCulture) + "] " + phase;
                    if (execution.TryGetValue((graph.GetId(), phase.ToString()), out var owners) && owners.Count == 1) {
                        ++sourceSelected;
                    } else {
                        ++selectionUnavailable;
                        report.AppendLine(label + ": source execution selection missing or ambiguous");
                    }
                    try {
                        // Cached sync hints are diagnostic only: the compiler derives sync from topology.
                        var topology = SourceGeneratorGraphTopology.Serialize(graph);
                        if (!snapshots.TryGetValue(graph.GetId(), out var snapshot) || snapshot.Count != 1 || WithoutSyncHints(snapshot[0]) != WithoutSyncHints(topology)) {
                            ++unavailable; report.AppendLine(label + ": missing, duplicate or stale topology snapshot"); continue;
                        }
                        if (!plans.TryGetValue((graph.GetId(), phase.ToString()), out var candidates) || candidates.Count != 1 ||
                            candidates[0][2] != "ME.BECS.GraphLifecyclePlan.v1") {
                            ++unavailable; report.AppendLine(label + ": missing, duplicate or unavailable compiled plan"); continue;
                        }
                        var calls = ReadCalls(candidates[0]);
                        var sourceDependencies = LifecycleDependencyTrace.FromPlan(candidates[0]);
                        ++inspected;
                        report.AppendLine(label + ": compiled call groups=" + calls.Count +
                            ", symbolic dependency events=" + sourceDependencies.Events.Count);
                        report.AppendLine("call\tfirst-slot\tcount\tmode\tpre-apply\tpost-apply\tburst");
                        for (var index = 0; index < calls.Count; ++index)
                            report.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').AppendLine(calls[index]);
                        foreach (var entry in sourceDependencies.Events) report.Append("dependency\t").AppendLine(entry);
                        report.Append("result-handle\t").AppendLine(sourceDependencies.Result);
                    } catch (Exception exception) { ++errors; report.AppendLine(label + ": " + exception.Message); }
                }
            }
            // Keep the historical report filename for existing workflows; this is
            // inspection of compiler output, not equivalence to a retired emitter.
            SourceGeneratorGraphTopology.PublishLifecycleComparison("Compiled lifecycle inspection: inspected=" + inspected +
                ", unavailable=" + unavailable + ", errors=" + errors + ", metadata errors=" + metadataErrors +
                ", source execution metadata=" + sourceSelected + ", execution selection unavailable=" + selectionUnavailable +
                ". No legacy comparison. Advisory report only; does not affect export. Does NOT verify runtime behavior, Burst execution or stripping. No systems invoked; no C# files read/written.", report.ToString());
        }

        private static string WithoutSyncHints(string topology) =>
            string.Join("\n", topology.Split('\n').Where(line => !line.StartsWith("sync\t", StringComparison.Ordinal)));

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
