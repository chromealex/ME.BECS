namespace ME.BECS.Editor.FeaturesGraph {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ME.BECS.Extensions.GraphProcessor;
    using ME.BECS.FeaturesGraph;
    using UnityEditor;
    using UnityEngine;

    /// <summary>Editor transactions over existing compiler input assets; no layout writes.</summary>
    internal static class FeaturesGraphStudioCommands {

        internal static NodePort Input(SerializableEdge edge) => edge.inputNode?.GetPort(edge.inputFieldName, edge.inputPortIdentifier);
        internal static NodePort Output(SerializableEdge edge) => edge.outputNode?.GetPort(edge.outputFieldName, edge.outputPortIdentifier);

        internal static bool WouldCycle(SystemsGraph graph, BaseNode source, BaseNode target, HashSet<SerializableEdge> removed = null) {
            var pending = new Stack<BaseNode>();
            var visited = new HashSet<BaseNode>();
            pending.Push(target);
            while (pending.Count > 0) {
                var node = pending.Pop();
                if (node == source) return true;
                if (!visited.Add(node)) continue;
                foreach (var edge in graph.edges)
                    if ((removed == null || !removed.Contains(edge)) && edge.outputNode == node && edge.inputNode != null) pending.Push(edge.inputNode);
            }
            return false;
        }

        internal static bool AreParallel(SystemsGraph graph, BaseNode a, BaseNode b) {
            HashSet<BaseNode> Following(BaseNode start) {
                var reached = new HashSet<BaseNode>(); var pending = new Stack<BaseNode>(); pending.Push(start);
                while (pending.Count > 0) {
                    var node = pending.Pop(); if (!reached.Add(node)) continue;
                    foreach (var edge in graph.edges) if (edge.outputNode == node && edge.inputNode != null) pending.Push(edge.inputNode);
                }
                return reached;
            }
            if (a == null || b == null || a == b) return false;
            var fromA = Following(a); var fromB = Following(b);
            // A direct/transitive dependency means a sequence, even if both share a later join.
            if (fromA.Contains(b) || fromB.Contains(a)) return false;
            return fromA.Overlaps(fromB);
        }

        private static void RemoveRedundantConnections(SystemsGraph graph) {
            // A dependency already enforced by another path needs no direct edge.
            // Disconnect sequentially so each decision uses the remaining graph.
            foreach (var edge in graph.edges.ToArray()) {
                if (edge.outputNode == null || edge.inputNode == null) continue;
                if (WouldCycle(graph, edge.inputNode, edge.outputNode, new HashSet<SerializableEdge> { edge }))
                    graph.Disconnect(edge);
            }
        }

        internal static bool Connect(SystemsGraph graph, NodePort input, NodePort output, out string error, IEnumerable<SerializableEdge> replace = null) {
            error = null;
            var requested = replace?.ToList() ?? new List<SerializableEdge>();
            if (requested.Any(edge => !graph.edges.Contains(edge))) { error = "Connection is no longer available."; return false; }
            if (input == null || output == null || !graph.nodes.Contains(input.owner) || !graph.nodes.Contains(output.owner)) {
                error = "Port is no longer available."; return false;
            }
            if (!Compatible(input, output)) { error = "Port types are incompatible."; return false; }
            if (graph.edges.Any(edge => Input(edge) == input && Output(edge) == output)) {
                error = "This connection already exists."; return false;
            }
            var replaced = new HashSet<SerializableEdge>(graph.edges.Where(edge =>
                (!input.portData.acceptMultipleEdges && Input(edge) == input) || (!output.portData.acceptMultipleEdges && Output(edge) == output)));
            foreach (var edge in requested) {
                // Preserve a sibling branch when both destinations independently
                // converge later. Sequential rewiring still replaces its old edge.
                var sibling = Output(edge) == output && output.portData.acceptMultipleEdges &&
                              AreParallel(graph, edge.inputNode, input.owner);
                if (!sibling) replaced.Add(edge);
            }
            if (WouldCycle(graph, output.owner, input.owner, replaced)) { error = "Connection rejected: dependency cycle."; return false; }
            Undo.RegisterCompleteObjectUndo(graph, "Connect graph nodes");
            foreach (var edge in replaced) graph.Disconnect(edge);
            graph.Connect(input, output);
            RemoveRedundantConnections(graph);
            return true;
        }

        internal static BaseNode Insert(SystemsGraph graph, Type type, BaseNode anchor, bool parallel, out string error) {
            return InsertCore(graph, type, anchor, parallel, null, out error);
        }

        internal static BaseNode InsertOnEdge(SystemsGraph graph, Type type, SerializableEdge edge, out string error) {
            if (edge == null || !graph.edges.Contains(edge)) { error = "Connection is no longer available."; return null; }
            return InsertCore(graph, type, edge.outputNode, false, edge, out error);
        }

        private static BaseNode InsertCore(SystemsGraph graph, Type type, BaseNode anchor, bool parallel, SerializableEdge selectedEdge, out string error) {
            error = null;
            var prototype = BaseNode.CreateFromType(type, Vector2.zero);
            if (prototype == null) { error = "Unsupported node type."; return null; }
            prototype.InitializePorts();
            var removed = new HashSet<SerializableEdge>();
            var links = new List<(NodePort input, NodePort output)>();
            if (anchor != null) {
                if (!graph.nodes.Contains(anchor)) { error = "Selected node is no longer available."; return null; }
                if (prototype.inputPorts.Count != 1 || prototype.outputPorts.Count != 1 || anchor.outputPorts.Count != 1) {
                    error = "Automatic insertion requires one input/output boundary. Connect these ports manually."; return null;
                }
                var output = anchor.outputPorts[0];
                var outgoing = graph.edges.Where(edge => Output(edge) == output && (selectedEdge == null || edge == selectedEdge)).ToList();
                if (parallel) {
                    var incoming = graph.edges.Where(edge => edge.inputNode == anchor).ToList();
                    if (incoming.Count == 0 || outgoing.Count == 0) {
                        error = "Select a node with incoming and outgoing dependencies to add a parallel branch."; return null;
                    }
                    foreach (var edge in incoming) links.Add((prototype.inputPorts[0], Output(edge)));
                } else {
                    foreach (var edge in outgoing) removed.Add(edge);
                    links.Add((prototype.inputPorts[0], output));
                }
                foreach (var edge in outgoing) links.Add((Input(edge), prototype.outputPorts[0]));
            } else if (parallel) { error = "Select a node to add a parallel branch."; return null; }
            if (!ValidateLinks(graph, links, removed, out error)) return null;
            // Validate everything before changing the asset. All rewiring is one Undo step.
            Undo.RegisterCompleteObjectUndo(graph, parallel ? "Add parallel graph node" : "Insert graph node");
            var node = graph.AddNode(BaseNode.CreateFromType(type, Vector2.zero));
            if (anchor != null && !string.IsNullOrEmpty(anchor.groupGUID)) {
                var group = graph.groups.FirstOrDefault(value => value.GUID == anchor.groupGUID);
                if (group != null) { node.groupGUID = group.GUID; group.innerNodeGUIDs.Add(node.GUID); }
            }
            foreach (var edge in removed) graph.Disconnect(edge);
            NodePort Actual(NodePort port) => port.owner == prototype ? node.GetPort(port.fieldName, port.portData.identifier) : port;
            foreach (var link in links.Distinct()) graph.Connect(Actual(link.input), Actual(link.output), false);
            RemoveRedundantConnections(graph);
            return node;
        }

        private static bool Compatible(NodePort input, NodePort output) => input != null && output != null &&
            BaseGraph.TypesAreConnectable(output.portData.displayType ?? output.fieldInfo.FieldType,
                input.portData.displayType ?? input.fieldInfo.FieldType);

        private static bool ValidateLinks(SystemsGraph graph, List<(NodePort input, NodePort output)> links,
            HashSet<SerializableEdge> removed, out string error) {
            error = null;
            var counts = new Dictionary<NodePort, int>();
            void Count(NodePort port) { if (port != null) counts[port] = counts.TryGetValue(port, out var count) ? count + 1 : 1; }
            foreach (var edge in graph.edges) {
                if (removed.Contains(edge)) continue;
                Count(Input(edge)); Count(Output(edge));
            }
            foreach (var link in links.Distinct()) {
                if (!Compatible(link.input, link.output)) { error = "Automatic connection has incompatible or missing ports."; return false; }
                Count(link.input); Count(link.output);
            }
            if (counts.Any(pair => pair.Value > 1 && !pair.Key.portData.acceptMultipleEdges)) {
                error = "A port accepts only one connection; automatic rewiring would overwrite an existing link."; return false;
            }
            return true;
        }

        internal static bool Delete(SystemsGraph graph, BaseNode node, out string error) {
            error = null;
            if (node == null || !node.deletable || !graph.nodes.Contains(node)) {
                error = "Node cannot be deleted."; return false;
            }
            var removed = new HashSet<SerializableEdge>(graph.edges.Where(edge => edge.inputNode == node || edge.outputNode == node));
            var incoming = removed.Where(edge => edge.inputNode == node).Select(Output).Distinct().ToList();
            var outgoing = removed.Where(edge => edge.outputNode == node).Select(Input).Distinct().ToList();
            var links = new List<(NodePort input, NodePort output)>();
            foreach (var output in incoming) foreach (var input in outgoing) {
                if (graph.edges.Any(edge => !removed.Contains(edge) && Input(edge) == input && Output(edge) == output)) continue;
                links.Add((input, output));
            }
            if (!ValidateLinks(graph, links, removed, out error)) return false;
            foreach (var link in links) {
                if (WouldCycle(graph, link.output.owner, link.input.owner, removed)) {
                    error = "Deletion rejected: reconnecting neighbours would create a dependency cycle."; return false;
                }
            }
            // Validate before mutation; removal and all replacement links are one Undo step.
            Undo.RegisterCompleteObjectUndo(graph, "Delete graph node and reconnect neighbours");
            foreach (var edge in removed) graph.Disconnect(edge);
            foreach (var group in graph.groups) group.innerNodeGUIDs.Remove(node.GUID);
            graph.RemoveNode(node);
            foreach (var link in links) graph.Connect(link.input, link.output, false);
            RemoveRedundantConnections(graph);
            return true;
        }
    }
}
