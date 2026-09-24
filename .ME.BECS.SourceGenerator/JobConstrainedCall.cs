using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobConstrainedCall {
    internal static string[] Resolve(string[] row, Compilation compilation,
        IReadOnlyDictionary<string, MethodSummaryType> bindings, ISet<string> gaps,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts) {
        if (row.Length < 5) { gaps.Add("MalformedConstrainedOperation"); return row; }
        var token = MethodSummaryContracts.Value(row, "constrained");
        if (token == null) return row;
        var receiverDescription = "invalid type expression";
        if (MethodSummaryType.TryDecode(token, out var expression)) {
            var concrete = expression!.Substitute(bindings);
            receiverDescription = Describe(concrete);
            if (concrete.Kind == 'n') {
                var type = MethodSummaryTypeResolver.Resolve(concrete, compilation) as INamedTypeSymbol;
                // Match the actual interface map, not a method with the same name. This also
                // resolves explicit implementations and overloads without runtime reflection.
                if (type != null && type.IsValueType) {
                    if (!MethodSummaryType.TryDecode(row[4], out var declaredContract)) {
                        gaps.Add("MalformedConstrainedContract: " + row[3]);
                        return row;
                    }
                    var closedContract = declaredContract!.Substitute(bindings);
                    foreach (var contract in type.AllInterfaces) {
                        // Match the constructed interface, not only its definition. A type
                        // can implement IFoo<A> and IFoo<B> with different method bodies.
                        if (MethodSummaryType.From(contract).Encode() != closedContract.Encode()) continue;
                        foreach (var member in MethodSummaryInterfaceMap.Members(contract)) {
                            if (MethodSummaryIdentity.Get(member) != row[3] || member.ContainingAssembly.Identity.ToString() != row[2]) continue;
                            var implementation = MethodSummaryInterfaceMap.Implementation(type, member);
                            if (implementation == null || implementation.IsAbstract) continue;
                            // The row already carries constructed method arguments in caller
                            // scope. Interface mapping preserves their positional correspondence.
                            if (implementation.Arity != member.Arity) continue;
                            var id = MethodSummaryIdentity.Get(implementation);
                            if (id == null) continue;
                            var result = (string[])row.Clone();
                            result[2] = implementation.ContainingAssembly.Identity.ToString();
                            result[3] = id;
                            result[4] = MethodSummaryType.From(implementation.ContainingType).Encode();
                            if (!methods.ContainsKey((result[2], result[3])) &&
                                SymbolEqualityComparer.Default.Equals(implementation.ContainingType, type)) {
                                var sourceId = MethodSummaryInterfaceMap.FindSourceBody(concrete, closedContract,
                                    row[2], row[3], methods, conflicts);
                                if (sourceId != null) result[3] = sourceId;
                            }
                            if (MethodSummaryContracts.IsScalarComparison(implementation))
                                result = result.Concat(new[] { "!scalar-comparison" }).ToArray();
                            return result;
                        }
                    }
                }
            }
        }
        gaps.Add("UnresolvedConstrainedCall: " + row[3] + " | receiver=" + receiverDescription);
        return row;
    }

    private static string Describe(MethodSummaryType type, int depth = 0) {
        if (depth > 8) return "...";
        return type.Identity.Replace('\n', ' ') + (type.Arguments.Length == 0 ? "" :
            "<" + string.Join(", ", type.Arguments.Select(t => Describe(t, depth + 1))) + ">");
    }
}
