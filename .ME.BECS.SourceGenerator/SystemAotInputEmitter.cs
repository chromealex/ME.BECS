using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ME.BECS.SourceGenerator;

internal sealed class SystemAotInputEmitter {
    internal readonly StringBuilder Metadata = new();
    internal readonly StringBuilder Body = new();

    internal static SystemAotInputEmitter? Create(Compilation compilation,
        IReadOnlyList<(string Identity, INamedTypeSymbol Type)> registrations, out string error) {
        error = "";
        var result = new SystemAotInputEmitter();
        var phases = new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" };
        result.Body.Append("namespace ME.BECS.SourceGenerated { internal static class SystemAotInputs {\n")
            .Append("[global::UnityEngine.Scripting.PreserveAttribute] public static void PreserveReferences() {\n")
            .Append("var context = new global::ME.BECS.SystemContext();\n");
        foreach (var registration in registrations) {
            var type = registration.Type;
            var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var present = 0;
            var factories = 0;
            for (var i = 0; i < phases.Length; ++i) {
                var contract = compilation.GetTypeByMetadataName("ME.BECS.I" + phases[i]);
                if (contract == null || !type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) continue;
                var member = contract.GetMembers("On" + phases[i]).OfType<IMethodSymbol>().SingleOrDefault();
                if (member == null || type.FindImplementationForInterfaceMember(member) is not IMethodSymbol method) {
                    error = "Cannot resolve AOT lifecycle implementation: " + registration.Identity + " / " + phases[i];
                    return null;
                }
                present |= 1 << i;
                if (!HasAttribute(method, "ME.BECS.WithoutBurstAttribute")) factories |= 1 << i;
            }
            var burst = HasAttribute(type, "Unity.Burst.BurstCompileAttribute") ? factories : 0;
            result.Metadata.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.SystemAotPlan.v1\", ")
                .Append(SymbolDisplay.FormatLiteral(registration.Identity + "\n" + present.ToString(CultureInfo.InvariantCulture) + "\n" +
                    burst.ToString(CultureInfo.InvariantCulture) + "\n" + factories.ToString(CultureInfo.InvariantCulture), true)).Append(")]\n");
            result.Body.Append("SystemInputs.Register_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
            void Pointers(string kind, int mask) {
                for (var i = 0; i < phases.Length; ++i)
                    if ((mask & (1 << i)) != 0) result.Body.Append("global::ME.BECS.SourceGeneratorSystemAot.")
                        .Append(kind).Append(phases[i]).Append('<').Append(name).Append(">();\n");
            }
            Pointers("Burst", burst);
            Pointers("NoBurst", present);
            for (var i = 0; i < phases.Length; ++i)
                if ((present & (1 << i)) != 0) result.Body.Append("{ var system = new ").Append(name)
                    .Append("(); global::ME.BECS.SourceGeneratorSystemCalls.").Append(phases[i])
                    .Append("(ref system, ref context); }\n");
            Pointers("Factory", factories);
        }
        result.Body.Append("} } }\n");
        return result;
    }

    private static bool HasAttribute(ISymbol symbol, string name) {
        foreach (var attribute in symbol.GetAttributes())
            for (var type = attribute.AttributeClass; type != null; type = type.BaseType)
                if (type.ToDisplayString() == name) return true;
        return false;
    }
}
