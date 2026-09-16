using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobSafetySummary {
    internal const string MetadataKey = "ME.BECS.JobSafety.v1";

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode) {
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var dependencies = new SortedDictionary<string, (int Bits, bool IsArgument)>(StringComparer.Ordinal);
        var componentTypes = new Dictionary<string, bool?>(StringComparer.Ordinal);
        var pending = new Queue<(string Assembly, string Id, MethodSummaryType[] Arguments)>();
        var visited = new HashSet<(string Assembly, string Id, string Context)>();
        long contextSize = 0;
        var rootArguments = root.RootArguments ?? root.Environment;
        if (rootArguments.Any(static a => a.IsOpen)) gaps.Add("OpenGenericRoot");

        bool Add(MethodSummaryType? type, int mode, bool parameter) {
            if (type == null || type.Kind != 'n' || type.IsUnsupported || type.IsOpen || type.Arguments.Length != 0) {
                gaps.Add("UnresolvedOrGenericComponent"); return false;
            }
            if (!componentTypes.TryGetValue(type.Identity, out var component)) {
                var identity = type.Identity.Split('\n');
                var symbols = identity.Length == 2 ? DocumentationCommentId.GetSymbolsForDeclarationId(identity[1], compilation)
                    .OfType<INamedTypeSymbol>().Where(s => s.ContainingAssembly.Identity.ToString() == identity[0]).ToArray() : Array.Empty<INamedTypeSymbol>();
                component = symbols.Length == 1 ? symbols[0].AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponentBase") : (bool?)null;
                componentTypes.Add(type.Identity, component);
            }
            if (!component.HasValue) { gaps.Add("UnresolvedComponentDefinition: " + type.Identity.Replace('\n', '|')); return false; }
            if (!component.Value) { if (parameter) gaps.Add("NonComponentParameterOverride"); return false; }
            var bits = mode == 2 ? 3 : mode + 1;
            dependencies.TryGetValue(type.Identity, out var previous);
            if (parameter) {
                if (previous.IsArgument && previous.Bits != bits) {
                    gaps.Add("ConflictingParameterOverrides: " + type.Identity.Replace('\n', '|'));
                    bits |= previous.Bits;
                }
                dependencies[type.Identity] = (bits, true);
            } else dependencies[type.Identity] = (previous.Bits | bits, previous.IsArgument);
            return true;
        }

        pending.Enqueue((assembly, root.Id, rootArguments));
        while (pending.Count != 0) {
            output.CancellationToken.ThrowIfCancellationRequested();
            var node = pending.Dequeue();
            var key = (node.Assembly, node.Id);
            var context = string.Join(";", node.Arguments.Select(static a => a.Encode()));
            if (!visited.Add((node.Assembly, node.Id, context))) continue;
            contextSize += context.Length;
            if (visited.Count > 10000 || contextSize > 16777216) { gaps.Add("TraversalLimit"); break; }
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + node.Id); continue; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + node.Assembly + " | " + node.Id); continue; }
            if (!method.Flags.Contains("safety-schema=1")) gaps.Add("MissingSafetyContracts: " + node.Id);
            if (node.Arguments.Any(static a => a.IsOpen)) gaps.Add("UnboundTypeParameter: " + node.Id);
            if (method.Environment.Length != node.Arguments.Length) { gaps.Add("GenericArityMismatch: " + node.Id); continue; }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var i = 0; i < node.Arguments.Length; ++i) environment[method.Environment[i].Identity] = node.Arguments[i];
            foreach (var unresolved in method.Unresolved) gaps.Add(unresolved + ": " + node.Id);
                foreach (var rawOperation in method.Operations) {
                    var operation = JobConstrainedCall.Resolve(rawOperation, compilation, environment, gaps);
                    if (MethodSummaryContracts.Has(operation, "scalar-comparison")) continue;
                if (operation.Length < 5) { gaps.Add("MalformedOperation: " + node.Id); continue; }
                if (operation[0] == "parameter-override" || MethodSummaryContracts.Has(operation, "disable-safety")) continue;
                var modeText = MethodSummaryContracts.Value(operation, operation[0] == "field" ? "ref" : "safety");
                if (modeText != null) {
                    var componentToken = MethodSummaryContracts.Value(operation, "component");
                    if (!TryMode(modeText, out var mode) || componentToken == null) gaps.Add("MalformedSafetyContract: " + operation[3]);
                    else if (Add(decode(componentToken)?.Substitute(environment), mode, false)) continue;
                }
                if (MethodSummaryContracts.Has(operation, "ref-unknown") || MethodSummaryContracts.Has(operation, "safety-unknown"))
                    gaps.Add("UnknownSafetyContract: " + operation[3]);
                if (operation[0] == "field" || MethodSummaryContracts.Has(operation, "ignore")) continue;
                if (operation[0] == "method-ref") gaps.Add("DeferredInvocation: " + operation[3]);
                if (operation[0] == "new") gaps.Add("ConstructorTraversalDiffersFromLegacy: " + operation[3]);
                var receiver = decode(operation[4])?.Substitute(environment);
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedReceiver: " + operation[3]); continue; }
                var arguments = new List<MethodSummaryType>(receiver.Arguments);
                var invalid = false;
                for (var i = 5; i < MethodSummaryContracts.ArgumentEnd(operation); ++i) {
                    var argument = decode(operation[i])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { invalid = true; break; }
                    arguments.Add(argument);
                }
                if (invalid) { gaps.Add("UnsupportedTypeArgument: " + operation[3]); continue; }
                pending.Enqueue((operation[2], operation[3], arguments.ToArray()));
            }
        }
        // Legacy applies explicit parameter overrides after the transitive dependency union.
        // Overrides on helper parameters do not override the root job's global access set.
        var rootBindings = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
        for (var i = 0; i < root.Environment.Length; ++i) rootBindings[root.Environment[i].Identity] = rootArguments[i];
        foreach (var operation in root.Operations.Where(static o => o[0] == "parameter-override")) {
            if (operation.Length < 6 || !TryMode(MethodSummaryContracts.Value(operation, "mode"), out var mode)) {
                gaps.Add("MalformedParameterOverride"); continue;
            }
            Add(decode(operation[5])?.Substitute(rootBindings), mode, true);
        }
        var jobType = root.Flags.FirstOrDefault(static f => f.StartsWith("job-type=", StringComparison.Ordinal))?.Substring(9) ?? "";
        var lines = new List<string> { jobType, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var dependency in dependencies) {
            var identity = dependency.Key.Split('\n');
            if (identity.Length != 2) continue;
            var mode = dependency.Value.Bits == 3 ? 2 : dependency.Value.Bits - 1;
            lines.Add("D\t" + identity[0] + "\t" + identity[1] + "\t" + mode.ToString(CultureInfo.InvariantCulture) + "\t" + (dependency.Value.IsArgument ? "1" : "0"));
        }
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) lines.Add("G\t" + gap);
        return string.Join("\n", lines);
    }

    private static bool TryMode(string? value, out int mode) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out mode) && mode >= 0 && mode <= 2;
}
