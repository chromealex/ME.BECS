using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class JobEntityInputEmitter {
    private readonly List<(string Identity, INamedTypeSymbol Job, INamedTypeSymbol? Initializer, string[] Groups, string[] RequiredGroups,
        uint[] Counts, uint Maximum, uint Loops, bool Allocate)> plans = new();
    private readonly HashSet<ITypeSymbol> jobs = new(SymbolEqualityComparer.Default);
    internal bool ContainsJob(ITypeSymbol job) => jobs.Contains(job);
    internal int JobCount => jobs.Count;
    private int sourceCount;
    private int fallbackCount;
    private readonly HashSet<ITypeSymbol> ilJobs = new(SymbolEqualityComparer.Default);

    internal bool ReadIL(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid IL job entity counts";
        if (fields.Length != 3 || fields[1] != ilJobs.Count.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 5 || rows[0] != "v1" || !Number(rows[2], out var maximum) || !Number(rows[3], out var loops) ||
            rows[4] is not ("0" or "1")) return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || jobs.Contains(job) ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        if (!MatchesMaximum(job, compilation, maximum)) return false;
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IEntityType");
        var required = new List<string>();
        var groups = new List<string>();
        var counts = new List<uint>();
        ulong totalLoops = 0;
        string? previous = null;
        foreach (var row in rows.Skip(5)) {
            var parts = row.Split('\t');
            if (parts.Length != 5 || !Number(parts[2], out var reserved) || !Number(parts[3], out var inline) ||
                !Number(parts[4], out var loop) || inline == 0u && loop == 0u ||
                reserved != (maximum > 0u && loop > 0u ? maximum : inline)) return false;
            var key = parts[0] + "\t" + parts[1];
            if (previous != null && StringComparer.Ordinal.Compare(previous, key) >= 0) return false;
            previous = key;
            var types = DocumentationCommentId.GetSymbolsForDeclarationId(parts[1], compilation).OfType<INamedTypeSymbol>()
                .Where(type => type.ContainingAssembly.Identity.ToString() == parts[0]).ToArray();
            if (contract == null || types.Length != 1 || !types[0].IsUnmanagedType || types[0].IsRefLikeType || IsOpen(types[0]) ||
                !types[0].AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) return false;
            totalLoops += loop;
            if (totalLoops > uint.MaxValue) return false;
            required.Add(key);
            if (reserved == 0u) continue;
            groups.Add(key);
            counts.Add(reserved);
        }
        if (totalLoops != loops || (rows[4] == "1") != (groups.Count > 0)) return false;
        // Deliberately never consult source catalogs: this is a fresh compiled-IL
        // snapshot, not a fallback that Roslyn may silently replace with a summary.
        jobs.Add(job);
        ilJobs.Add(job);
        plans.Add((rows[1], job, null, groups.ToArray(), required.ToArray(), counts.ToArray(), maximum, loops, groups.Count > 0));
        error = "";
        return true;
    }

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid job entity initializer";
        if (fields.Length != 3 || fields[1] != sourceCount.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 2 || rows[0] is not ("v1" or "v2") || (rows[0] == "v1" ? rows.Length < 3 : rows.Length != 2)) return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || jobs.Contains(job) || !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        var status = CompilerJobStatistics.Entities(job, resolver, compilation, out var selected, out var selectedGroups, out var requiredGroups, out error);
        if (status == CompilerJobCatalogs.Status.Invalid) return false;
        if (status == CompilerJobCatalogs.Status.Complete) {
            jobs.Add(job);
            plans.Add((rows[1], job, selected, selectedGroups, requiredGroups, Array.Empty<uint>(), 0, 0, false));
            ++sourceCount;
            error = "";
            return true;
        }
        if (rows[0] == "v2") return false;
        error = "Invalid job entity initializer";
        var initializer = resolver.ResolveDefinition(rows[2], out _);
        if (job == null || initializer == null || !job.IsUnmanagedType || IsOpen(job) || IsOpen(initializer) ||
            !initializer.IsStatic || !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly) ||
            !compilation.IsSymbolAccessibleWithin(initializer, compilation.Assembly) || jobs.Contains(job)) return false;
        var groups = rows.Skip(3).ToArray();
        if (groups.Distinct(StringComparer.Ordinal).Count() != groups.Length ||
            !groups.SequenceEqual(groups.OrderBy(key => key, StringComparer.Ordinal))) return false;
        var methods = initializer.GetMembers("Apply").OfType<IMethodSymbol>().ToArray();
        if (methods.Length != 1) return false;
        var method = methods[0];
        if (!method.IsStatic || !method.ReturnsVoid || method.Arity != 1 || method.Parameters.Length != groups.Length + 1 ||
            method.Parameters.Any(parameter => parameter.Type.SpecialType != SpecialType.System_UInt32 || parameter.RefKind != RefKind.None) ||
            !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly)) return false;
        var argument = method.TypeParameters[0];
        if (!argument.HasValueTypeConstraint || argument.HasUnmanagedTypeConstraint || argument.HasReferenceTypeConstraint ||
            argument.ConstraintTypes.Length != 0) return false;
        jobs.Add(job);
        plans.Add((rows[1], job, initializer, groups, groups, Array.Empty<uint>(), 0, 0, false));
        ++sourceCount;
        error = "";
        return true;
    }

    internal bool ReadFallback(string[] fields, InputManifestTypes resolver, Compilation compilation, out string error) {
        error = "Invalid job entity fallback";
        if (fields.Length != 3 || fields[1] != fallbackCount.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 5 || rows[0] != "v1" || !Number(rows[2], out var maximum) || !Number(rows[3], out var loops) ||
            (rows[4] != "0" && rows[4] != "1") || rows[4] == "0" && rows.Length != 5) return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || jobs.Contains(job) ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        if (!MatchesMaximum(job, compilation, maximum)) return false;
        var contract = compilation.GetTypeByMetadataName("ME.BECS.IEntityType");
        var groups = new List<string>();
        var counts = new List<uint>();
        for (var index = 5; index < rows.Length; ++index) {
            var parts = rows[index].Split('\t');
            if (parts.Length != 3 || !Number(parts[2], out var count) || count == 0) return false;
            var key = parts[0] + "\t" + parts[1];
            if (groups.Contains(key)) return false;
            var types = DocumentationCommentId.GetSymbolsForDeclarationId(parts[1], compilation).OfType<INamedTypeSymbol>()
                .Where(type => type.ContainingAssembly.Identity.ToString() == parts[0]).ToArray();
            if (contract == null || types.Length != 1 || !types[0].IsUnmanagedType || types[0].IsRefLikeType || IsOpen(types[0]) ||
                !types[0].AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)) return false;
            groups.Add(key);
            counts.Add(count);
        }
        // Editor already selected the compatibility IL analyzer. This is not
        // proof of complete coverage by the newer analyzer (ReadIL), but its
        // counts must not be overridden or blocked by diagnostic catalogs.
        // Keep the existing v1 transport so an already exported manifest can
        // compile and allow Unity to reload the updated Editor exporter.
        jobs.Add(job);
        plans.Add((rows[1], job, null, groups.ToArray(), groups.ToArray(), counts.ToArray(), maximum, loops, rows[4] == "1"));
        ++fallbackCount;
        error = "";
        return true;
    }

    private static bool MatchesMaximum(INamedTypeSymbol job, Compilation compilation, uint maximum) {
        var attributeType = compilation.GetTypeByMetadataName("ME.BECS.EntitiesJobMaxCountAttribute");
        var limits = job.GetAttributes().Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType)).ToArray();
        return limits.Length == 0 ? maximum == 0u : limits.Length == 1 &&
            limits[0].ConstructorArguments.Length == 1 && limits[0].ConstructorArguments[0].Value is uint limit && limit > 0u && limit == maximum;
    }

    private static bool Number(string text, out uint number) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && text == number.ToString(CultureInfo.InvariantCulture);

    internal bool Append(StringBuilder source, IReadOnlyList<INamedTypeSymbol> registrations, out string error) {
        var groups = GroupIds(registrations);
        return Append(source, groups, (uint)registrations.Count, "", out error);
    }

    private static Dictionary<string, uint> GroupIds(IReadOnlyList<INamedTypeSymbol> registrations) =>
        registrations.Select((type, index) => (Key: type.ContainingAssembly.Identity + "\t" + type.GetDocumentationCommentId(), Id: (uint)index))
            .ToDictionary(item => item.Key, item => item.Id, StringComparer.Ordinal);

    internal bool ValidateGroups(IReadOnlyList<INamedTypeSymbol> registrations, out string error) =>
        ValidateGroups(GroupIds(registrations), (uint)registrations.Count, out error);

    private bool ValidateGroups(IReadOnlyDictionary<string, uint> groups, uint groupCount, out string error) {
        foreach (var plan in plans) {
            foreach (var group in plan.RequiredGroups) {
                if (groups.TryGetValue(group, out var id) && id < groupCount) continue;
                error = "Unregistered entity group in job initializer for " + plan.Identity + ": " + group;
                return false;
            }
        }
        error = "";
        return true;
    }

    internal bool Append(StringBuilder source, IReadOnlyDictionary<string, uint> groups, uint groupCount, string suffix, out string error) {
        if (!ValidateGroups(groups, groupCount, out error)) return false;
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static unsafe class JobEntityInputCalls").Append(suffix).Append(" {\n");
        foreach (var plan in plans) {
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize_")
                .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.Identity)).Append("() {\n");
            if (plan.Initializer == null) {
                var target = "global::ME.BECS.JobStaticInfo<" + plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">";
                source.Append(target).Append(".entitiesMaxCount = ").Append(plan.Maximum.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
                    .Append(target).Append(".loopCount = ").Append(plan.Loops.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
                    .Append(target).Append(".inlineCount = ").Append(plan.Allocate ?
                        "global::ME.BECS.Cuts._makeArray<uint>(" + groupCount.ToString(CultureInfo.InvariantCulture) + "u, global::Unity.Collections.Allocator.Domain)" : "default").Append(";\n");
                for (var index = 0; index < plan.Groups.Length; ++index)
                    source.Append(target).Append(".inlineCount[").Append(groups[plan.Groups[index]].ToString(CultureInfo.InvariantCulture))
                        .Append("u] = ").Append(plan.Counts[index].ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                source.Append("}\n");
                continue;
            }
            source.Append(plan.Initializer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(".Apply<")
                .Append(plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(").Append(groupCount.ToString(CultureInfo.InvariantCulture)).Append("u");
            foreach (var group in plan.Groups)
                source.Append(", ").Append(groups[group].ToString(CultureInfo.InvariantCulture)).Append("u");
            source.Append(");\n}\n");
        }
        source.Append("} }\n");
        error = "";
        return true;
    }

    internal void AppendMetadata(StringBuilder source, string key = "ME.BECS.JobEntitySelection.v1") {
        foreach (var plan in plans) {
            var payload = plan.Initializer == null ? "v1\n" + plan.Identity + (ilJobs.Contains(plan.Job) ? "\nil\n" : "\nlegacy\n") +
                plan.Maximum.ToString(CultureInfo.InvariantCulture) + "\n" + plan.Loops.ToString(CultureInfo.InvariantCulture) + "\n" +
                (plan.Allocate ? "1" : "0") + string.Concat(plan.Groups.Select((group, index) => "\n" + group + "\t" + plan.Counts[index].ToString(CultureInfo.InvariantCulture))) :
                "v1\n" + plan.Identity + "\nsource\n" + JobSafetySummary.ReflectionIdentity(plan.Initializer) + string.Concat(plan.Groups.Select(group => "\n" + group));
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(key, true)).Append(", ")
                .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(payload, true)).Append(")]\n");
        }
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
