using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ME.BECS.SourceGenerator;

internal static class MethodSummaryControlFlow {
    internal static bool Visit(IOperation? operation, Action<IOperation, bool> visit, ISet<string> gaps,
        System.Threading.CancellationToken cancellation, Action? beginBlock = null) {
        ControlFlowGraph? graph;
        try { graph = GetGraph(operation, cancellation); }
        catch (ArgumentException) { gaps.Add("ControlFlowGraphUnavailable"); return false; }
        catch (InvalidOperationException) { gaps.Add("ControlFlowGraphUnavailable"); return false; }
        if (graph == null) return false;
        var blocks = graph.Blocks;
        if (blocks.Length > 20000) { gaps.Add("ControlFlowGraphSizeLimit"); return false; }
        var edges = new int[blocks.Length][];
        var reverse = new List<int>[blocks.Length];
        for (var i = 0; i < blocks.Length; ++i) reverse[i] = new List<int>();
        for (var i = 0; i < blocks.Length; ++i) {
            cancellation.ThrowIfCancellationRequested();
            if (!blocks[i].IsReachable) { edges[i] = Array.Empty<int>(); continue; }
            edges[i] = new[] { blocks[i].FallThroughSuccessor?.Destination, blocks[i].ConditionalSuccessor?.Destination }
                .Where(static b => b != null && b.IsReachable).Select(static b => b!.Ordinal).Distinct().ToArray();
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
            if (component.Count > 1 || edges[component[0]].Contains(component[0])) foreach (var node in component) loops[node] = true;
        }
        var regions = new Stack<ControlFlowRegion>();
        regions.Push(graph.Root);
        while (regions.Count != 0) {
            var region = regions.Pop();
            if (region.Kind is ControlFlowRegionKind.Finally or ControlFlowRegionKind.Filter or ControlFlowRegionKind.Catch)
                gaps.Add("ExceptionControlFlow"); // Exceptional/structured-finally edges need separate modeling.
            foreach (var nested in region.NestedRegions) regions.Push(nested);
        }
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
