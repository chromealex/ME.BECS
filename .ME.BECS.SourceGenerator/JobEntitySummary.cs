using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobEntitySummary {
    internal const string MetadataKey = "ME.BECS.JobEntityCounts.v1";

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode) {
        var ent = compilation.GetTypeByMetadataName("ME.BECS.Ent");
        var terminal = ent?.GetMembers("NewEnt_INTERNAL").OfType<IMethodSymbol>()
            .SingleOrDefault(static m => m.Arity == 1);
        var terminalId = terminal?.GetDocumentationCommentId();
        var terminalAssembly = ent?.ContainingAssembly.Identity.ToString();
        // Roslyn's public metadata import can hide internal methods in referenced asmdefs.
        // The declaring compilation exports their summaries, including the allocation contract.
        if (terminalId == null && terminalAssembly != null) {
            var exported = methods.Where(m => m.Key.Assembly == terminalAssembly &&
                m.Value.Flags.Contains("entity-creation-contract=1") && !conflicts.Contains(m.Key)).ToArray();
            if (exported.Length == 1) terminalId = exported[0].Key.Id;
        }
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var counts = new SortedDictionary<string, (int Inline, int Loop)>(StringComparer.Ordinal);
        var visited = new HashSet<(string Assembly, string Id, string Context)>();
        var active = new HashSet<(string Assembly, string Id, string Context)>();
        var definitionContexts = new Dictionary<(string Assembly, string Id), string>();
        var work = 0;
        long contextSize = 0;
        var rootArguments = root.RootArguments ?? root.Environment;
        if (rootArguments.Any(static a => a.IsOpen)) gaps.Add("OpenGenericRoot");
        if (terminalId == null) gaps.Add("MissingEntityCreationContract");

        void Visit(string targetAssembly, string id, MethodSummaryType[] arguments, bool inLoop, int depth) {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (++work > 20000 || depth > 128) { gaps.Add("TraversalLimit"); return; }
            var key = (targetAssembly, id);
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + id); return; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + targetAssembly + " | " + id); return; }
            if (method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreAttribute")) return;
            if (arguments.Any(static a => a.IsOpen)) gaps.Add("UnboundTypeParameter: " + id);
            var context = string.Join(";", arguments.Select(static a => a.Encode()));
            contextSize += context.Length;
            if (contextSize > 16777216) { gaps.Add("TraversalContextLimit"); return; }
            var instance = (targetAssembly, id, context);
            if (active.Contains(instance)) { gaps.Add("RecursiveCreationPath: " + id); return; }
            if (!method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreVisitedAttribute")) {
                if (!visited.Add(instance)) return;
                // Legacy MethodPointerData does not consistently distinguish generic arguments.
                // Do not declare parity merely because a correctly instantiated graph is closed.
                if (definitionContexts.TryGetValue(key, out var previous) && previous != context) gaps.Add("LegacyGenericVisitIdentity: " + id);
                definitionContexts[key] = context;
            }
            if (method.Environment.Length != arguments.Length) { gaps.Add("GenericArityMismatch: " + id); return; }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Length; ++i) environment[method.Environment[i].Identity] = arguments[i];
            foreach (var unresolved in method.Unresolved) gaps.Add(unresolved + ": " + id);
            active.Add(instance);
                foreach (var rawOperation in method.Operations) {
                    var operation = JobConstrainedCall.Resolve(rawOperation, compilation, environment, gaps);
                    if (MethodSummaryContracts.Has(operation, "scalar-comparison")) continue;
                if (work > 20000) break;
                if (operation.Length < 5 || !int.TryParse(operation[1], NumberStyles.None, CultureInfo.InvariantCulture, out var loopDepth)) {
                    gaps.Add("MalformedOperation: " + id); continue;
                }
                if (operation[0] == "field" || operation[0] == "parameter-override" || MethodSummaryContracts.Has(operation, "ignore")) continue;
                if (operation[0] == "method-ref") { gaps.Add("DeferredInvocation: " + operation[3]); continue; }
                if (operation[0] == "new") gaps.Add("ConstructorTraversalDiffersFromLegacy: " + operation[3]);
                var receiver = decode(operation[4])?.Substitute(environment);
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedReceiver: " + operation[3]); continue; }
                var targetArguments = new List<MethodSummaryType>(receiver.Arguments);
                var invalid = false;
                for (var i = 5; i < MethodSummaryContracts.ArgumentEnd(operation); ++i) {
                    var argument = decode(operation[i])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { invalid = true; break; }
                    targetArguments.Add(argument);
                }
                if (invalid) { gaps.Add("UnsupportedTypeArgument: " + operation[3]); continue; }
                var loop = inLoop || loopDepth > 0;
                if (operation[2] == terminalAssembly && operation[3] == terminalId && targetArguments.Count == 1) {
                    var entityType = targetArguments[0];
                    if (entityType.Kind != 'n' || entityType.IsOpen || entityType.Arguments.Length != 0) { gaps.Add("UnresolvedEntityType"); continue; }
                    counts.TryGetValue(entityType.Identity, out var count);
                    counts[entityType.Identity] = loop ? (count.Inline, count.Loop + 1) : (count.Inline + 1, count.Loop);
                    // This is the allocation contract; do not count its implementation as a second creation.
                    continue;
                }
                Visit(operation[2], operation[3], targetArguments.ToArray(), loop, depth + 1);
            }
            active.Remove(instance);
        }

        Visit(assembly, root.Id, rootArguments, false, 0);
        var jobType = root.Flags.FirstOrDefault(static f => f.StartsWith("job-type=", StringComparison.Ordinal))?.Substring(9) ?? "";
        var lines = new List<string> { jobType, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var count in counts) {
            var identity = count.Key.Split('\n');
            if (identity.Length != 2) continue;
            lines.Add("C\t" + identity[0] + "\t" + identity[1] + "\t" + count.Value.Inline.ToString(CultureInfo.InvariantCulture) + "\t" + count.Value.Loop.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) lines.Add("G\t" + gap);
        return string.Join("\n", lines);
    }
}
