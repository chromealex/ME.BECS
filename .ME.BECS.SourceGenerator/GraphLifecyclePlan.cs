using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Typed lifecycle IR. Handle references are previous step indices; -1 is the caller's input.
// An empty dependency list means default(JobHandle), not the caller's input.
internal sealed class GraphLifecyclePlan {
    internal sealed class Step {
        internal int Occurrence, Node, SlotStart, SlotCount;
        internal int[] Dependencies = Array.Empty<int>();
        internal bool Invoke, Generic, Parallel, PreApply, Apply, Burst;
    }
    internal readonly List<Step> Steps = new();
    internal int Result = -1;
    internal string SyncDifferences = "";

    internal static bool TryCreate(GraphTopologyInput topology, InputManifestTypes resolver, string phase,
        int phaseIndex, bool flatQueries, out GraphLifecyclePlan? plan, out string reason) {
        plan = null;
        reason = "";
        var result = new GraphLifecyclePlan();
        if (!GraphSyncAnalysis.TryCreate(topology, resolver, phase, out var freshSync, out reason)) return false;
        var syncDifferences = new StringBuilder();
        foreach (var pair in freshSync.OrderBy(static pair => pair.Key.Graph).ThenBy(static pair => pair.Key.Node)) {
            topology.Occurrences[pair.Key.Graph].Nodes[pair.Key.Node].Phases.TryGetValue(phaseIndex, out var saved);
            if (saved != null && saved.Value.Sync == pair.Value.Sync && saved.Value.Count == pair.Value.Count) continue;
            syncDifferences.Append(pair.Key.Graph.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(pair.Key.Node.ToString(CultureInfo.InvariantCulture)).Append("\tfresh=")
                .Append(pair.Value.Sync ? "1" : "0").Append(':').Append(pair.Value.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\tsaved=").Append(saved == null ? "unknown" : (saved.Value.Sync ? "1:" : "0:") + saved.Value.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        result.SyncDifferences = syncDifferences.ToString();
        var children = new Dictionary<(int Graph, int Node), int>();
        for (var index = 1; index < topology.Occurrences.Count; ++index) {
            var occurrence = topology.Occurrences[index];
            children.Add((occurrence.Parent, occurrence.ParentNode), index);
        }
        var kinds = new Dictionary<(int Graph, int Node), string>();
        string Kind((int Graph, int Node) key) {
            if (kinds.TryGetValue(key, out var kind)) return kind;
            var type = resolver.ResolveDefinition(topology.Occurrences[key.Graph].Nodes[key.Node].Type, out _);
            kind = "unknown";
            for (var current = type; current != null; current = current.BaseType) {
                var name = current.ToDisplayString();
                if (name == "ME.BECS.FeaturesGraph.Nodes.StartNode") { kind = "start"; break; }
                if (name == "ME.BECS.FeaturesGraph.Nodes.ExitNode") { kind = "exit"; break; }
                if (name == "ME.BECS.FeaturesGraph.Nodes.SystemNode") { kind = "system"; break; }
                if (name == "ME.BECS.FeaturesGraph.Nodes.GraphNode") { kind = "graph"; break; }
                if (name == "ME.BECS.Extensions.GraphProcessor.RelayNode") { kind = "relay"; break; }
            }
            kinds.Add(key, kind);
            return kind;
        }
        bool Sync((int Graph, int Node) key) {
            if (!freshSync.TryGetValue(key, out var data)) throw new InvalidOperationException("Missing fresh sync state at " + key);
            return data.Sync;
        }
        bool Apply((int Graph, int Node) key) {
            if (!Sync(key)) return false;
            if (!flatQueries) {
                var occurrence = topology.Occurrences[key.Graph];
                while (occurrence.Parent >= 0) {
                    if (!Sync((occurrence.Parent, occurrence.ParentNode))) return false;
                    occurrence = topology.Occurrences[occurrence.Parent];
                }
            }
            return true;
        }
        try {
            if (topology.Occurrences.Count == 0) throw new InvalidOperationException("Missing root occurrence");
            var root = topology.Occurrences[0];
            if (root.Entry < 0) throw new InvalidOperationException("Missing root entry");
            if (root.Exit < 0) throw new InvalidOperationException("Missing root exit");
            var queue = new Queue<(int Graph, int Node)>();
            var queued = new HashSet<(int Graph, int Node)>();
            var entered = new HashSet<(int Graph, int Node)>();
            var completed = new Dictionary<(int Graph, int Node), int>();
            void Enqueue((int Graph, int Node) key) {
                if (!completed.ContainsKey(key) && queued.Add(key)) queue.Enqueue(key);
            }
            void Outputs((int Graph, int Node) key) {
                foreach (var port in topology.Occurrences[key.Graph].Nodes[key.Node].Outputs)
                    foreach (var target in port) Enqueue((key.Graph, target));
            }
            bool Dependencies((int Graph, int Node) key, out int[] dependencies) {
                var occurrence = topology.Occurrences[key.Graph];
                if (key.Node == occurrence.Entry && occurrence.Parent >= 0)
                    return Dependencies((occurrence.Parent, occurrence.ParentNode), out dependencies);
                var node = occurrence.Nodes[key.Node];
                if (node.Inputs.Count == 0) { dependencies = new[] { -1 }; return true; }
                var values = new List<int>();
                foreach (var source in node.Inputs.SelectMany(static port => port).Distinct()) {
                    var dependency = (key.Graph, source);
                    if (Kind(dependency) == "start" && source != occurrence.Entry) continue;
                    if (!completed.TryGetValue(dependency, out var handle)) { dependencies = Array.Empty<int>(); return false; }
                    values.Add(handle);
                }
                dependencies = values.ToArray();
                return true;
            }
            Enqueue((0, root.Entry));
            var stalled = 0;
            while (queue.Count != 0) {
                var key = queue.Dequeue();
                queued.Remove(key);
                var occurrence = topology.Occurrences[key.Graph];
                var node = occurrence.Nodes[key.Node];
                var kind = Kind(key);
                if (kind == "unknown") throw new InvalidOperationException("Unsupported node kind at " + key);
                var step = new Step { Occurrence = key.Graph, Node = key.Node, SlotStart = node.SlotStart, SlotCount = node.SlotCount };
                var ready = Dependencies(key, out step.Dependencies);
                if (entered.Contains(key)) {
                    var child = children[key];
                    var childExit = topology.Occurrences[child].Exit;
                    ready = completed.TryGetValue((child, childExit), out var childResult);
                    if (ready) step.Dependencies = new[] { childResult };
                }
                if (!ready) {
                    Enqueue(key);
                    if (++stalled >= queue.Count) throw new InvalidOperationException("Cycle or unreachable dependency at " + key);
                    continue;
                }
                stalled = 0;
                if (kind == "graph" && node.Enabled && node.GroupEnabled && !entered.Contains(key)) {
                    if (!children.TryGetValue(key, out var child)) throw new InvalidOperationException("Missing nested occurrence at " + key);
                    var nested = topology.Occurrences[child];
                    if (nested.Entry < 0 || nested.Exit < 0) throw new InvalidOperationException("Missing nested boundary at " + key);
                    entered.Add(key);
                    Enqueue((child, nested.Entry));
                    Enqueue(key);
                    Outputs(key);
                    continue;
                }
                if (kind == "system" && node.Enabled && node.GroupEnabled && node.SystemType.Length != 0) {
                    var type = resolver.ResolveDefinition(node.SystemType, out _) ?? throw new InvalidOperationException("Unresolved system at " + key);
                    var contract = type.AllInterfaces.FirstOrDefault(i => i.ToDisplayString() == "ME.BECS.I" + phase);
                    if (contract != null) {
                        var member = contract.GetMembers("On" + phase).OfType<IMethodSymbol>().SingleOrDefault();
                        var method = member == null ? null : type.FindImplementationForInterfaceMember(member) as IMethodSymbol;
                        if (method == null) throw new InvalidOperationException("Unresolved lifecycle implementation at " + key);
                        step.Invoke = true;
                        step.Generic = type.IsGenericType;
                        step.Parallel = node.Parallel;
                        step.Burst = !HasAttribute(method, "ME.BECS.WithoutBurstAttribute") &&
                            (HasAttribute(type, "Unity.Burst.BurstCompileAttribute") || HasAttribute(method, "Unity.Burst.BurstCompileAttribute"));
                        step.PreApply = Sync(key) && node.Inputs.SelectMany(static port => port).Take(2).Count() > 1;
                        step.Apply = Apply(key);
                    }
                } else if (kind == "start" || kind == "relay" || kind == "system") {
                    // Fresh phase projection determines pass-through batch boundaries; a live
                    // system without this phase takes the pure pass-through branch above.
                    step.Apply = Apply(key);
                }
                completed.Add(key, result.Steps.Count);
                result.Steps.Add(step);
                Outputs(key);
            }
            if (!completed.TryGetValue((0, root.Exit), out result.Result)) throw new InvalidOperationException("Root exit is unreachable");
            // The transitional emitter removes the entire phase when it contains no lifecycle calls.
            if (!result.Steps.Any(static step => step.Invoke)) { result.Steps.Clear(); result.Result = -1; }
            if (!result.ValidateCompletion(out var completionError)) throw new InvalidOperationException(completionError);
            plan = result;
            return true;
        } catch (InvalidOperationException exception) { reason = exception.Message; return false; }
    }

    private static bool HasAttribute(ISymbol symbol, string name) {
        foreach (var attribute in symbol.GetAttributes())
            for (var type = attribute.AttributeClass; type != null; type = type.BaseType)
                if (type.ToDisplayString() == name) return true;
        return false;
    }

    internal bool ValidateCompletion(out string reason) {
        reason = "";
        if (this.Result < -1 || this.Result >= this.Steps.Count) { reason = "Invalid lifecycle result handle"; return false; }
        for (var index = 0; index < this.Steps.Count; ++index)
            foreach (var dependency in this.Steps[index].Dependencies)
                if (dependency < -1 || dependency >= index) { reason = "Forward or invalid lifecycle dependency at step " + index; return false; }
        var joined = new bool[this.Steps.Count];
        if (this.Result >= 0) joined[this.Result] = true;
        // All references point backward, so one descending pass computes the result's ancestors.
        for (var index = this.Steps.Count - 1; index >= 0; --index) {
            if (!joined[index]) continue;
            foreach (var dependency in this.Steps[index].Dependencies)
                if (dependency >= 0) joined[dependency] = true;
        }
        for (var index = 0; index < this.Steps.Count; ++index) {
            var step = this.Steps[index];
            if (joined[index] || (!step.Invoke && !step.PreApply && !step.Apply)) continue;
            reason = "Root completion does not join work at occurrence " + step.Occurrence.ToString(CultureInfo.InvariantCulture) +
                ", node " + step.Node.ToString(CultureInfo.InvariantCulture);
            return false;
        }
        return true;
    }

    internal string Serialize() {
        var report = new StringBuilder("ME.BECS.GraphLifecyclePlan.v1\n");
        for (var index = 0; index < this.Steps.Count; ++index) {
            var step = this.Steps[index];
            report.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(step.Occurrence.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(step.Node.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(string.Join(",", step.Dependencies.Select(value => value.ToString(CultureInfo.InvariantCulture))))
                .Append('\t').Append(step.Invoke ? "invoke" : "pass").Append('\t').Append(step.SlotStart.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(step.SlotCount.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(step.Generic ? (step.Parallel ? "parallel" : "sequential") : "ordinary")
                .Append('\t').Append(step.PreApply ? "1" : "0").Append('\t').Append(step.Apply ? "1" : "0")
                .Append('\t').Append(step.Burst ? "1" : "0").Append('\n');
        }
        return report.Append("result\t").Append(this.Result.ToString(CultureInfo.InvariantCulture)).Append('\n').ToString();
    }
}
