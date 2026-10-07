using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace ME.BECS.SourceGenerator;

// A conservative exception graph for reservation bounds, NOT execution ordering
// or synchronization. Every reachable protected block may throw to any enclosing
// catch (exception types are deliberately not used to remove possible targets).
internal static class ExceptionalCountEdges {
    internal readonly struct Edge {
        internal readonly int Target;
        internal readonly ControlFlowRegion[] Unwind;
        internal Edge(int target, ControlFlowRegion[] unwind) { this.Target = target; this.Unwind = unwind; }
    }

    internal static Dictionary<int, List<Edge>>? Create(ControlFlowGraph graph, CancellationToken cancellation) {
        var result = new Dictionary<int, List<Edge>>();
        var work = 0;
        foreach (var block in graph.Blocks) {
            cancellation.ThrowIfCancellationRequested();
            if (!block.IsReachable) continue;
            var unwind = new List<ControlFlowRegion>();
            for (var region = block.EnclosingRegion; region != null && region != graph.Root; region = region.EnclosingRegion) {
                if (++work > 200000) return null;
                if (region.Kind != ControlFlowRegionKind.Try) continue;
                var group = region.EnclosingRegion;
                if (group?.Kind == ControlFlowRegionKind.TryAndFinally) {
                    var final = group.NestedRegions.SingleOrDefault(nested => nested.Kind == ControlFlowRegionKind.Finally);
                    if (final == null) return null;
                    unwind.Add(final);
                } else if (group?.Kind == ControlFlowRegionKind.TryAndCatch) {
                    foreach (var handler in group.NestedRegions) {
                        if (++work > 200000) return null;
                        if (handler == region) continue;
                        // Filters run during the first exception pass, before stack
                        // unwinding, and can run again if a callee's finally throws.
                        // They require a separate multiplicity proof, not catch edges.
                        if (handler.Kind != ControlFlowRegionKind.Catch) return null;
                        var target = handler.FirstBlockOrdinal;
                        if (!graph.Blocks[target].IsReachable) continue;
                        work += unwind.Count;
                        if (work > 200000) return null;
                        if (!result.TryGetValue(block.Ordinal, out var edges)) result.Add(block.Ordinal, edges = new());
                        edges.Add(new Edge(target, unwind.ToArray()));
                    }
                }
            }
        }
        return result;
    }
}
