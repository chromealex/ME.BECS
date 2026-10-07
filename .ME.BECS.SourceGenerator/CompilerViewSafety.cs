using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Status = ME.BECS.SourceGenerator.CompilerJobCatalogs.Status;

namespace ME.BECS.SourceGenerator;

// Select current callback catalogs, never cached Editor dependency arrays or IL.
// View and module roles can dispatch different bodies on the same concrete type.
internal sealed class CompilerViewSafety {
    private readonly Dictionary<(string Assembly, string Owner, string Phase), List<string[]>> records = new();
    private readonly Dictionary<(string Assembly, string Owner, string Phase), List<string[]>> dispatch = new();

    internal CompilerViewSafety(IEnumerable<IAssemblySymbol> assemblies, System.Threading.CancellationToken cancellation) {
        foreach (var assembly in assemblies)
            foreach (var attribute in assembly.GetAttributes()) {
                cancellation.ThrowIfCancellationRequested();
                if (attribute.AttributeClass?.ToDisplayString() != "System.Reflection.AssemblyMetadataAttribute" || attribute.ConstructorArguments.Length != 2 ||
                    attribute.ConstructorArguments[0].Value is not string key || attribute.ConstructorArguments[1].Value is not string value ||
                    (key != ViewSafetySummary.MetadataKey && key != ViewSafetySummary.DispatchMetadataKey)) continue;
                var rows = value.Split('\n');
                if (key == ViewSafetySummary.DispatchMetadataKey) {
                    if (rows.Length < 3 || rows[1].Length == 0 || rows[2].Length == 0) continue;
                    var dispatchKey = (assembly.Identity.ToString(), rows[1], rows[2]);
                    if (!this.dispatch.TryGetValue(dispatchKey, out var callbacks)) this.dispatch.Add(dispatchKey, callbacks = new());
                    callbacks.Add(rows);
                    continue;
                }
                if (rows.Length < 2 || rows[0].Length == 0 || rows[1].Length == 0) continue;
                var recordKey = (assembly.Identity.ToString(), rows[1], rows[0]);
                if (!this.records.TryGetValue(recordKey, out var entries)) this.records.Add(recordKey, entries = new());
                entries.Add(rows.Skip(1).ToArray());
            }
    }

    internal Status Read(INamedTypeSymbol owner, bool module, string phase, InputManifestTypes resolver, Compilation compilation,
        out (INamedTypeSymbol Type, string Mode)[] dependencies, out bool present, out string error) {
        dependencies = Array.Empty<(INamedTypeSymbol, string)>();
        present = false;
        var qualified = JobSafetySummary.ReflectionIdentity(owner);
        error = "Invalid view safety owner/phase: " + owner + " :: " + phase;
        var view = compilation.GetTypeByMetadataName("ME.BECS.Views.EntityView");
        var moduleContract = compilation.GetTypeByMetadataName("ME.BECS.Views.IViewModule");
        if (qualified == null || owner.IsAbstract || !compilation.IsSymbolAccessibleWithin(owner, compilation.Assembly) ||
            (phase != "ApplyState" && phase != "ApplyStateParallel") ||
            (module ? moduleContract == null || !owner.AllInterfaces.Contains(moduleContract, SymbolEqualityComparer.Default) : !ViewSafetySummary.IsView(owner, view))) return Status.Invalid;
        var isView = ViewSafetySummary.IsView(owner, view);
        var selectedPhase = module && isView ? "module:" + phase : phase;
        var identity = owner.IsGenericType ? qualified : qualified.Substring(0, qualified.Length - owner.ContainingAssembly.Identity.ToString().Length - 2);
        error = "Invalid source view safety for " + qualified + " :: " + selectedPhase;
        string? sourceId = null;
        if (module) {
            var contract = moduleContract!.ContainingAssembly.GetTypeByMetadataName("ME.BECS.Views.IView" + phase);
            if (contract == null) return Status.Invalid;
            if (!owner.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) { error = ""; return Status.Complete; }
            present = true;
            var members = contract.GetMembers(phase).OfType<IMethodSymbol>().ToArray();
            if (members.Length != 1) return Status.Invalid;
            sourceId = resolver.JobCatalogs.SourceInterfaceBody(owner, contract, members[0]);
        } else present = true;
        var key = (owner.ContainingAssembly.Identity.ToString(), identity, selectedPhase);
        if (!this.dispatch.TryGetValue(key, out var dispatches)) {
            // Older v2 safety summaries do not prove source dispatch after Unity
            // weaving. Keep them explicitly transitional; do not guess a body.
            error = "Missing source view callback dispatch contract for " + qualified + " :: " + selectedPhase + ". Recompile the owning assembly with the current generator";
            return Status.Incomplete;
        }
        {
            if (dispatches.Count != 1) return Status.Invalid;
            var record = dispatches[0];
            if (record.Length != 6 || record[0] != "v1" || !MethodSummaryType.TryDecode(record[3], out var expression) || expression!.IsOpen || expression.IsUnsupported ||
                MethodSummaryTypeResolver.Resolve(expression, compilation) is not INamedTypeSymbol declaring || declaring.ContainingAssembly.Identity.ToString() != record[4]) return Status.Invalid;
            var ancestor = false;
            for (var type = owner; type != null; type = type.BaseType)
                if (SymbolEqualityComparer.Default.Equals(type, declaring)) ancestor = true;
            var prefix = "M:" + declaring.OriginalDefinition.GetDocumentationCommentId()!.Substring(2) + ".";
            const string suffix = "(ME.BECS.Views.ViewData@)";
            if (!ancestor || !record[5].StartsWith(prefix, StringComparison.Ordinal) ||
                !record[5].EndsWith(suffix, StringComparison.Ordinal) || sourceId != null && sourceId != record[5]) return Status.Invalid;
            var methodName = record[5].Substring(prefix.Length, record[5].Length - prefix.Length - suffix.Length);
            if (methodName != phase && (!module || methodName != "ME#BECS#Views#IView" + phase + "#" + phase)) return Status.Invalid;
            sourceId = record[5];
        }
        if (!this.records.TryGetValue((owner.ContainingAssembly.Identity.ToString(), identity, selectedPhase), out var entries)) {
            error = "Missing source view safety for " + qualified + " :: " + selectedPhase;
            return Status.Missing;
        }
        if (entries.Count != 1) return Status.Invalid;
        var rows = entries[0];
        if (rows.Length < 3 || !CompilerJobCatalogs.Number(rows[2], out var gaps)) return Status.Invalid;
        var gapRows = rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).ToArray();
        if ((gaps == 0) != (gapRows.Length == 0) || gapRows.Any(row => row.Length == 2)) return Status.Invalid;
        // Unavailable callbacks may use a diagnostic placeholder instead of a
        // source body ID. They must not be certified as a complete empty tracker.
        if (gaps != 0) { error = "Incomplete source view safety for " + qualified + " :: " + selectedPhase; return Status.Incomplete; }
        if (rows[1] != sourceId) return Status.Invalid;
        return resolver.JobSafety.ReadRows(owner, owner.ContainingAssembly, rows, compilation, out dependencies, out error);
    }
}
