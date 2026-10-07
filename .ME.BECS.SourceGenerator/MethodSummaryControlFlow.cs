using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

internal static class MethodSummaryControlFlow {
    // Certifies the unordered union of reachable effects, including exception
    // regions. It does NOT certify exceptional edges, loop counts or execution order.
    internal const string EffectUnionSchema = "effect-union-schema=1";

    // Old imported producer proof for methods without catches or filters.
    internal const string FinallyCountsSchema = "finally-count-schema=1";
    // Includes exceptional transfers to catch handlers, but not filter reentry.
    internal const string ExceptionCountsSchema = "exception-count-schema=1";
    // Filter replay without a local cycle uses runtime-bounded repeatable sites.
    internal const string FilterCountsSchema = "filter-count-schema=1";

    internal static bool Visit(IOperation? operation, Action<IOperation, bool> visit, ISet<string> gaps,
        System.Threading.CancellationToken cancellation, out bool exceptionCounts, out bool filterCounts,
        Action? beginBlock = null, Action<ControlFlowGraph>? analyzeGraph = null) {
        exceptionCounts = false;
        filterCounts = false;
        ControlFlowGraph? graph;
        try { graph = GetGraph(operation, cancellation); }
        catch (ArgumentException) { gaps.Add("ControlFlowGraphUnavailable"); return false; }
        catch (InvalidOperationException) { gaps.Add("ControlFlowGraphUnavailable"); return false; }
        if (graph == null) return false;
        var blocks = graph.Blocks;
        if (blocks.Length > 20000) { gaps.Add("ControlFlowGraphSizeLimit"); return false; }
        var hasFinally = false;
        var hasCatch = false;
        var hasFilter = false;
        var regions = new Stack<ControlFlowRegion>();
        regions.Push(graph.Root);
        while (regions.Count != 0) {
            var region = regions.Pop();
            hasFinally |= region.Kind == ControlFlowRegionKind.Finally;
            hasCatch |= region.Kind == ControlFlowRegionKind.Catch;
            hasFilter |= region.Kind == ControlFlowRegionKind.Filter;
            if (region.Kind is ControlFlowRegionKind.Finally or ControlFlowRegionKind.Filter or ControlFlowRegionKind.Catch)
                gaps.Add("ExceptionControlFlow"); // Still unresolved for synchronization/old consumers.
            foreach (var nested in region.NestedRegions) regions.Push(nested);
        }
        var repeatableFilters = new HashSet<ControlFlowRegion>();
        var exceptional = hasFilter ? ExceptionalFilterCountEdges.Create(graph, cancellation, out repeatableFilters) :
            hasCatch ? ExceptionalCountEdges.Create(graph, cancellation) : null;
        exceptionCounts = !hasCatch || exceptional != null;
        filterCounts = hasFilter;
        var edges = new int[blocks.Length][];
        var reverse = new List<int>[blocks.Length];
        for (var i = 0; i < blocks.Length; ++i) reverse[i] = new List<int>();
        for (var i = 0; i < blocks.Length; ++i) {
            cancellation.ThrowIfCancellationRequested();
            if (!blocks[i].IsReachable) { edges[i] = Array.Empty<int>(); continue; }
            var targets = new[] { blocks[i].FallThroughSuccessor?.Destination, blocks[i].ConditionalSuccessor?.Destination }
                .Where(static b => b != null && b.IsReachable).Select(static b => b!.Ordinal);
            if (exceptional != null && exceptional.TryGetValue(i, out var handlers)) targets = targets.Concat(handlers.Select(static edge => edge.Target));
            edges[i] = targets.Distinct().ToArray();
            foreach (var target in edges[i]) reverse[target].Add(i);
        }
        // Iterative Kosaraju avoids stack overflow on large generated methods. A block is
        // repeating exactly when it belongs to a nontrivial SCC or has a self edge.
        var visited = new bool[blocks.Length];
        var order = new List<int>();
        var pending = new Stack<(int Block, bool Exit)>();
        for (var start = 0; start < blocks.Length; ++start) {
            if (!blocks[start].IsReachable || visited[start]) continue;
            pending.Push((start, false));
            while (pending.Count != 0) {
                cancellation.ThrowIfCancellationRequested();
                var frame = pending.Pop();
                if (frame.Exit) { order.Add(frame.Block); continue; }
                if (visited[frame.Block]) continue;
                visited[frame.Block] = true;
                pending.Push((frame.Block, true));
                for (var j = edges[frame.Block].Length - 1; j >= 0; --j) pending.Push((edges[frame.Block][j], false));
            }
        }
        Array.Clear(visited, 0, visited.Length);
        var loops = new bool[blocks.Length];
        var components = hasFinally ? new int[blocks.Length] : null;
        var nodes = new Stack<int>();
        for (var i = order.Count - 1; i >= 0; --i) {
            if (visited[order[i]]) continue;
            var component = new List<int>();
            nodes.Push(order[i]);
            while (nodes.Count != 0) {
                var node = nodes.Pop();
                if (visited[node]) continue;
                visited[node] = true;
                component.Add(node);
                foreach (var predecessor in reverse[node]) nodes.Push(predecessor);
            }
            if (components != null) foreach (var node in component) components[node] = component[0];
            if (component.Count > 1 || edges[component[0]].Contains(component[0])) foreach (var node in component) loops[node] = true;
        }
        // Roslyn represents finally execution on the leave edge, not as normal
        // successors of the try blocks. Merely checking whether the source block
        // repeats would wrongly mark `try { while (...) {} } finally { New(); }`.
        // Both ends must belong to the SAME cyclic SCC. A repeating outer finally
        // also repeats its nested regions, even when their normal CFG is acyclic.
        if (components != null) {
            var repeatingFinally = new HashSet<ControlFlowRegion>();
            foreach (var block in blocks) {
                cancellation.ThrowIfCancellationRequested();
                if (!block.IsReachable || !loops[block.Ordinal]) continue;
                foreach (var branch in new[] { block.FallThroughSuccessor, block.ConditionalSuccessor }) {
                    if (branch?.Destination is not { IsReachable: true } destination ||
                        components[block.Ordinal] != components[destination.Ordinal]) continue;
                    foreach (var region in branch.FinallyRegions) repeatingFinally.Add(region);
                }
                if (exceptional != null && exceptional.TryGetValue(block.Ordinal, out var handlers))
                    foreach (var edge in handlers) {
                        if (components[block.Ordinal] != components[edge.Target]) continue;
                        foreach (var region in edge.Unwind) repeatingFinally.Add(region);
                    }
            }
            if (repeatingFinally.Count > 0) {
                var finallyDepthChanges = new int[blocks.Length + 1];
                foreach (var region in repeatingFinally) {
                    ++finallyDepthChanges[region.FirstBlockOrdinal];
                    --finallyDepthChanges[region.LastBlockOrdinal + 1];
                }
                var finallyDepth = 0;
                for (var ordinal = 0; ordinal < blocks.Length; ++ordinal) {
                    finallyDepth += finallyDepthChanges[ordinal];
                    if (finallyDepth > 0) loops[ordinal] = true;
                }
            }
        }
        foreach (var filter in repeatableFilters)
            for (var ordinal = filter.FirstBlockOrdinal; ordinal <= filter.LastBlockOrdinal; ++ordinal) loops[ordinal] = true;
        analyzeGraph?.Invoke(graph);
        // Enumerate Roslyn's reachable blocks directly, not the normal successor
        // graph above: catch/filter/finally can have no normal predecessor edges.
        foreach (var block in blocks) {
            cancellation.ThrowIfCancellationRequested();
            if (!block.IsReachable) continue;
            beginBlock?.Invoke();
            foreach (var statement in block.Operations) visit(statement, loops[block.Ordinal]);
            if (block.BranchValue != null) visit(block.BranchValue, loops[block.Ordinal]);
        }
        return true;
    }

    private static ControlFlowGraph? GetGraph(IOperation? operation, System.Threading.CancellationToken cancellation) {
        if (operation == null) return null;
        var nested = new Stack<IOperation>();
        var root = operation;
        for (var current = operation; current != null; current = current.Parent) {
            root = current;
            if (current is ILocalFunctionOperation or IAnonymousFunctionOperation) nested.Push(current);
        }
        ControlFlowGraph? graph = root switch {
            IMethodBodyOperation method => ControlFlowGraph.Create(method, cancellation),
            IConstructorBodyOperation constructor => ControlFlowGraph.Create(constructor, cancellation),
            IFieldInitializerOperation field => ControlFlowGraph.Create(field, cancellation),
            IPropertyInitializerOperation property => ControlFlowGraph.Create(property, cancellation),
            IBlockOperation block => ControlFlowGraph.Create(block, cancellation),
            _ => null,
        };
        if (graph == null) return null;
        while (nested.Count != 0) {
            cancellation.ThrowIfCancellationRequested();
            var child = nested.Pop();
            if (child is ILocalFunctionOperation local) {
                graph = graph.GetLocalFunctionControlFlowGraph(local.Symbol, cancellation);
            } else if (child is IAnonymousFunctionOperation anonymous) {
                IFlowAnonymousFunctionOperation? flow = null;
                var operations = new Stack<IOperation>();
                foreach (var block in graph.Blocks) {
                    foreach (var statement in block.Operations) operations.Push(statement);
                    if (block.BranchValue != null) operations.Push(block.BranchValue);
                }
                while (operations.Count != 0) {
                    cancellation.ThrowIfCancellationRequested();
                    var current = operations.Pop();
                    if (current is IFlowAnonymousFunctionOperation candidate &&
                        SymbolEqualityComparer.Default.Equals(candidate.Symbol.OriginalDefinition, anonymous.Symbol.OriginalDefinition)) {
                        flow = candidate;
                        break;
                    }
                    foreach (var descendant in current.ChildOperations) operations.Push(descendant);
                }
                if (flow == null) return null;
                graph = graph.GetAnonymousFunctionControlFlowGraph(flow, cancellation);
            }
        }
        return graph;
    }
}
