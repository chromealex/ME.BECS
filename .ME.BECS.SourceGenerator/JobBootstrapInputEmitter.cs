using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class JobBootstrapInputEmitter {
    internal bool HasSchema { get; private set; }
    private readonly List<(string Identity, INamedTypeSymbol Job, IMethodSymbol? Method)> slots = new();

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation) {
        if (fields[0] == "job-early-init-schema") {
            if (HasSchema || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") return false;
            HasSchema = true;
            return true;
        }
        if (!HasSchema || fields.Length != 3 || fields[1] != slots.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 4 || rows[0] != "v1") return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        IMethodSymbol? selected = null;
        if (rows[2].Length == 0) {
            if (rows.Length != 4 || rows[3].Length != 0) return false;
        } else {
            var owner = resolver.ResolveDefinition(rows[2], out _);
            if (owner == null || IsOpen(owner) || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly)) return false;
            var methods = owner.GetMembers(rows[3]).OfType<IMethodSymbol>().ToArray();
            if (methods.Length != 1) return false;
            selected = methods[0];
            if (!selected.IsStatic || !selected.ReturnsVoid || selected.Parameters.Length != 0 || selected.Arity != rows.Length - 4 ||
                !compilation.IsSymbolAccessibleWithin(selected, compilation.Assembly)) return false;
            if (selected.Arity != 0) {
                var arguments = new ITypeSymbol[selected.Arity];
                for (var index = 0; index < arguments.Length; ++index) {
                    var argument = resolver.ResolveDefinition(rows[index + 4], out _);
                    if (argument == null || IsOpen(argument) || !compilation.IsSymbolAccessibleWithin(argument, compilation.Assembly)) return false;
                    arguments[index] = argument;
                }
                selected = selected.Construct(arguments);
            }
        }
        slots.Add((rows[1], job, selected));
        return true;
    }

    internal bool Append(StringBuilder source, string bootstrapNamespace, IReadOnlyList<DebugJobInputPlan> debugPlans,
        JobWeightInputEmitter weights, JobEntityInputEmitter entities, out string error) {
        error = "";
        if (!HasSchema) return true; // Pre-migration inputs retain their exported body.
        var layouts = new HashSet<ITypeSymbol>(debugPlans.Select(plan => plan.Job), SymbolEqualityComparer.Default);
        var selectedJobs = new HashSet<ITypeSymbol>(slots.Select(slot => slot.Job), SymbolEqualityComparer.Default);
        // Checking only existing slots would accept a truncated (even empty) bootstrap
        // while leaving valid debug/weight/entity plans completely uninitialized.
        if (!layouts.SetEquals(selectedJobs) || weights.JobCount != selectedJobs.Count || entities.JobCount != selectedJobs.Count) {
            error = "Job EarlyInit, debug, weight and entity plans select different job sets. Regenerate bootstrap and inputs together.";
            return false;
        }
        foreach (var slot in slots) {
            if (layouts.Contains(slot.Job) && weights.ContainsJob(slot.Job) && entities.ContainsJob(slot.Job)) continue;
            error = "Incomplete job initialization inputs for " + slot.Identity + ". Regenerate bootstrap and inputs together.";
            return false;
        }
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static class JobBootstrapInputs {\n")
            .Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize() {\n")
            .Append("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS\n")
            .Append("global::").Append(bootstrapNamespace).Append(".DebugJobs.InitializeJobsDebug();\n#endif\n");
        // Repeated jobs are intentional: the preflight compares this phase ordering,
        // including stat-only slots. Do not sort or deduplicate this sequence.
        foreach (var slot in slots) {
            var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(slot.Identity);
            source.Append("JobEntityInputCalls.Initialize_").Append(suffix).Append("();\n")
                .Append("JobWeightInputs.Initialize_").Append(suffix).Append("();\n")
                .Append("JobLayoutInputs.Initialize_").Append(suffix).Append("();\n");
            if (slot.Method == null) continue;
            source.Append(slot.Method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('.').Append(slot.Method.Name);
            if (slot.Method.Arity > 0)
                source.Append('<').Append(string.Join(", ", slot.Method.TypeArguments.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))).Append('>');
            source.Append("();\n");
        }
        source.Append("} } }\n");
        return true;
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
