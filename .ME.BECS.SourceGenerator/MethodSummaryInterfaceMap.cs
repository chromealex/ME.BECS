using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Export the source interface map before a compiler/weaver can introduce metadata-only
// forwarding methods. Never infer the target body from a method name or signature alone.
internal static class MethodSummaryInterfaceMap {
    private const string Prefix = "interface-map-v1=";

    internal static string Flags(IMethodSymbol method) {
        if (method.IsStatic || !method.ContainingType.IsValueType) return "";
        var records = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var contract in method.ContainingType.AllInterfaces) {
            foreach (var member in Members(contract)) {
                if (!SymbolEqualityComparer.Default.Equals(Implementation(method.ContainingType, member), method)) continue;
                var id = MethodSummaryIdentity.Get(member);
                if (id == null) continue;
                var record = string.Join("\n", MethodSummaryType.From(method.ContainingType).Encode(),
                    MethodSummaryType.From(contract).Encode(), member.ContainingAssembly.Identity.ToString(), id);
                records.Add(Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(record)));
            }
        }
        return records.Count == 0 ? "" : "," + string.Join(",", records);
    }

    internal static IEnumerable<IMethodSymbol> Members(INamedTypeSymbol contract) =>
        contract.GetMembers().OfType<IMethodSymbol>().Concat(contract.GetMembers().OfType<IPropertySymbol>()
            .SelectMany(p => new[] { p.GetMethod, p.SetMethod }).Where(m => m != null).Select(m => m!))
            .Distinct(SymbolEqualityComparer.Default).OfType<IMethodSymbol>();

    internal static IMethodSymbol? Implementation(INamedTypeSymbol type, IMethodSymbol member) {
        var result = type.FindImplementationForInterfaceMember(member) as IMethodSymbol;
        if (result == null && member.AssociatedSymbol is IPropertySymbol property &&
            type.FindImplementationForInterfaceMember(property) is IPropertySymbol implemented)
            result = member.MethodKind == MethodKind.PropertyGet ? implemented.GetMethod : implemented.SetMethod;
        return result;
    }

    internal static string? FindSourceBody(MethodSummaryType receiver, MethodSummaryType contract,
        string assembly, string memberId,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts) {
        string? result = null;
        var ownerAssembly = receiver.Identity.Split('\n')[0];
        foreach (var entry in methods) {
            if (entry.Key.Assembly != ownerAssembly) continue;
            foreach (var flag in entry.Value.Flags) {
                if (!flag.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string[] fields;
                try { fields = Encoding.UTF8.GetString(Convert.FromBase64String(flag.Substring(Prefix.Length))).Split('\n'); }
                catch (FormatException) { continue; }
                if (fields.Length != 4 || fields[2] != assembly || fields[3] != memberId ||
                    !MethodSummaryType.TryDecode(fields[0], out var owner) || owner!.Identity != receiver.Identity ||
                    !MethodSummaryType.TryDecode(fields[1], out var mappedContract) ||
                    owner.Arguments.Length != receiver.Arguments.Length) continue;
                var bindings = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
                var valid = true;
                for (var index = 0; index < owner.Arguments.Length; ++index) {
                    if (owner.Arguments[index].Kind != 'p') { valid = false; break; }
                    bindings[owner.Arguments[index].Identity] = receiver.Arguments[index];
                }
                if (!valid || mappedContract!.Substitute(bindings).Encode() != contract.Encode()) continue;
                // Ambiguous or conflicting maps cannot establish coverage.
                if (conflicts.Contains(entry.Key) || (result != null && result != entry.Key.Id)) return null;
                result = entry.Key.Id;
            }
        }
        return result;
    }
}
