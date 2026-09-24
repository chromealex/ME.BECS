using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Fresh phase-specific sync analysis. Asset sync arrays are comparison data, not its inputs.
internal static class GraphSyncAnalysis {
    internal static bool TryCreate(GraphTopologyInput topology, InputManifestTypes resolver, string phase,
        out Dictionary<(int Graph, int Node), (bool Sync, int Count)> sync, out string reason) {
        sync = new Dictionary<(int Graph, int Node), (bool Sync, int Count)>();
        reason = "";
        var children = new Dictionary<(int, int), int>();
        for (var index = 1; index < topology.Occurrences.Count; ++index)
            children.Add((topology.Occurrences[index].Parent, topology.Occurrences[index].ParentNode), index);
        var invokes = new bool[topology.Occurrences.Count];
        try {
            var reachableOccurrences = new Dictionary<int, HashSet<int>>();
            for (var occurrence = 0; occurrence < topology.Occurrences.Count; ++occurrence) {
                var graph = topology.Occurrences[occurrence];
                if (graph.Parent >= 0) {
                    var parentNode = topology.Occurrences[graph.Parent].Nodes[graph.ParentNode];
                    if (!parentNode.Enabled || !parentNode.GroupEnabled ||
                        !reachableOccurrences.TryGetValue(graph.Parent, out var parentReachable) ||
                        !parentReachable.Contains(graph.ParentNode)) continue;
                }
                if (graph.Entry < 0 || graph.Exit < 0)
                    throw new InvalidOperationException("Missing sync-analysis boundary in active occurrence " + occurrence);
                var reachable = new HashSet<int> { graph.Entry };
                var queue = new Queue<int>();
                queue.Enqueue(graph.Entry);
                while (queue.Count != 0) {
                    foreach (var target in graph.Nodes[queue.Dequeue()].Outputs.SelectMany(static port => port))
                        if (reachable.Add(target)) queue.Enqueue(target);
                }
                reachableOccurrences.Add(occurrence, reachable);
            }
            // Children follow parents in the snapshot; process them first without recursive traversal.
            for (var occurrence = topology.Occurrences.Count - 1; occurrence >= 0; --occurrence) {
                if (!reachableOccurrences.TryGetValue(occurrence, out var reachable)) continue;
                var graph = topology.Occurrences[occurrence];
                var keep = new bool[graph.Nodes.Count];
                var calls = new bool[graph.Nodes.Count];
                var disabledJoins = new bool[graph.Nodes.Count];
                var outputs = Enumerable.Range(0, graph.Nodes.Count).Select(_ => new HashSet<int>()).ToArray();
                var inputs = Enumerable.Range(0, graph.Nodes.Count).Select(_ => new HashSet<int>()).ToArray();
                foreach (var index in reachable.OrderBy(static index => index)) {
                    var node = graph.Nodes[index];
                    var type = resolver.ResolveDefinition(node.Type, out _) ?? throw new InvalidOperationException("Unresolved sync node type");
                    var boundary = index == graph.Entry || index == graph.Exit;
                    var relay = Derives(type, "ME.BECS.Extensions.GraphProcessor.RelayNode");
                    var system = Derives(type, "ME.BECS.FeaturesGraph.Nodes.SystemNode");
                    var nested = children.TryGetValue((occurrence, index), out var child);
                    if (!boundary && !relay && !system && !nested && !Derives(type, "ME.BECS.FeaturesGraph.Nodes.StartNode"))
                        throw new InvalidOperationException("Unsupported sync node: " + node.Type);
                    var hasCall = false;
                    if (node.Enabled && node.GroupEnabled) {
                        if (system && node.SystemType.Length != 0) {
                            var definition = resolver.ResolveDefinition(node.SystemType, out _) ?? throw new InvalidOperationException("Unresolved sync system type");
                            hasCall = definition.AllInterfaces.Any(i => i.ToDisplayString() == "ME.BECS.I" + phase);
                        } else if (nested) hasCall = invokes[child];
                    }
                    invokes[occurrence] |= hasCall;
                    calls[index] = hasCall;
                    disabledJoins[index] = system && (!node.Enabled || !node.GroupEnabled) &&
                        node.Inputs.SelectMany(static port => port).Distinct().Take(2).Count() > 1;
                    keep[index] = boundary || relay || hasCall;
                    foreach (var target in node.Outputs.SelectMany(static port => port)) {
                        outputs[index].Add(target);
                        inputs[target].Add(index);
                    }
                }
                // Validate original reachable edges before bypassing inactive nodes: a cycle cannot
                // become silently acceptable merely because this phase has no method on its nodes.
                var indegree = inputs.Select(static value => value.Count).ToArray();
                var ready = new SortedSet<int>(reachable.Where(index => indegree[index] == 0));
                var order = new List<int>();
                while (ready.Count != 0) {
                    var index = ready.Min;
                    ready.Remove(index);
                    order.Add(index);
                    foreach (var target in outputs[index]) if (--indegree[target] == 0) ready.Add(target);
                }
                if (order.Count != reachable.Count) throw new InvalidOperationException("Cycle in reachable sync graph");
                // Serialized fan-in alone is not a phase boundary. Follow the last active
                // producers through transparent nodes, and discard producers already ordered
                // before another producer. Thus empty branches and redundant ancestor edges
                // cannot manufacture a join in Awake/Start/etc. A real parallel join must
                // remain at the disabled node: the next system's original ports may have
                // only one input, so moving it there would lose its pre-apply boundary.
                var ancestors = new HashSet<int>[graph.Nodes.Count];
                var producers = new HashSet<int>[graph.Nodes.Count];
                foreach (var index in order) {
                    var before = ancestors[index] = new HashSet<int>();
                    var frontier = producers[index] = new HashSet<int>();
                    foreach (var parent in inputs[index]) {
                        before.Add(parent);
                        before.UnionWith(ancestors[parent]);
                        frontier.UnionWith(producers[parent]);
                    }
                    var candidates = frontier.ToArray();
                    foreach (var producer in candidates)
                        if (candidates.Any(other => other != producer && ancestors[other].Contains(producer)))
                            frontier.Remove(producer);
                    if (disabledJoins[index] && frontier.Count > 1) keep[index] = true;
                    if (calls[index]) { frontier.Clear(); frontier.Add(index); }
                }
                foreach (var index in order) {
                    if (keep[index]) continue;
                    foreach (var before in inputs[index])
                        foreach (var after in outputs[index]) { outputs[before].Add(after); inputs[after].Add(before); }
                    foreach (var before in inputs[index]) outputs[before].Remove(index);
                    foreach (var after in outputs[index]) inputs[after].Remove(index);
                    inputs[index].Clear();
                    outputs[index].Clear();
                }
                // Preserve the sync model's treatment of a redundant direct edge to the exit.
                foreach (var index in order)
                    if (outputs[index].Count > 1 && outputs[index].Remove(graph.Exit)) inputs[graph.Exit].Remove(index);
                foreach (var index in order) {
                    if (!keep[index]) { sync.Add((occurrence, index), (false, 0)); continue; }
                    var parents = new HashSet<int>();
                    var pending = new Stack<int>(inputs[index]);
                    if (index == graph.Entry) parents.Add(index);
                    while (pending.Count != 0) {
                        var parent = pending.Pop();
                        if (!parents.Add(parent) || parent == graph.Entry) continue;
                        foreach (var before in inputs[parent]) pending.Push(before);
                    }
                    var accumulator = -(long)inputs[index].Count;
                    foreach (var parent in parents)
                        accumulator += outputs[parent].Count - inputs[parent].Count;
                    var count = checked((int)accumulator);
                    sync.Add((occurrence, index), (count == 0, count));
                }
            }
            return true;
        } catch (InvalidOperationException exception) { reason = exception.Message; sync.Clear(); return false;
        } catch (OverflowException exception) { reason = exception.Message; sync.Clear(); return false; }
    }

    private static bool Derives(INamedTypeSymbol type, string name) {
        for (var current = type; current != null; current = current.BaseType)
            if (current.ToDisplayString() == name) return true;
        return false;
    }
}
