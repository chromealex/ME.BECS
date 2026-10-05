using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class ViewCallbackBodies {
    private readonly Compilation compilation;
    private readonly System.Threading.CancellationToken cancellation;
    private readonly Dictionary<IAssemblySymbol, Dictionary<string, string>?> catalogs = new(SymbolEqualityComparer.Default);

    internal ViewCallbackBodies(Compilation compilation, System.Threading.CancellationToken cancellation) {
        this.compilation = compilation;
        this.cancellation = cancellation;
    }

    internal bool Read(IMethodSymbol method, out string status) {
        method = method.OriginalDefinition;
        if (method.DeclaringSyntaxReferences.Length > 0 &&
            this.compilation.ContainsSyntaxTree(method.DeclaringSyntaxReferences[0].SyntaxTree)) {
            status = ViewCallbackBodyGenerator.IsEmpty(method, this.compilation, this.cancellation) ? "empty" : "active";
            return true;
        }
        var assembly = method.ContainingAssembly;
        if (!this.catalogs.TryGetValue(assembly, out var catalog)) {
            catalog = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in assembly.GetAttributes()) {
                this.cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" ||
                    attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value as string != ViewCallbackBodyGenerator.MetadataKey) continue;
                var rows = (attribute.ConstructorArguments[1].Value as string)?.Split('\n');
                if (rows == null || rows.Length != 3 || rows[0] != "v1" || !rows[1].StartsWith("M:", StringComparison.Ordinal) ||
                    (rows[2] != "empty" && rows[2] != "active") || catalog.ContainsKey(rows[1])) { catalog = null; break; }
                catalog.Add(rows[1], rows[2]);
            }
            this.catalogs.Add(assembly, catalog);
        }
        status = "unknown";
        if (catalog == null) return false;
        var id = method.GetDocumentationCommentId();
        if (id != null && catalog.TryGetValue(id, out var body)) status = body;
        return true;
    }
}
