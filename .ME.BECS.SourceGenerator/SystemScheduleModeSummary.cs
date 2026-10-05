using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Separate from direct accesses and job discovery. A system dependency consumer must
// require all three coverages, then union job safety with these per-job schedule modes.
internal static class SystemScheduleModeSummary {
    internal const string MetadataKey = "ME.BECS.SystemScheduleModes.v1";

    internal static string Resolve(string? expression, IReadOnlyDictionary<int, string> values) {
        if (string.IsNullOrEmpty(expression)) return "?";
        string? result = null;
        foreach (var term in expression!.Split('|')) {
            string value;
            if (term is "0" or "1" or "?") value = term;
            else if (term.StartsWith("p", StringComparison.Ordinal) && int.TryParse(term.Substring(1), NumberStyles.None,
                         CultureInfo.InvariantCulture, out var ordinal) && term == "p" + ordinal.ToString(CultureInfo.InvariantCulture))
                value = values.TryGetValue(ordinal, out var parameter) ? parameter : "?";
            else return "?";
            result = result == null ? value : QueryScheduleModeFlow.Union(result, value);
        }
        return result ?? "?";
    }

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode,
        out Dictionary<string, (INamedTypeSymbol Job, string Mode)> scheduledJobs) {
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var modes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        scheduledJobs = new Dictionary<string, (INamedTypeSymbol Job, string Mode)>(StringComparer.Ordinal);
        var pending = new Queue<(string Assembly, string Id, MethodSummaryType[] Types, Dictionary<int, string> Values)>();
        var visited = new HashSet<(string Assembly, string Id, string Types, string Values)>();
        pending.Enqueue((assembly, root.Id, root.RootArguments ?? root.Environment, new Dictionary<int, string>()));
        long contextSize = 0;
        while (pending.Count > 0) {
            output.CancellationToken.ThrowIfCancellationRequested();
            var node = pending.Dequeue();
            var typesKey = string.Join(";", node.Types.Select(type => type.Encode()));
            var valuesKey = string.Join(";", node.Values.OrderBy(entry => entry.Key).Select(entry =>
                entry.Key.ToString(CultureInfo.InvariantCulture) + "=" + entry.Value));
            if (!visited.Add((node.Assembly, node.Id, typesKey, valuesKey))) continue;
            contextSize += typesKey.Length + valuesKey.Length;
            if (visited.Count > 10000 || contextSize > 16777216) { gaps.Add("ScheduleModeTraversalLimit"); break; }
            var key = (node.Assembly, node.Id);
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + node.Id); continue; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + node.Id); continue; }
            if (method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreAttribute")) continue;
            if (!method.Flags.Contains(DestroyDispatchContracts.Schema)) gaps.Add("MissingDestroyContracts: " + node.Id);
            if (!method.Flags.Contains(ImplicitFormattingContracts.Schema)) gaps.Add("MissingImplicitFormattingContracts: " + node.Id);
            if (!method.Flags.Contains(GenericConstructionContracts.Schema)) gaps.Add("MissingGenericConstructionContracts: " + node.Id);
            if (MethodSummaryContracts.IsConstructor(node.Id) && !method.Flags.Contains(MethodSummaryContracts.ConstructorSchema))
                gaps.Add("MissingConstructorContract: " + node.Id);
            if (!method.Flags.Contains(QueryScheduleModeFlow.Schema) || !method.Flags.Contains(MethodSummaryContracts.SchedulingSchema))
                gaps.Add("MissingScheduleModeSchema: " + node.Id);
            if (method.Environment.Length != node.Types.Length || node.Types.Any(type => type.IsOpen || type.IsUnsupported)) {
                gaps.Add("UnresolvedScheduleModeContext: " + node.Id); continue;
            }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var index = 0; index < node.Types.Length; ++index) environment.Add(method.Environment[index].Identity, node.Types[index]);
            foreach (var gap in method.Unresolved) {
                // The effect-union contract includes every handler/cleanup call.
                // QueryScheduleModeFlow follows finally continuations separately;
                // unsupported handlers/escaped values still emit ?, never a guessed mode.
                if (gap == "ExceptionControlFlow" && method.Flags.Contains(MethodSummaryControlFlow.EffectUnionSchema)) continue;
                gaps.Add(gap + ": " + node.Id);
            }
            foreach (var raw in method.Operations) {
                var operation = JobConstrainedCall.Resolve(raw, compilation, environment, gaps, methods, conflicts);
                if (operation.Length < 5) { gaps.Add("MalformedOperation: " + node.Id); continue; }
                DestroyDispatchContracts.Component(operation, compilation, environment, decode, gaps);
                if (operation[0] is "field" or "parameter-override" || MethodSummaryContracts.Has(operation, "ignore") ||
                    MethodSummaryContracts.Has(operation, "ecs-leaf") || MethodSummaryContracts.Has(operation, "scalar-comparison")) continue;
                if (operation[0] == "method-ref") { gaps.Add("DeferredInvocation: " + operation[3]); continue; }
                if (JobControlContracts.SystemCall(operation, method.Flags, gaps)) continue;
                if (MethodSummaryContracts.Value(operation, "scheduled-job") is string jobToken) {
                    var jobExpression = decode(jobToken)?.Substitute(environment);
                    var job = jobExpression == null || jobExpression.IsUnsupported || jobExpression.IsOpen ? null :
                        MethodSummaryTypeResolver.Resolve(jobExpression, compilation) as INamedTypeSymbol;
                    var identity = job == null ? null : JobSafetySummary.ReflectionIdentity(job);
                    if (identity == null) { gaps.Add("UnresolvedScheduledJob: " + operation[3]); continue; }
                    var mode = Resolve(MethodSummaryContracts.Value(operation, "schedule-readonly"), node.Values);
                    if (mode == "?") gaps.Add("UnknownScheduleMode: " + node.Id + " -> " + operation[3]);
                    modes[identity] = modes.TryGetValue(identity, out var previous) ? QueryScheduleModeFlow.Union(previous, mode) : mode;
                    scheduledJobs[identity] = (job!, modes[identity]);
                    // Execute runs later. Do not rediscover this same job through the
                    // engine's lower-level scheduling wrappers with an unbound receiver.
                    continue;
                }
                if (MethodSummaryContracts.Has(operation, "query-mode-leaf") ||
                    MethodSummaryContracts.Value(operation, "query-filter") != null ||
                    MethodSummaryContracts.Value(operation, "system-access") != null ||
                    (MethodSummaryContracts.Value(operation, "component") != null &&
                     MethodSummaryContracts.Value(operation, "safety") is "0" or "1" or "2")) continue;
                var receiver = decode(operation[4])?.Substitute(environment);
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedReceiver: " + operation[3]); continue; }
                var arguments = new List<MethodSummaryType>(receiver.Arguments);
                var invalid = false;
                for (var index = 5; index < MethodSummaryContracts.ArgumentEnd(operation); ++index) {
                    var argument = decode(operation[index])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { invalid = true; break; }
                    arguments.Add(argument);
                }
                if (invalid) { gaps.Add("UnsupportedTypeArgument: " + operation[3]); continue; }
                var values = new Dictionary<int, string>();
                foreach (var annotation in operation.Where(item => item.StartsWith("!schedule-value-", StringComparison.Ordinal))) {
                    var pair = annotation.Substring("!schedule-value-".Length).Split('=');
                    if (pair.Length != 2 || !int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) ||
                        pair[0] != ordinal.ToString(CultureInfo.InvariantCulture) || values.ContainsKey(ordinal)) {
                        gaps.Add("MalformedScheduleArgument: " + operation[3]); continue;
                    }
                    values.Add(ordinal, Resolve(pair[1], node.Values));
                }
                pending.Enqueue((operation[2], operation[3], arguments.ToArray(), values));
            }
        }
        var system = root.Flags.Single(flag => flag.StartsWith("system-type=", StringComparison.Ordinal)).Substring("system-type=".Length);
        return system + "\n" + root.Id + "\n" + gaps.Count.ToString(CultureInfo.InvariantCulture) + "\n" +
            string.Join("\n", modes.Select(entry => "S\t" + entry.Value + "\t" + entry.Key)) + "\n" +
            string.Join("\n", gaps.Take(12).Select(gap => "G\t" + gap));
    }
}
