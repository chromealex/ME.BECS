using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace ME.BECS.SourceGenerator;

// Lexical destinations for an exception raised at a particular CFG block. A route
// records cleanup BEFORE entering the selected catch, or before escaping the CFG.
// Exception types are not used to prune alternatives. Consumers must propagate
// the state at the throwing operation, not a normal-successor/block-exit state.
internal static class ExceptionalFlowRoutes {
    internal readonly struct Route {
        internal readonly int? Target;
        internal readonly ControlFlowRegion[] Unwind;
        internal readonly ControlFlowRegion? Filter;
        internal Route(int? target, ControlFlowRegion[] unwind, ControlFlowRegion? filter = null) {
            this.Target = target; this.Unwind = unwind; this.Filter = filter;
        }
    }

    // Filter-capable consumers must execute these routes as an ORDERED search:
    // rejected/throwing filters retain their effects for subsequent candidates.
    // Unwind executes only after a handler is selected or the search escapes.
    // Consumers without a first-pass interpreter must keep the default false.
    internal static Route[][]? Create(ControlFlowGraph graph, CancellationToken cancellation, bool includeFilters = false) {
        var result = new Route[graph.Blocks.Length][];
        var work = 0;
        foreach (var block in graph.Blocks) {
            cancellation.ThrowIfCancellationRequested();
            var routes = new List<Route>();
            var unwind = new List<ControlFlowRegion>();
            // Root.EnclosingRegion of a nested function can belong to its lexical
            // caller. Those handlers use another ordinal space and execute in the
            // caller's frame, not when interpreting the nested function's body.
            for (var region = block.EnclosingRegion; region != null && region != graph.Root; region = region.EnclosingRegion) {
                if (++work > 200000) return null;
                if (region.Kind != ControlFlowRegionKind.Try) continue;
                var group = region.EnclosingRegion;
                if (group?.Kind == ControlFlowRegionKind.TryAndFinally) {
                    var cleanup = group.NestedRegions.SingleOrDefault(child => child.Kind == ControlFlowRegionKind.Finally);
                    if (cleanup == null) return null;
                    unwind.Add(cleanup);
                } else if (group?.Kind == ControlFlowRegionKind.TryAndCatch) {
                    foreach (var handler in group.NestedRegions) {
                        if (++work > 200000) return null;
                        if (handler == region) continue;
                        ControlFlowRegion? filter = null;
                        var body = handler;
                        if (handler.Kind == ControlFlowRegionKind.FilterAndHandler && includeFilters) {
                            filter = handler.NestedRegions.SingleOrDefault(child => child.Kind == ControlFlowRegionKind.Filter);
                            body = handler.NestedRegions.SingleOrDefault(child => child.Kind == ControlFlowRegionKind.Catch);
                            if (filter == null || body == null) return null;
                        } else if (handler.Kind != ControlFlowRegionKind.Catch) return null;
                        if (!graph.Blocks[(filter ?? body).FirstBlockOrdinal].IsReachable) continue;
                        work += unwind.Count;
                        if (work > 200000) return null;
                        routes.Add(new Route(body.FirstBlockOrdinal, unwind.ToArray(), filter));
                    }
                }
            }
            work += unwind.Count;
            if (work > 200000) return null;
            routes.Add(new Route(null, unwind.ToArray()));
            result[block.Ordinal] = routes.ToArray();
        }
        return result;
    }
}
