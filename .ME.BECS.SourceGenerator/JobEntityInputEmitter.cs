using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal sealed class JobEntityInputEmitter {
    private readonly List<(string Identity, INamedTypeSymbol Job, INamedTypeSymbol? Initializer, string[] Groups,
        uint[] Counts, uint Maximum, uint Loops, bool Allocate)> plans = new();
    private readonly HashSet<ITypeSymbol> jobs = new(SymbolEqualityComparer.Default);
    internal bool ContainsJob(ITypeSymbol job) => jobs.Contains(job);
    internal int JobCount => jobs.Count;
    private int sourceCount;
    private int fallbackCount;

    internal bool Read(string[] fields, InputManifestTypes resolver, Compilation compilation) {
        if (fields.Length != 3 || fields[1] != sourceCount.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 3 || rows[0] != "v1") return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
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
        plans.Add((rows[1], job, initializer, groups, Array.Empty<uint>(), 0, 0, false));
        ++sourceCount;
        return true;
    }

    internal bool ReadFallback(string[] fields, InputManifestTypes resolver, Compilation compilation) {
        if (fields.Length != 3 || fields[1] != fallbackCount.ToString(CultureInfo.InvariantCulture)) return false;
        string[] rows;
        try { rows = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])).Split('\n'); }
        catch (FormatException) { return false; }
        if (rows.Length < 5 || rows[0] != "v1" || !Number(rows[2], out var maximum) || !Number(rows[3], out var loops) ||
            (rows[4] != "0" && rows[4] != "1") || rows[4] == "0" && rows.Length != 5) return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        if (job == null || !job.IsUnmanagedType || IsOpen(job) || jobs.Contains(job) ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly)) return false;
        var groups = new List<string>();
        var counts = new List<uint>();
        for (var index = 5; index < rows.Length; ++index) {
            var parts = rows[index].Split('\t');
            if (parts.Length != 3 || !Number(parts[2], out var count) || count == 0) return false;
            var key = parts[0] + "\t" + parts[1];
            if (groups.Contains(key)) return false;
            groups.Add(key);
            counts.Add(count);
        }
        jobs.Add(job);
        plans.Add((rows[1], job, null, groups.ToArray(), counts.ToArray(), maximum, loops, rows[4] == "1"));
        ++fallbackCount;
        return true;
    }

    private static bool Number(string text, out uint number) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && text == number.ToString(CultureInfo.InvariantCulture);

    internal bool Append(StringBuilder source, IReadOnlyList<INamedTypeSymbol> registrations, out string error) {
        var keys = new HashSet<string>(registrations.Select(type => type.ContainingAssembly.Identity + "\t" + type.GetDocumentationCommentId()), StringComparer.Ordinal);
        foreach (var plan in plans) {
            foreach (var group in plan.Groups) {
                if (keys.Contains(group)) continue;
                error = "Unregistered entity group in source initializer for " + plan.Identity + ": " + group;
                return false;
            }
        }
        source.Append("\nnamespace ME.BECS.SourceGenerated { internal static unsafe class JobEntityInputCalls {\n");
        foreach (var plan in plans) {
            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\npublic static void Initialize_")
                .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.Identity)).Append("() {\n");
            if (plan.Initializer == null) {
                var target = "global::ME.BECS.JobStaticInfo<" + plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">";
                source.Append(target).Append(".entitiesMaxCount = ").Append(plan.Maximum.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
                    .Append(target).Append(".loopCount = ").Append(plan.Loops.ToString(CultureInfo.InvariantCulture)).Append("u;\n")
                    .Append(target).Append(".inlineCount = ").Append(plan.Allocate ?
                        "global::ME.BECS.Cuts._makeArray<uint>(EntityInputs.GroupCount, global::Unity.Collections.Allocator.Domain)" : "default").Append(";\n");
                for (var index = 0; index < plan.Groups.Length; ++index)
                    source.Append(target).Append(".inlineCount[EntityInputs.Id_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(plan.Groups[index]))
                        .Append("] = ").Append(plan.Counts[index].ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                source.Append("}\n");
                continue;
            }
            source.Append(plan.Initializer.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(".Apply<")
                .Append(plan.Job.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">(EntityInputs.GroupCount");
            foreach (var group in plan.Groups)
                source.Append(", EntityInputs.Id_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(group));
            source.Append(");\n}\n");
        }
        source.Append("} }\n");
        error = "";
        return true;
    }

    private static bool IsOpen(INamedTypeSymbol type) => type.IsUnboundGenericType ||
        type.TypeArguments.Any(argument => argument is ITypeParameterSymbol || argument is INamedTypeSymbol named && IsOpen(named)) ||
        type.ContainingType != null && IsOpen(type.ContainingType);
}
