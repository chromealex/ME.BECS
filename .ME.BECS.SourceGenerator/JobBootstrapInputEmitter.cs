using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class JobBootstrapInputEmitter {
    internal bool HasSchema { get; private set; }
    private readonly List<(string Identity, INamedTypeSymbol Job, string Call, string Payload)> slots = new();
    internal IReadOnlyList<(string Identity, INamedTypeSymbol Job, string Call, string Payload)> Slots => this.slots;

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
        var call = "";
        if (rows[2].Length == 0) {
            if (rows.Length != 4 || rows[3].Length != 0) return false;
        } else {
            var arguments = new ITypeSymbol[rows.Length - 4];
            for (var index = 0; index < arguments.Length; ++index) {
                var argument = resolver.ResolveDefinition(rows[index + 4], out _);
                if (argument == null || IsOpen(argument) || !compilation.IsSymbolAccessibleWithin(argument, compilation.Assembly)) return false;
                arguments[index] = argument;
            }
            var localName = "ME.BECS.SourceGenerated.JobEarlyInit_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(compilation.AssemblyName!);
            if (SymbolEqualityComparer.Default.Equals(job.ContainingAssembly, compilation.Assembly) && rows[2] == localName + ", " + compilation.Assembly.Identity) {
                // Other generators' output is not part of this Compilation. Use
                // their exact semantic selection, never an unchecked method name.
                var owners = new Stack<INamedTypeSymbol>();
                for (var current = job; current != null; current = current.ContainingType) owners.Push(current);
                if (!arguments.SequenceEqual(owners.SelectMany(type => type.TypeArguments), SymbolEqualityComparer.Default) ||
                    !JobEarlyInitGenerator.EmitsWrapper(job, compilation.GetTypeByMetadataName("ME.BECS.Jobs.EarlyInit"), rows[3])) return false;
                call = "global::" + localName + "." + rows[3];
            } else {
                var owner = resolver.ResolveDefinition(rows[2], out _);
                if (owner == null || IsOpen(owner) || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly)) return false;
                var methods = owner.GetMembers(rows[3]).OfType<IMethodSymbol>().ToArray();
                if (methods.Length != 1) return false;
                var selected = methods[0];
                if (!selected.IsStatic || !selected.ReturnsVoid || selected.Parameters.Length != 0 || selected.Arity != arguments.Length ||
                    !compilation.IsSymbolAccessibleWithin(selected, compilation.Assembly)) return false;
                call = owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + selected.Name;
            }
            if (arguments.Length != 0) call += "<" + string.Join(", ", arguments.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) + ">";
            call += "();";
        }
        slots.Add((rows[1], job, call, string.Join("\n", rows)));
        return true;
    }

    internal bool Append(StringBuilder source, string bootstrapNamespace, IReadOnlyList<DebugJobInputPlan> debugPlans,
        JobWeightInputEmitter weights, JobEntityInputEmitter entities, JobInitRegistrationOwners owners, JobSetupRegistrationOwners setupOwners, bool editor, out string error) {
        error = "";
        if (!owners.Matches(this)) { error = "Job initialization owners do not match the exact ordered EarlyInit selection."; return false; }
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
        var profile = editor ? "true" : "false";
        // Old snapshots still execute their aggregate methods until input export.
        // Their explicit empty setup plan keeps preflight safe during this upgrade.
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapJobSetupSelection { public static void Publish() {\n")
            .Append("global::ME.BECS.BootstrapRuntime.ExpectJobSetupPlan(")
            .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(setupOwners.Plan, true)).Append(", ")
            .Append(setupOwners.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ").Append(profile).Append(");\n} } }\n");
        var plan = owners.Distributed ? owners.Plan : ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(string.Join("\n", slots.Select(slot => slot.Payload)));
        source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapJobInitSelection { public static void Publish() {\n");
        if (!owners.Distributed) {
            // One-way upgrade only; new snapshots publish all typed calls locally.
            source.Append("global::ME.BECS.BootstrapRuntime.InstallJobInitFragment(")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(plan, true)).Append(", \"$upgrade\", ")
                .Append(slots.Count.ToString(CultureInfo.InvariantCulture)).Append(", new int[] { ")
                .Append(string.Join(",", Enumerable.Range(0, slots.Count))).Append(" }, new global::System.Action[] { ")
                .Append(string.Join(",", slots.Select(slot => "() => { " + slot.Call + " }"))).Append(" }, editor: ").Append(profile).Append(");\n");
        }
        source.Append("global::ME.BECS.BootstrapRuntime.ExpectJobInitPlan(")
            .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(plan, true)).Append(", ")
            .Append(slots.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ").Append(profile).Append(");\n} } }\n");
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static class JobBootstrapInputs {\n")
            .Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize() {\n")
            .Append("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS\n")
            .Append("global::").Append(bootstrapNamespace).Append(".DebugJobs.InitializeJobsDebug();\n#endif\n");
        // Repeated jobs are intentional: the preflight compares this phase ordering,
        // including stat-only slots. Do not sort or deduplicate this sequence.
        for (var ordinal = 0; ordinal < slots.Count; ++ordinal) {
            var slot = slots[ordinal];
            var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(slot.Identity);
            if (setupOwners.Distributed) {
                source.Append("global::ME.BECS.BootstrapRuntime.InvokeJobSetup(").Append(setupOwners.Ordinal(slot.Identity).ToString(CultureInfo.InvariantCulture))
                    .Append(", editor: ").Append(profile).Append(");\n");
            } else {
                source.Append("JobEntityInputCalls.Initialize_").Append(suffix).Append("();\n")
                    .Append("JobWeightInputs.Initialize_").Append(suffix).Append("();\n")
                    .Append("JobLayoutInputs.Initialize_").Append(suffix).Append("();\n");
            }
            source.Append("global::ME.BECS.BootstrapRuntime.InvokeJobEarlyInit(").Append(ordinal.ToString(CultureInfo.InvariantCulture))
                .Append(", editor: ").Append(profile).Append(");\n");
        }
        source.Append("} } }\n");
        return true;
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
