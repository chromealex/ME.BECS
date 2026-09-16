using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobWeightSummary {
    internal const string MetadataKey = "ME.BECS.JobWeights.v1";

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode) {
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var contributions = new SortedDictionary<string, uint>(StringComparer.Ordinal);
        var visited = new HashSet<(string Assembly, string Id, string Context)>();
        var active = new HashSet<(string Assembly, string Id, string Context)>();
        var baseText = root.Flags.FirstOrDefault(static f => f.StartsWith("weight-base=", StringComparison.Ordinal))?.Substring(12);
        if (!uint.TryParse(baseText, NumberStyles.None, CultureInfo.InvariantCulture, out var weight)) gaps.Add("MissingBaseWeight");
        var rootArguments = root.RootArguments ?? root.Environment;
        if (rootArguments.Any(static a => a.IsOpen)) gaps.Add("OpenGenericRoot");
        var work = 0;
        long contextSize = 0;

        void Visit(string targetAssembly, string id, MethodSummaryType[] arguments, int depth) {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (++work > 20000 || depth > 128) { gaps.Add("TraversalLimit"); return; }
            var key = (targetAssembly, id);
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + id); return; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + targetAssembly + " | " + id); return; }
            if (method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreAttribute")) return;
            if (!method.Flags.Contains("weight-schema=1")) gaps.Add("MissingWeightContracts: " + id);
            if (arguments.Any(static a => a.IsOpen)) gaps.Add("UnboundTypeParameter: " + id);
            var context = string.Join(";", arguments.Select(static a => a.Encode()));
            contextSize += context.Length;
            if (contextSize > 16777216) { gaps.Add("TraversalContextLimit"); return; }
            var instance = (targetAssembly, id, context);
            if (active.Contains(instance)) { gaps.Add("RecursiveWeightPath: " + id); return; }
            if (!method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreVisitedAttribute")) {
                if (!visited.Add(instance)) return;
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
                if (operation.Length < 5) { gaps.Add("MalformedOperation: " + id); continue; }
                if (operation[0] == "field" || operation[0] == "parameter-override") continue;
                // Binding a method group does not execute its body or pay an ECS API
                // weight. A resolved delegate invocation contributes a separate call row.
                // Unknown escapes remain a gap rather than an assumed zero-cost callback.
                if (operation[0] == "method-ref") { gaps.Add("DeferredInvocation: " + operation[3]); continue; }
                // Legacy counts the instruction even if its callee's body is ignored/visited.
                var weightText = MethodSummaryContracts.Value(operation, "weight");
                if (weightText != null) {
                    if (!uint.TryParse(weightText, NumberStyles.None, CultureInfo.InvariantCulture, out var delta) || delta > uint.MaxValue - weight)
                        gaps.Add("InvalidOrOverflowingWeight: " + operation[3]);
                    else {
                        weight += delta;
                        var declaration = operation[3];
                        var end = declaration.IndexOf('(');
                        if (end >= 0) declaration = declaration.Substring(0, end);
                        var generic = declaration.IndexOf("``", StringComparison.Ordinal);
                        if (generic >= 0) declaration = declaration.Substring(0, generic);
                        var name = declaration.StartsWith("M:", StringComparison.Ordinal) ? declaration.Substring(2) : declaration;
                        contributions.TryGetValue(name, out var previous);
                        contributions[name] = previous + delta;
                    }
                }
                if (MethodSummaryContracts.Has(operation, "ignore")) continue;
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
                Visit(operation[2], operation[3], targetArguments.ToArray(), depth + 1);
            }
            active.Remove(instance);
        }

        Visit(assembly, root.Id, rootArguments, 0);
        var jobType = root.Flags.FirstOrDefault(static f => f.StartsWith("job-type=", StringComparison.Ordinal))?.Substring(9) ?? "";
        var lines = new List<string> { jobType, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture), weight.ToString(CultureInfo.InvariantCulture) };
        if (gaps.Count == 0 && compilation.GetTypeByMetadataName("ME.BECS.JobStaticInfo`1") != null) {
            var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(compilation.Assembly.Identity + "\n" + jobType + "\n" + root.Id);
            var className = "JobWeight_" + suffix;
            // No module initializer: the ordered bootstrap chooses when this method runs.
            // A generic wrapper works for closed jobs whose definition lives in another asmdef.
            var source = "// <auto-generated/>\nnamespace ME.BECS.SourceGenerated {\n" +
                "public static class " + className + " {\n" +
                "public static void Apply<TJob>() where TJob : struct { global::ME.BECS.JobStaticInfo<TJob>.opsWeight = " +
                weight.ToString(CultureInfo.InvariantCulture) + "u; }\n}\n}\n";
            output.AddSource("ME.BECS." + className + ".g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(source, System.Text.Encoding.UTF8));
            lines.Add("I\t" + compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + className + "\tApply");
        }
        foreach (var contribution in contributions) lines.Add("W\t" + contribution.Key + "\t" + contribution.Value.ToString(CultureInfo.InvariantCulture));
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) lines.Add("G\t" + gap);
        return string.Join("\n", lines);
    }
}
