namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using ME.BECS.Extensions.GraphProcessor;
    using ME.BECS.FeaturesGraph;

    public static class SourceGeneratorGraphTopology {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Graph Topology")]
        private static void Export() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before exporting graph topology.");
                return;
            }
            var report = new StringBuilder();
            var count = 0;
            var errors = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph")) {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                try {
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(path);
                    if (graph == null) throw new InvalidOperationException("Missing graph asset");
                    if (graph.isInnerGraph) continue;
                    report.AppendLine("asset\t" + Encode(path));
                    report.Append(Serialize(graph));
                    ++count;
                } catch (Exception exception) { ++errors; report.AppendLine("ERROR " + path + ": " + exception.Message); }
            }
            SourceGeneratorReport.Publish("GraphTopology", "Graph topology snapshots=" + count + ", errors=" + errors +
                ". Read-only asset snapshot; no sync recalculation, registry or generated output changes. Not yet used for lifecycle emission.", report.ToString());
        }

        public static string Serialize(SystemsGraph root) {
            var result = new StringBuilder("ME.BECS.GraphTopology.v2\n");
            var active = new HashSet<SystemsGraph>();
            var layouts = new Dictionary<SystemsGraph, List<SourceGeneratorInputManifest.GraphSystemInput>>();
            List<SourceGeneratorInputManifest.GraphSystemInput> Layout(SystemsGraph graph) {
                if (graph == null) throw new InvalidOperationException("Missing nested graph");
                if (!layouts.TryGetValue(graph, out var value)) layouts.Add(graph, value = SourceGeneratorInputManifest.GetGraphSystems(graph));
                return value;
            }
            var nextOccurrence = 0;
            void Append(SystemsGraph graph, int parent, int parentNode, int firstSlot) {
                if (graph == null) throw new InvalidOperationException("Missing nested graph");
                if (!active.Add(graph)) throw new InvalidOperationException("Recursive graph: " + graph.name);
                try {
                    var occurrence = checked(nextOccurrence++);
                    var layout = Layout(graph);
                    var starts = new int[graph.nodes.Count];
                    var counts = new int[graph.nodes.Count];
                    foreach (var item in layout)
                        if (item.graph == graph) counts[item.nodeIndex] = checked(counts[item.nodeIndex] + 1);
                    var cursor = firstSlot;
                    var indices = new Dictionary<BaseNode, int>();
                    for (var index = 0; index < graph.nodes.Count; ++index) {
                        var node = graph.nodes[index];
                        if (node == null || indices.ContainsKey(node)) throw new InvalidOperationException("Null/duplicate graph node at " + index);
                        indices.Add(node, index);
                        starts[index] = cursor;
                        if (node is FeaturesGraph.Nodes.GraphNode nested) counts[index] = Layout(nested.graphValue).Count;
                        cursor = checked(cursor + counts[index]);
                    }
                    result.Append("graph\t").Append(Number(occurrence)).Append('\t').Append(Number(parent)).Append('\t').Append(Number(parentNode))
                        .Append('\t').Append(Number(graph.GetId())).Append('\t').Append(Number(graph.nodes.Count)).Append('\n');
                    for (var index = 0; index < graph.nodes.Count; ++index) {
                        var node = graph.nodes[index];
                        var system = (node as FeaturesGraph.Nodes.SystemNode)?.system;
                        result.Append("node\t").Append(Number(occurrence)).Append('\t').Append(Number(index))
                            .Append('\t').Append(Encode(node.GetType().AssemblyQualifiedName)).Append('\t').Append(node.enabled ? "1" : "0")
                            .Append('\t').Append(node.IsGroupEnabled() ? "1" : "0").Append('\t').Append(Encode(system?.GetType().AssemblyQualifiedName ?? ""))
                            .Append('\t').Append(Number(starts[index])).Append('\t').Append(Number(counts[index]))
                            .Append('\t').Append(system != null && system.GetType().IsGenericType && Attribute.IsDefined(system.GetType(), typeof(SystemGenericParallelModeAttribute)) ? "1" : "0").Append('\n');
                        // Do not call GetSyncPoint: it normalizes/mutates the node's array.
                        var validSync = node.syncPoints != null && node.syncPoints.Length == (int)Method.DrawGizmos + 1;
                        foreach (var phase in new[] { Method.Awake, Method.Start, Method.Update, Method.Destroy, Method.DrawGizmos }) {
                            result.Append("sync\t").Append(Number(occurrence)).Append('\t').Append(Number(index)).Append('\t').Append(Number((int)phase));
                            if (!validSync) result.Append("\tunknown\n");
                            else {
                                var sync = node.syncPoints[(int)phase];
                                result.Append('\t').Append(sync.syncPoint ? "1" : "0").Append('\t').Append(Number(sync.syncCount))
                                    .Append('\t').Append(sync.hasMethod ? "1" : "0").Append('\n');
                            }
                        }
                        for (var port = 0; port < node.inputPorts.Count; ++port) {
                            result.Append("input\t").Append(Number(occurrence)).Append('\t').Append(Number(index)).Append('\t').Append(Number(port));
                            foreach (var edge in node.inputPorts[port].GetEdges()) {
                                if (edge.outputNode == null || !indices.TryGetValue(edge.outputNode, out var source))
                                    throw new InvalidOperationException("Missing/cross-graph input endpoint at node " + index);
                                result.Append('\t').Append(Number(source));
                            }
                            result.Append('\n');
                        }
                        for (var port = 0; port < node.outputPorts.Count; ++port) {
                            result.Append("output\t").Append(Number(occurrence)).Append('\t').Append(Number(index)).Append('\t').Append(Number(port));
                            foreach (var edge in node.outputPorts[port].GetEdges()) {
                                if (edge.inputNode == null || !indices.TryGetValue(edge.inputNode, out var target))
                                    throw new InvalidOperationException("Missing/cross-graph output endpoint at node " + index);
                                result.Append('\t').Append(Number(target));
                            }
                            result.Append('\n');
                        }
                    }
                    for (var index = 0; index < graph.nodes.Count; ++index)
                        if (graph.nodes[index] is FeaturesGraph.Nodes.GraphNode nested) Append(nested.graphValue, occurrence, index, starts[index]);
                } finally { active.Remove(graph); }
            }
            Append(root, -1, -1, 0);
            return result.ToString();
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}
