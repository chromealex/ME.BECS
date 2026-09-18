using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ME.BECS.SourceGenerator;

// Structural scheduling input only: does not decide batches, Burst boundaries or lifecycle filtering.
internal static class GraphDependencyOrder {
    internal static string Analyze(GraphTopologyInput topology) {
        var report = new StringBuilder("ME.BECS.GraphDependencyOrder.v1\n");
        for (var occurrence = 0; occurrence < topology.Occurrences.Count; ++occurrence) {
            var graph = topology.Occurrences[occurrence];
            var inputs = new List<int>[graph.Nodes.Count];
            var outputs = new List<int>[graph.Nodes.Count];
            var mismatches = new List<string>();
            for (var node = 0; node < graph.Nodes.Count; ++node) {
                // Preserve first edge occurrence, like the existing dependency expressions.
                inputs[node] = graph.Nodes[node].Inputs.SelectMany(static port => port).Distinct().ToList();
                outputs[node] = graph.Nodes[node].Outputs.SelectMany(static port => port).Distinct().ToList();
            }
            for (var node = 0; node < graph.Nodes.Count; ++node) {
                foreach (var dependency in inputs[node])
                    if (!outputs[dependency].Contains(node)) mismatches.Add(dependency.ToString(CultureInfo.InvariantCulture) + ">" + node.ToString(CultureInfo.InvariantCulture));
                foreach (var dependent in outputs[node])
                    if (!inputs[dependent].Contains(node)) mismatches.Add(node.ToString(CultureInfo.InvariantCulture) + ">" + dependent.ToString(CultureInfo.InvariantCulture));
            }
            report.Append("occurrence\t").Append(occurrence.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (mismatches.Count != 0) {
                report.Append("inconsistent-edges\t").Append(string.Join(",", mismatches.Distinct().OrderBy(static item => item, StringComparer.Ordinal))).Append('\n');
                continue;
            }
            var indegree = inputs.Select(static edges => edges.Count).ToArray();
            var ready = new SortedSet<int>();
            for (var node = 0; node < indegree.Length; ++node) if (indegree[node] == 0) ready.Add(node);
            var order = new List<int>();
            while (ready.Count != 0) {
                var node = ready.Min;
                ready.Remove(node);
                order.Add(node);
                foreach (var dependent in outputs[node]) if (--indegree[dependent] == 0) ready.Add(dependent);
            }
            if (order.Count != graph.Nodes.Count) {
                // Remaining nodes include downstream dependents of cycles, not only cycle members.
                report.Append("cycle-or-blocked\t").Append(string.Join(",", Enumerable.Range(0, indegree.Length)
                    .Where(index => indegree[index] > 0).Select(index => index.ToString(CultureInfo.InvariantCulture)))).Append('\n');
                continue;
            }
            report.Append("structural-order\t").Append(string.Join(",", order.Select(index => index.ToString(CultureInfo.InvariantCulture)))).Append('\n');
            foreach (var node in order) report.Append("dependencies\t").Append(node.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(string.Join(",", inputs[node].Select(index => index.ToString(CultureInfo.InvariantCulture)))).Append('\n');
        }
        return report.ToString();
    }
}
