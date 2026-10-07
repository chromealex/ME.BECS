using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class DestroyDispatchContracts {
    internal const string Schema = "destroy-dispatch-schema=1";

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        var ent = compilation.GetTypeByMetadataName("ME.BECS.Ent");
        if (!method.IsStatic || ent == null || !SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, ent.ContainingAssembly)) return;
        // Exact engine boundaries whose bodies can be hidden by SafetyCheck/Ignore.
        // A matching method name or SafetyCheck on user code is not this contract.
        var id = method.OriginalDefinition.GetDocumentationCommentId();
        if (id is "M:ME.BECS.EntExt.Set``1(ME.BECS.Ent@,``0@)" or
            "M:ME.BECS.EntExt.Remove``1(ME.BECS.Ent@)" or
            "M:ME.BECS.Batches.Set``1(ME.BECS.Ent@,``0@,ME.BECS.safe_ptr{ME.BECS.State})" or
            "M:ME.BECS.Batches.Remove``1(ME.BECS.Ent@,ME.BECS.safe_ptr{ME.BECS.State})") {
            row.Append("\t!destroy-component=").Append(MethodSummaryType.From(method.TypeArguments[0]).Encode());
        } else if (id is "M:ME.BECS.EntExt.Destroy(ME.BECS.Ent@)" or
            "M:ME.BECS.Batches.Set(ME.BECS.Ent@,System.UInt32,System.Void*,ME.BECS.safe_ptr{ME.BECS.State})" or
            "M:ME.BECS.Batches.Remove(ME.BECS.Ent@,System.UInt32,ME.BECS.safe_ptr{ME.BECS.State})") {
            row.Append("\t!destroy-registry");
        }
    }

    internal static INamedTypeSymbol? Component(string[] operation, Compilation compilation,
        IReadOnlyDictionary<string, MethodSummaryType> environment, Func<string, MethodSummaryType?> decode, ISet<string> gaps) {
        if (operation[0] == "method-ref") return null;
        if (MethodSummaryContracts.Has(operation, "destroy-registry")) {
            gaps.Add("UnclosedDestroyRegistry: " + operation[3]);
            return null;
        }
        var token = MethodSummaryContracts.Value(operation, "destroy-component");
        if (token == null) return null;
        var expression = decode(token)?.Substitute(environment);
        var component = expression == null || expression.IsOpen || expression.IsUnsupported ? null :
            MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol;
        if (component == null) { gaps.Add("UnresolvedDestroyComponent: " + operation[3]); return null; }
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IComponentDestroy");
        if (contract == null || !component.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) return null;
        // The public registry accepts arbitrary callback replacements. The default
        // interface body is useful diagnostic coverage, not a closed dispatch proof.
        gaps.Add("UnclosedDestroyRegistry: " + component.ToDisplayString());
        return component;
    }

    internal static (string Assembly, string Id, MethodSummaryType[] Arguments)? DefaultTarget(INamedTypeSymbol component,
        Compilation compilation, IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, ISet<string> gaps) {
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IComponentDestroy");
        var member = contract?.GetMembers("Destroy").OfType<IMethodSymbol>().SingleOrDefault();
        var implementation = member == null ? null : MethodSummaryInterfaceMap.Implementation(component, member);
        var id = implementation == null ? null : MethodSummaryIdentity.Get(implementation);
        var receiver = MethodSummaryType.From(component);
        var assembly = component.ContainingAssembly.Identity.ToString();
        if (id == null || !methods.ContainsKey((assembly, id))) {
            // A metadata-only forwarding stub must not replace an explicit source
            // implementation; use the exported interface map, including substitutions.
            id = member == null ? null : MethodSummaryInterfaceMap.FindSourceBody(receiver, MethodSummaryType.From(contract!),
                contract!.ContainingAssembly.Identity.ToString(), MethodSummaryIdentity.Get(member)!, methods, conflicts);
        }
        if (id == null || conflicts.Contains((assembly, id)) || !methods.ContainsKey((assembly, id))) {
            gaps.Add("UnavailableDestroyCallback: " + component.ToDisplayString());
            return null;
        }
        return (assembly, id, receiver.Arguments);
    }
}
