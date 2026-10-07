using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

// Reservation multiplicity only, not execution/synchronization ordering.
// A filter may run again when unwinding throws, including inside an imported
// callee. Such creation sites use the existing runtime-enforced per-job maximum.
internal static class ExceptionalFilterCountEdges {
    internal static Dictionary<int, List<ExceptionalCountEdges.Edge>>? Create(
        ControlFlowGraph graph, CancellationToken cancellation, out HashSet<ControlFlowRegion> repeatable) {
        repeatable = new();
        var routes = ExceptionalFlowRoutes.Create(graph, cancellation, includeFilters: true);
        if (routes == null) return null;
        var result = new Dictionary<int, List<ExceptionalCountEdges.Edge>>();
        var filters = new HashSet<ControlFlowRegion>();
        var work = 0;
        bool Add(int source, int target, ControlFlowRegion[] unwind) {
            if (++work > 200000) return false;
            if (!graph.Blocks[source].IsReachable || !graph.Blocks[target].IsReachable) return true;
            if (!result.TryGetValue(source, out var edges)) result.Add(source, edges = new());
            edges.Add(new ExceptionalCountEdges.Edge(target, unwind));
            return true;
        }
        bool AddCandidate(int source, ExceptionalFlowRoutes.Route candidate) {
            // Direct catch edges conservatively include the accepted path; the
            // additional filter edge accounts for calls during first-pass search.
            if (candidate.Target.HasValue && !Add(source, candidate.Target.Value, candidate.Unwind)) return false;
            return candidate.Filter == null || Add(source, candidate.Filter.FirstBlockOrdinal, Array.Empty<ControlFlowRegion>());
        }
        foreach (var block in graph.Blocks) {
            cancellation.ThrowIfCancellationRequested();
            if (!block.IsReachable) continue;
            var candidates = routes[block.Ordinal];
            for (var index = 0; index < candidates.Length; ++index) {
                var candidate = candidates[index];
                if (!AddCandidate(block.Ordinal, candidate)) return null;
                var filter = candidate.Filter;
                if (filter == null) continue;
                filters.Add(filter);
                // False/throwing filters can continue to later sibling or outer
                // handlers. Union origins: each can require different cleanup.
                for (var ordinal = filter.FirstBlockOrdinal; ordinal <= filter.LastBlockOrdinal; ++ordinal)
                    for (var next = index + 1; next < candidates.Length; ++next)
                        if (!AddCandidate(ordinal, candidates[next])) return null;
            }
        }
        var proofs = new Dictionary<ControlFlowRegion, bool>();
        foreach (var filter in filters) {
            cancellation.ThrowIfCancellationRequested();
            var group = filter.EnclosingRegion?.EnclosingRegion;
            var body = group?.NestedRegions.SingleOrDefault(region => region.Kind == ControlFlowRegionKind.Try);
            if (body == null) return null;
            if (!proofs.TryGetValue(body, out var oneShot)) {
                oneShot = true;
                var regions = new Stack<ControlFlowRegion>();
                regions.Push(body);
                while (regions.Count > 0 && oneShot) {
                    var region = regions.Pop();
                    if (++work > 200000) return null;
                    if (region.Kind == ControlFlowRegionKind.Finally &&
                        graph.Blocks[region.FirstBlockOrdinal].IsReachable) oneShot = false;
                    foreach (var nested in region.NestedRegions) regions.Push(nested);
                }
                // Without cleanup or a call that could contain cleanup, leaving
                // the protected body cannot re-enter its filter during unwind.
                // Cyclic handler reentry is classified separately by the CFG SCC.
                var pending = new Stack<Microsoft.CodeAnalysis.IOperation>();
                for (var ordinal = body.FirstBlockOrdinal; ordinal <= body.LastBlockOrdinal && oneShot; ++ordinal) {
                    if (!graph.Blocks[ordinal].IsReachable) continue;
                    foreach (var operation in graph.Blocks[ordinal].Operations) pending.Push(operation);
                    if (graph.Blocks[ordinal].BranchValue is { } branch) pending.Push(branch);
                    while (pending.Count > 0 && oneShot) {
                        cancellation.ThrowIfCancellationRequested();
                        if (++work > 200000) return null;
                        var operation = pending.Pop();
                        if (operation is ILocalFunctionOperation or IAnonymousFunctionOperation or IFlowAnonymousFunctionOperation) continue;
                        if (operation is IInvocationOperation or IPropertyReferenceOperation or IObjectCreationOperation or
                            ITypeParameterObjectCreationOperation or IEventAssignmentOperation or IInterpolatedStringOperation or
                            IDynamicInvocationOperation or IDynamicMemberReferenceOperation or IDynamicIndexerAccessOperation or
                            IDynamicObjectCreationOperation ||
                            // A static field can trigger type initialization; string
                            // concatenation can call user ToString without an explicit
                            // invocation node. Both can hide cleanup in another frame.
                            operation is IFieldReferenceOperation { Field: { IsStatic: true, IsConst: false } } ||
                            operation is IBinaryOperation { Type: { SpecialType: Microsoft.CodeAnalysis.SpecialType.System_String } } ||
                            operation is IConversionOperation { OperatorMethod: not null } or IBinaryOperation { OperatorMethod: not null } or
                                IUnaryOperation { OperatorMethod: not null } or IIncrementOrDecrementOperation { OperatorMethod: not null } or
                                ICompoundAssignmentOperation { OperatorMethod: not null }) oneShot = false;
                        foreach (var child in operation.ChildOperations) pending.Push(child);
                    }
                }
                proofs.Add(body, oneShot);
            }
            if (!oneShot) repeatable.Add(filter);
        }
        return result;
    }
}
