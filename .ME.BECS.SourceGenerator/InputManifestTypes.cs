using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class InputManifestTypes {
    private readonly Dictionary<string, IAssemblySymbol> assemblies = new Dictionary<string, IAssemblySymbol>(StringComparer.Ordinal);

    internal InputManifestTypes(Compilation compilation, System.Threading.CancellationToken cancellation) {
        var pending = new Queue<IAssemblySymbol>();
        pending.Enqueue(compilation.Assembly);
        while (pending.Count != 0) {
            cancellation.ThrowIfCancellationRequested();
            var assembly = pending.Dequeue();
            var identity = assembly.Identity.ToString();
            if (this.assemblies.ContainsKey(identity)) continue;
            this.assemblies.Add(identity, assembly);
            foreach (var module in assembly.Modules)
                foreach (var reference in module.ReferencedAssemblySymbols) pending.Enqueue(reference);
        }
    }

    internal INamedTypeSymbol? ResolveDefinition(string identity, out string? gap) {
        var budget = 4096;
        return this.Resolve(identity, ref budget, 0, out gap);
    }

    private INamedTypeSymbol? Resolve(string identity, ref int budget, int depth, out string? gap) {
        gap = null;
        if (--budget < 0 || depth > 64 || identity.Length > 262144) { gap = "TypeResolutionLimit"; return null; }
        var nesting = 0;
        var separator = -1;
        for (var i = 0; i < identity.Length; ++i) {
            if (identity[i] == '[') ++nesting;
            else if (identity[i] == ']') {
                if (--nesting < 0) { gap = "InvalidGenericBrackets"; return null; }
            } else if (identity[i] == ',' && nesting == 0) { separator = i; break; }
        }
        if (separator <= 0 || separator + 1 >= identity.Length || identity[separator + 1] != ' ') { gap = "InvalidAssemblyQualifiedType"; return null; }
        var assemblyIdentity = identity.Substring(separator + 2);
        if (!this.assemblies.TryGetValue(assemblyIdentity, out var assembly)) { gap = "AssemblyNotReferenced"; return null; }
        var name = identity.Substring(0, separator);
        var genericStart = name.IndexOf("[[", StringComparison.Ordinal);
        var definitionName = genericStart < 0 ? name : name.Substring(0, genericStart);
        if (definitionName.IndexOfAny(new[] { '[', ']', '*', '&', '\\' }) >= 0) { gap = "UnsupportedTypeShape"; return null; }
        var type = assembly.GetTypeByMetadataName(definitionName);
        if (type == null) { gap = "TypeNotFound"; return null; }
        if (type.ContainingAssembly.Identity.ToString() != assemblyIdentity) { gap = "ForwardedTypeIdentity"; return null; }
        if (genericStart < 0) return type;
        if (!name.EndsWith("]]", StringComparison.Ordinal)) { gap = "InvalidGenericArguments"; return null; }
        var arguments = new List<ITypeSymbol>();
        var position = genericStart + 1;
        while (position < name.Length - 1) {
            if (name[position] != '[') { gap = "InvalidGenericArguments"; return null; }
            var start = ++position;
            nesting = 1;
            while (position < name.Length && nesting != 0) {
                if (name[position] == '[') ++nesting;
                else if (name[position] == ']') --nesting;
                if (nesting != 0) ++position;
            }
            if (nesting != 0) { gap = "InvalidGenericArguments"; return null; }
            var argument = this.Resolve(name.Substring(start, position - start), ref budget, depth + 1, out gap);
            if (argument == null) return null;
            // Closed manifests must not smuggle an open definition as a type argument.
            if (MethodSummaryType.From(argument).IsOpen) { gap = "OpenGenericArgument"; return null; }
            arguments.Add(argument);
            ++position;
            if (position == name.Length - 1) break;
            if (name[position++] != ',') { gap = "InvalidGenericArguments"; return null; }
        }
        if (position != name.Length - 1) { gap = "InvalidGenericArguments"; return null; }
        var owners = MethodSummaryType.TypeOwners(type).ToArray();
        if (owners.Sum(t => t.Arity) != arguments.Count) { gap = "GenericArityMismatch"; return null; }
        INamedTypeSymbol? result = null;
        var offset = 0;
        foreach (var owner in owners) {
            var candidates = result == null ? new[] { owner } : result.GetTypeMembers(owner.Name, owner.Arity)
                .Where(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, owner.OriginalDefinition)).ToArray();
            if (candidates.Length != 1) { gap = "NestedTypeNotFound"; return null; }
            result = candidates[0];
            if (owner.Arity == 0) continue;
            try { result = result.Construct(arguments.Skip(offset).Take(owner.Arity).ToArray()); }
            catch (ArgumentException) { gap = "InvalidTypeConstruction"; return null; }
            offset += owner.Arity;
        }
        return result;
    }
}
