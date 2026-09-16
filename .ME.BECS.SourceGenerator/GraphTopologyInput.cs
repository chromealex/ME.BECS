using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class GraphTopologyInput {
    internal sealed class Occurrence {
        internal int Parent, ParentNode, GraphId, Capacity;
        internal readonly List<Node> Nodes = new();
    }
    internal sealed class Node {
        internal string Type = "", SystemType = "";
        internal bool Enabled, GroupEnabled;
        internal int SlotStart, SlotCount;
        internal bool Parallel;
        // null means unknown, not a disabled sync point.
        internal readonly Dictionary<int, (bool Sync, int Count, bool HasMethod)?> Phases = new();
        internal readonly List<int[]> Inputs = new(), Outputs = new();
    }
    internal readonly List<Occurrence> Occurrences = new();

    internal static bool TryParse(string payload, int rootId, out GraphTopologyInput? topology, out string reason) {
        topology = null;
        reason = "";
        try {
            var parsed = new GraphTopologyInput();
            var rows = payload.Split('\n');
            if (rows.Length < 3 || rows[0] != "ME.BECS.GraphTopology.v2" || rows[rows.Length - 1] != "") throw new FormatException("Invalid topology header/terminator");
            var children = new HashSet<(int Parent, int Node)>();
            for (var line = 1; line < rows.Length - 1; ++line) {
                var fields = rows[line].Split('\t');
                if (fields.Length < 3) throw new FormatException("Short topology record");
                var occurrenceId = Integer(fields[1], 0);
                if (fields[0] == "graph") {
                    if (fields.Length != 6 || occurrenceId != parsed.Occurrences.Count) throw new FormatException("Graph occurrence order/shape");
                    var occurrence = new Occurrence { Parent = Integer(fields[2], -1), ParentNode = Integer(fields[3], -1),
                        GraphId = Integer(fields[4], int.MinValue), Capacity = Integer(fields[5], 0) };
                    if (occurrenceId == 0) {
                        if (occurrence.Parent != -1 || occurrence.ParentNode != -1 || occurrence.GraphId != rootId) throw new FormatException("Root mismatch");
                    } else if (occurrence.Parent < 0 || occurrence.Parent >= occurrenceId || occurrence.ParentNode < 0 ||
                        occurrence.ParentNode >= parsed.Occurrences[occurrence.Parent].Nodes.Count || !children.Add((occurrence.Parent, occurrence.ParentNode)))
                        throw new FormatException("Invalid nested graph parent");
                    parsed.Occurrences.Add(occurrence);
                    continue;
                }
                if (occurrenceId >= parsed.Occurrences.Count) throw new FormatException("Unknown graph occurrence");
                var graph = parsed.Occurrences[occurrenceId];
                var nodeId = Integer(fields[2], 0);
                if (fields[0] == "node") {
                    if (fields.Length != 10 || nodeId != graph.Nodes.Count || nodeId >= graph.Capacity) throw new FormatException("Node order/shape");
                    var node = new Node { Type = Decode(fields[3]), Enabled = Boolean(fields[4]), GroupEnabled = Boolean(fields[5]), SystemType = Decode(fields[6]),
                        SlotStart = Integer(fields[7], 0), SlotCount = Integer(fields[8], 0), Parallel = Boolean(fields[9]) };
                    if (node.Type.Length == 0 || node.Type.Any(char.IsControl) || node.SystemType.Any(char.IsControl)) throw new FormatException("Invalid node type identity");
                    graph.Nodes.Add(node);
                    continue;
                }
                if (nodeId >= graph.Nodes.Count || fields.Length < 4) throw new FormatException("Unknown node");
                var target = graph.Nodes[nodeId];
                var index = Integer(fields[3], 0);
                if (fields[0] == "sync") {
                    if (index < 1 || index > 5 || target.Phases.ContainsKey(index)) throw new FormatException("Invalid/duplicate phase");
                    if (fields.Length == 5 && fields[4] == "unknown") target.Phases.Add(index, null);
                    else if (fields.Length == 7) target.Phases.Add(index, (Boolean(fields[4]), Integer(fields[5], 0), Boolean(fields[6])));
                    else throw new FormatException("Invalid phase shape");
                } else if (fields[0] == "input" || fields[0] == "output") {
                    var ports = fields[0] == "input" ? target.Inputs : target.Outputs;
                    if (index != ports.Count) throw new FormatException("Port order mismatch");
                    var edges = fields.Skip(4).Select(value => Integer(value, 0)).ToArray();
                    if (edges.Any(edge => edge >= graph.Capacity)) throw new FormatException("Edge endpoint outside occurrence");
                    ports.Add(edges);
                } else throw new FormatException("Unknown topology record: " + fields[0]);
            }
            if (parsed.Occurrences.Count == 0 || parsed.Occurrences.Any(graph => graph.Nodes.Count != graph.Capacity || graph.Nodes.Any(node => node.Phases.Count != 5)))
                throw new FormatException("Incomplete topology");
            topology = parsed;
            return true;
        } catch (FormatException exception) { reason = exception.Message; return false; }
    }

    internal bool BindSlots(IReadOnlyList<INamedTypeSymbol> slots, InputManifestTypes resolver, out string reason) {
        reason = "Topology/system slot mismatch";
        var occupied = new bool[slots.Count];
        var children = this.Occurrences.Skip(1).ToDictionary(graph => (graph.Parent, graph.ParentNode));
        for (var occurrence = 0; occurrence < this.Occurrences.Count; ++occurrence) {
            var graph = this.Occurrences[occurrence];
            var parent = occurrence == 0 ? null : this.Occurrences[graph.Parent].Nodes[graph.ParentNode];
            var cursor = parent?.SlotStart ?? 0;
            for (var index = 0; index < graph.Nodes.Count; ++index) {
                var node = graph.Nodes[index];
                if (node.SlotStart != cursor || (long)node.SlotStart + node.SlotCount > slots.Count) return false;
                cursor += node.SlotCount;
                if (node.SystemType.Length == 0) {
                    if (node.Parallel || (node.SlotCount != 0 && !children.ContainsKey((occurrence, index)))) return false;
                    continue;
                }
                if (children.ContainsKey((occurrence, index))) return false;
                var type = resolver.ResolveDefinition(node.SystemType, out _);
                if (type == null || (!type.IsGenericType && node.SlotCount != 1)) return false;
                var parallel = type.IsGenericType && type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "ME.BECS.SystemGenericParallelModeAttribute");
                if (parallel != node.Parallel) return false;
                for (var slot = node.SlotStart; slot < cursor; ++slot) {
                    if (occupied[slot] || !SymbolEqualityComparer.Default.Equals(slots[slot].OriginalDefinition, type.OriginalDefinition)) return false;
                    occupied[slot] = true;
                }
            }
            if (cursor != (parent == null ? slots.Count : parent.SlotStart + parent.SlotCount)) return false;
        }
        if (occupied.Any(value => !value)) return false;
        reason = "";
        return true;
    }

    private static int Integer(string text, int minimum) {
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) || value < minimum ||
            text != value.ToString(CultureInfo.InvariantCulture)) throw new FormatException("Invalid topology integer");
        return value;
    }
    private static bool Boolean(string text) => text == "1" ? true : text == "0" ? false : throw new FormatException("Invalid topology boolean");
    private static string Decode(string text) {
        var bytes = Convert.FromBase64String(text);
        if (Convert.ToBase64String(bytes) != text) throw new FormatException("Noncanonical topology string");
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new FormatException("Invalid topology UTF8"); }
    }
}
