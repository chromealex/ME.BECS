using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Explicit IL estimates are authoritative. Old source/legacy records remain
// readable until their manifests are regenerated; they retain their old selection.
internal sealed class JobWeightInputEmitter {
    private readonly List<(string Identity, INamedTypeSymbol Job, INamedTypeSymbol? Initializer, uint Weight)> plans = new();
    private readonly HashSet<ITypeSymbol> jobs = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ITypeSymbol> ilJobs = new(SymbolEqualityComparer.Default);
    internal bool ContainsJob(ITypeSymbol job) => jobs.Contains(job);
    internal int JobCount => jobs.Count;

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid job weight plan";
        if (fields.Length != 5 || fields[1] != plans.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string identity;
        string target = "";
        try {
            identity = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2]));
            if (fields[3] == "source") target = Encoding.UTF8.GetString(Convert.FromBase64String(fields[4]));
        } catch (FormatException) { return false; }
        var job = resolver.ResolveDefinition(identity, out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly) || jobs.Contains(job)) return false;
        INamedTypeSymbol? initializer = null;
        uint weight = 0;
        if (fields[3] == "source") {
            initializer = resolver.ResolveDefinition(target, out _);
            if (initializer == null || !initializer.IsStatic || IsOpen(initializer) ||
                !compilation.IsSymbolAccessibleWithin(initializer, compilation.Assembly)) return false;
            // Only accept the exact generated generic adapter contract. The editor
            // validates its metadata identity; Roslyn independently validates callable shape.
            var methods = initializer.GetMembers("Apply").OfType<IMethodSymbol>().ToArray();
            if (methods.Length != 1) return false;
            var method = methods[0];
            if (!method.IsStatic || !method.ReturnsVoid || method.Arity != 1 || method.Parameters.Length != 0 ||
                !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly)) return false;
            var parameter = method.TypeParameters[0];
            if (!parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint ||
                parameter.ConstraintTypes.Length != 0 || parameter.HasReferenceTypeConstraint) return false;
        } else if (fields[3] == "catalog") {
            if (fields[4] != "v1") return false;
        } else if ((fields[3] != "value" && fields[3] != "il") || !CompilerJobCatalogs.Number(fields[4], out weight)) {
            return false;
        }
        if (fields[3] != "il") {
            var status = CompilerJobStatistics.Weight(job, resolver, compilation, out var selected, out var currentWeight, out error);
            if (status == CompilerJobCatalogs.Status.Invalid) return false;
            if (status == CompilerJobCatalogs.Status.Complete) {
                initializer = selected;
                weight = currentWeight;
            } else if (fields[3] == "catalog") return false;
        } else ilJobs.Add(job);
        jobs.Add(job);
        plans.Add((identity, job, initializer, weight));
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source) {
        foreach (var plan in plans) {
            var payload = "v1\n" + plan.Identity + "\n" + (ilJobs.Contains(plan.Job) ? "il" : plan.Initializer == null ? "legacy" : "source") + "\n" +
                (plan.Initializer == null ? "" : JobSafetySummary.ReflectionIdentity(plan.Initializer)) + "\n" + plan.Weight.ToString(CultureInfo.InvariantCulture);
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.JobWeightSelection.v1\", ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(payload, true)).Append(")]\n");
        }
    }

    internal void Append(StringBuilder source, string suffix = "") {
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static class JobWeightInputs").Append(suffix).Append(" {\n");
        foreach (var plan in plans) {
            var job = plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize_")
                .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.Identity)).Append("() {\n");
            if (plan.Initializer != null)
                source.Append(plan.Initializer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(".Apply<").Append(job).Append(">();\n");
            else
                source.Append("global::ME.BECS.JobStaticInfo<").Append(job).Append(">.opsWeight = ")
                    .Append(plan.Weight.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
            source.Append("}\n");
        }
        source.Append("} }\n");
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
