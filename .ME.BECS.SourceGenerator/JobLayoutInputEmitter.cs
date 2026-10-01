using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobLayoutInputEmitter {
    internal static void Append(StringBuilder source, Compilation compilation, IReadOnlyList<DebugJobInputPlan> plans) {
        var componentContract = compilation.GetTypeByMetadataName("ME.BECS.IComponent");
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static class JobLayoutInputs {\n");
        foreach (var group in plans.GroupBy(plan => plan.Job, SymbolEqualityComparer.Default)) {
            var plan = group.First();
            var job = plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize_")
                .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.JobIdentity)).Append("() {\n")
                .Append("global::ME.BECS.JobStaticInfo<").Append(job).Append(">.maxStructSize = 0u;\n");
            // A job can have several scheduling contracts. Merge their safety sets
            // without depending on contract order, retaining only regular components.
            var components = group.SelectMany(other => other.Safety).Select(dependency => dependency.Type)
                .Where(type => type.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, componentContract)))
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), System.StringComparer.Ordinal);
            foreach (var component in components) {
                source.Append("global::ME.BECS.JobStaticInfo<").Append(job).Append(">.maxStructSize = global::Unity.Mathematics.math.max(")
                    .Append("global::ME.BECS.JobStaticInfo<").Append(job).Append(">.maxStructSize, (uint)global::Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<")
                    .Append(component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">());\n");
            }
            source.Append("}\n");
        }
        source.Append("} }\n");
    }
}
