using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class DestroyInputEmitter {
    internal static void EmitCatalog(SourceProductionContext output, Compilation compilation, IReadOnlyList<INamedTypeSymbol> components) {
        var catalog = MethodSummaryGraph.LoadCatalog(output, compilation, System.Array.Empty<string>());
        var rows = new StringBuilder();
        var safetyMetadata = new StringBuilder();
        var missing = 0;
        for (var index = 0; index < components.Count; ++index) {
            var component = components[index];
            var contract = component.AllInterfaces.FirstOrDefault(type => type.ToDisplayString() == "ME.BECS.IComponentDestroy");
            var member = contract?.GetMembers("Destroy").OfType<IMethodSymbol>().SingleOrDefault();
            var implementation = member == null ? null : component.FindImplementationForInterfaceMember(member) as IMethodSymbol;
            var id = implementation == null ? null : MethodSummaryIdentity.Get(implementation);
            if (id == null) {
                ++missing;
                rows.Append("G\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append("\tUnresolvedDestroyImplementation\n");
                continue;
            }
            // Preserve manifest order and the constructed receiver for generic substitution.
            // Explicit interface implementations resolve to their actual method, not an
            // assumed public method named Destroy. No callback is invoked here.
            rows.Append("C\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(MethodSummaryType.From(component).Encode()).Append('\t')
                .Append(implementation!.ContainingAssembly.Identity).Append('\t').Append(id).Append('\t')
                .Append(MethodSummaryType.From(implementation.ContainingType).Encode()).Append('\n');
            // A resolved method symbol alone does not establish analyzable callback coverage.
            // Distinguish missing/duplicate bodies and locally incomplete summaries; even an
            // available body still needs transitive closure in the manifest-level analysis.
            var key = (implementation.ContainingAssembly.Identity.ToString(), id);
            if (!catalog.Methods.ContainsKey(key) && !catalog.Conflicts.Contains(key)) {
                // Metadata can expose an interface forwarding method absent from source.
                // Resolve only an unambiguous source-exported interface-map binding, never
                // a same-name public method that might not implement this interface.
                var ownerFlag = "destroy-owner=" + MethodSummaryType.From(component).Encode();
                var mapped = catalog.Methods.Where(entry => entry.Key.Assembly == key.Item1 &&
                    entry.Value.Flags.Contains(ownerFlag)).ToArray();
                if (mapped.Length == 1 && !catalog.Conflicts.Contains(mapped[0].Key)) {
                    key = mapped[0].Key;
                    rows.Append("A\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(key.Item1).Append('\t').Append(key.Item2).Append('\n');
                }
            }
            var status = catalog.Conflicts.Contains(key) ? "ambiguous" :
                !catalog.Methods.TryGetValue(key, out var summary) ? "missing" :
                summary.Unresolved.Length != 0 ? "local-gaps" : "available";
            rows.Append("S\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(status).Append('\n');
            if (!catalog.Conflicts.Contains(key) && catalog.Methods.TryGetValue(key, out var methodSummary)) {
                var root = new MethodSummaryGraph.Summary {
                    Id = methodSummary.Id, Environment = methodSummary.Environment,
                    Unresolved = methodSummary.Unresolved, RootAssembly = key.Item1,
                    RootArguments = MethodSummaryType.From(implementation.ContainingType).Arguments,
                    Flags = methodSummary.Flags.Concat(new[] { "job-type=" + MethodSummaryType.From(component).Encode(),
                        "entity-limit-schema=1", "entity-max-count=0" }).ToArray(),
                };
                root.Operations.AddRange(methodSummary.Operations);
                // The callback receives writable component storage; mutations through this
                // have no Get<T>/Set<T> call for the API-based safety walker to discover.
                root.Operations.Add(new[] { "parameter-override", "0", key.Item1, id,
                    MethodSummaryType.From(implementation.ContainingType).Encode(),
                    MethodSummaryType.From(component).Encode(), "!mode=2" });
                if (root.Environment.Length != root.RootArguments.Length) {
                    ++missing;
                    rows.Append("G\t").Append(index.ToString(CultureInfo.InvariantCulture)).Append("\tDestroyGenericArityMismatch\n");
                    continue;
                }
                MethodSummaryType? Decode(string token) => MethodSummaryType.TryDecode(token, out var value) ? value : null;
                var safety = JobSafetySummary.Analyze(output, compilation, key.Item1, root, catalog.Methods,
                    catalog.Conflicts, Decode, emitInitializer: false);
                safetyMetadata.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.DestroyCallbackSafety.v1\", ")
                    .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(safety, true)).Append(")]\n");
                var counts = JobEntitySummary.Analyze(output, compilation, key.Item1, root, catalog.Methods,
                    catalog.Conflicts, Decode, emitInitializer: false);
                safetyMetadata.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.DestroyCallbackCounts.v1\", ")
                    .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(counts, true)).Append(")]\n");
            }
        }
        var payload = "ME.BECS.DestroyCallbackTargets.v1\n" + components.Count.ToString(CultureInfo.InvariantCulture) + "\n" +
            missing.ToString(CultureInfo.InvariantCulture) + "\n" + rows;
        output.AddSource("ME.BECS.DestroyCallbackTargets.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(
            "// <auto-generated/>\n[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.DestroyCallbackTargets.v1\", " +
            Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(payload, true) + ")]\n" + safetyMetadata, Encoding.UTF8));
    }

    internal static void Append(StringBuilder source, IReadOnlyList<INamedTypeSymbol> components) {
        source.Append("namespace ME.BECS.SourceGenerated { [global::Unity.Burst.BurstCompile] internal static unsafe class DestroyInputs {\n")
            .Append("public static void Initialize() {\n");
        for (var index = 0; index < components.Count; ++index)
            source.Append("global::ME.BECS.WorldStaticCallbacks.RegisterAutoDestroyCallback<")
                .Append(components[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(Destroy_")
                .Append(index.ToString(CultureInfo.InvariantCulture)).Append(");\n");
        source.Append("}\n[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n")
            .Append("private static void Invoke<T>(in global::ME.BECS.Ent ent, byte* comp) where T : unmanaged, global::ME.BECS.IComponentDestroy {\n")
            .Append("if (comp == null) { var value = default(T); value.Destroy(in ent); } else { ref T value = ref *(T*)comp; value.Destroy(in ent); }\n}\n");
        for (var index = 0; index < components.Count; ++index)
            source.Append("[global::Unity.Burst.BurstCompile]\n[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.AutoDestroyRegistry.DestroyDelegate))]\n")
                .Append("public static void Destroy_").Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("(in global::ME.BECS.Ent ent, byte* comp) => Invoke<")
                .Append(components[index].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(in ent, comp);\n");
        source.Append("} }\n");
    }
}
