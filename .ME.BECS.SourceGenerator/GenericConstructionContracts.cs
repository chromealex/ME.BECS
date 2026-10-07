using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class GenericConstructionContracts {
    internal const string Schema = "generic-construction-schema=1";
    private const string Marker = "generic-construction";

    internal static string Operation(ITypeSymbol type, int loopDepth) => "new\t" + loopDepth.ToString(CultureInfo.InvariantCulture) +
        "\t" + type.ContainingAssembly.Identity + "\tM:__GenericConstruction\t" + MethodSummaryType.From(type).Encode() + "\t!" + Marker;

    internal static string[] Resolve(string[] row, Compilation compilation,
        IReadOnlyDictionary<string, MethodSummaryType> bindings, ISet<string> gaps) {
        if (!MethodSummaryContracts.Has(row, Marker)) return row;
        if (row[0] != "new" || !MethodSummaryType.TryDecode(row[4], out var expression)) {
            gaps.Add("MalformedGenericConstruction"); return row;
        }
        var concrete = expression!.Substitute(bindings);
        var type = MethodSummaryTypeResolver.Resolve(concrete, compilation) as INamedTypeSymbol;
        if (type == null || concrete.IsOpen || concrete.IsUnsupported || type.IsAbstract || type.TypeKind == TypeKind.Interface) {
            gaps.Add("UnresolvedGenericConstruction: " + concrete.Identity.Replace('\n', ' ')); return row;
        }
        var constructors = type.InstanceConstructors.Where(method => method.Parameters.Length == 0).ToArray();
        if (type.IsValueType && (constructors.Length == 0 || constructors.Length == 1 && constructors[0].IsImplicitlyDeclared)) {
            // Only an implicit/default value constructor is zero initialization.
            // A C# 10 struct can instead have a user-defined parameterless body.
            return new[] { "new", row[1], type.ContainingAssembly.Identity.ToString(), "M:__DefaultValueConstruction",
                concrete.Encode(), "!ecs-leaf", "!default-value-construction" };
        }
        if (constructors.Length != 1 || constructors[0].DeclaredAccessibility != Accessibility.Public ||
            !int.TryParse(row[1], NumberStyles.None, CultureInfo.InvariantCulture, out var loopDepth)) {
            gaps.Add("UnavailableGenericConstructor: " + type.ToDisplayString()); return row;
        }
        return MethodSummaryContracts.Operation("new", loopDepth, constructors[0], compilation,
            new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default))!.Split('\t');
    }
}
