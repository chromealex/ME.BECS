using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Status = ME.BECS.SourceGenerator.CompilerJobCatalogs.Status;

namespace ME.BECS.SourceGenerator;

internal static class CompilerJobStatistics {
    internal static Status Weight(INamedTypeSymbol job, InputManifestTypes resolver, Compilation compilation,
        out INamedTypeSymbol? initializer, out uint weight, out string error) {
        initializer = null;
        weight = 0;
        var catalogs = resolver.JobCatalogs;
        var status = catalogs.Read(job, JobWeightSummary.MetadataKey, out var rows, out var producer, out error);
        if (status != Status.Complete) return status;
        if (rows.Length < 4 || !CompilerJobCatalogs.Number(rows[3], out weight)) return Status.Invalid;
        string[]? init = null;
        string? previous = null;
        foreach (var row in rows.Skip(4)) {
            catalogs.Cancellation.ThrowIfCancellationRequested();
            var fields = row.Split('\t');
            if (fields[0] == "I") {
                if (init != null || fields.Length != 4 || fields[3] != "Apply") return Status.Invalid;
                init = fields;
            } else if (fields[0] == "W") {
                if (fields.Length != 3 || fields[1].Length == 0 || !CompilerJobCatalogs.Number(fields[2], out _) ||
                    previous != null && StringComparer.Ordinal.Compare(previous, fields[1]) >= 0) return Status.Invalid;
                previous = fields[1];
            } else return Status.Invalid;
        }
        if (init == null) {
            error = "Source weight initializer unavailable for " + JobSafetySummary.ReflectionIdentity(job);
            return Status.Incomplete;
        }
        initializer = Initializer(job, compilation, catalogs, producer, rows, init, "JobWeight_", 0);
        if (initializer == null) return Status.Invalid;
        error = "";
        return Status.Complete;
    }

    internal static Status Entities(INamedTypeSymbol job, InputManifestTypes resolver, Compilation compilation,
        out INamedTypeSymbol? initializer, out string[] groups, out string[] allGroups, out string error) {
        initializer = null;
        groups = allGroups = Array.Empty<string>();
        var catalogs = resolver.JobCatalogs;
        var status = catalogs.Read(job, JobEntitySummary.MetadataKey, out var rows, out var producer, out error);
        if (status != Status.Complete) return status;
        var attributeType = compilation.GetTypeByMetadataName("ME.BECS.EntitiesJobMaxCountAttribute");
        var attributes = job.GetAttributes().Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)).ToArray();
        uint maximum = 0;
        if (attributes.Length > 1 || attributes.Length == 1 &&
            (attributes[0].ConstructorArguments.Length != 1 || attributes[0].ConstructorArguments[0].Value is not uint value || value == 0)) return Status.Invalid;
        if (attributes.Length == 1) maximum = (uint)attributes[0].ConstructorArguments[0].Value!;
        var limitSeen = false;
        string[]? init = null;
        string? previous = null;
        var reserved = new List<string>();
        var required = new List<string>();
        ulong loops = 0;
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IEntityType");
        foreach (var row in rows.Skip(3)) {
            catalogs.Cancellation.ThrowIfCancellationRequested();
            var fields = row.Split('\t');
            if (fields[0] == "L") {
                if (limitSeen || fields.Length != 2 || !CompilerJobCatalogs.Number(fields[1], out var limit) || limit != maximum) return Status.Invalid;
                limitSeen = true;
            } else if (fields[0] == "I") {
                if (init != null || fields.Length != 5 || fields[3] != "Apply" || fields[4] != "v3") return Status.Invalid;
                init = fields;
            } else if (fields[0] == "C") {
                if (fields.Length != 5 || contract == null || !CompilerJobCatalogs.Number(fields[3], out var inline) ||
                    !CompilerJobCatalogs.Number(fields[4], out var loop)) return Status.Invalid;
                var identity = fields[1] + "\n" + fields[2];
                if (previous != null && StringComparer.Ordinal.Compare(previous, identity) >= 0) return Status.Invalid;
                previous = identity;
                var matches = DocumentationCommentId.GetSymbolsForDeclarationId(fields[2], compilation).OfType<INamedTypeSymbol>()
                    .Where(type => type.ContainingAssembly.Identity.ToString() == fields[1]).ToArray();
                if (matches.Length != 1 || !matches[0].IsUnmanagedType || matches[0].IsRefLikeType || MethodSummaryType.From(matches[0]).IsOpen ||
                    !matches[0].AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) return Status.Invalid;
                loops += loop;
                if (loops > uint.MaxValue) return Status.Invalid;
                var key = fields[1] + "\t" + fields[2];
                required.Add(key);
                if (inline > 0 || maximum > 0 && loop > 0) reserved.Add(key);
            } else return Status.Invalid;
        }
        if (!limitSeen) return Status.Invalid;
        if (init == null) {
            error = "Source entity-count v3 initializer unavailable for " + JobSafetySummary.ReflectionIdentity(job);
            return Status.Incomplete;
        }
        initializer = Initializer(job, compilation, catalogs, producer, rows, init, "JobEntityCounts_", reserved.Count + 1);
        if (initializer == null) return Status.Invalid;
        groups = reserved.ToArray();
        allGroups = required.ToArray();
        error = "";
        return Status.Complete;
    }

    private static INamedTypeSymbol? Initializer(INamedTypeSymbol job, Compilation compilation, CompilerJobCatalogs catalogs,
        IAssemblySymbol originalProducer, string[] rows, string[] init, string prefix, int parameterCount) {
        var expected = "ME.BECS.SourceGenerated." + prefix +
            ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(init[1] + "\n" + rows[0] + "\n" + rows[1]);
        if (init[2] != expected) return null;
        var producer = catalogs.Producer(job, originalProducer, init[1]);
        var type = producer?.GetTypeByMetadataName(expected);
        if (type == null || !type.IsStatic || MethodSummaryType.From(type).IsOpen || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) return null;
        var methods = type.GetMembers("Apply").OfType<IMethodSymbol>().ToArray();
        if (methods.Length != 1) return null;
        var method = methods[0];
        if (!method.IsStatic || !method.ReturnsVoid || method.Arity != 1 || method.Parameters.Length != parameterCount ||
            method.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_UInt32 || parameter.RefKind != RefKind.None) ||
            !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly)) return null;
        var argument = method.TypeParameters[0];
        return argument.HasValueTypeConstraint && !argument.HasUnmanagedTypeConstraint && !argument.HasReferenceTypeConstraint &&
            argument.ConstraintTypes.Length == 0 ? type : null;
    }
}
