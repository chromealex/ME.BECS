using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Explicit IL snapshots are authoritative. Older source/legacy plans retain
// their previous catalog selection rules for input upgrade compatibility.
internal sealed class DebugJobInputPlan {
    internal INamedTypeSymbol Job = null!;
    internal string JobIdentity = "";
    internal string ContractIdentity = "";
    internal INamedTypeSymbol Contract = null!;
    internal INamedTypeSymbol? WorkInterface;
    internal bool HasTypedArguments;
    internal bool SourceSafety;
    internal string SafetyOrigin = "legacy";
    internal readonly List<INamedTypeSymbol> Components = new();
    internal readonly List<INamedTypeSymbol> Aspects = new();
    internal readonly List<(INamedTypeSymbol Type, string Mode)> Safety = new();

    internal static bool TryRead(string payload, InputManifestTypes resolver, Compilation compilation,
        out DebugJobInputPlan plan, out string error) {
        plan = new DebugJobInputPlan();
        error = "Invalid debug job plan: expected v1/v2 header and a 0/1 typed-arguments flag";
        var rows = payload.Split('\n');
        if (rows.Length < 5 || rows[0] is not ("v1" or "v2") || (rows[4] != "0" && rows[4] != "1")) return false;
        var job = resolver.ResolveDefinition(rows[1], out var jobGap);
        if (job == null) return Invalid(rows[1], "job type cannot be resolved (" + jobGap + ")", out error);
        var contract = resolver.ResolveDefinition(rows[2], out var contractGap);
        if (contract == null) return Invalid(rows[1], "contract cannot be resolved: " + rows[2] + " (" + contractGap + ")", out error);
        if (HasOpenArguments(job)) return Invalid(rows[1], "job contains open generic arguments", out error);
        if (!job.IsUnmanagedType) return Invalid(rows[1], "job is not unmanaged", out error);
        if (!compilation.IsSymbolAccessibleWithin(job, compilation.Assembly))
            return Invalid(rows[1], "job is inaccessible to " + compilation.AssemblyName, out error);
        if (!job.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, contract)))
            return Invalid(rows[1], "job no longer implements contract " + rows[2], out error);
        plan.Job = job;
        plan.JobIdentity = rows[1];
        plan.ContractIdentity = rows[2];
        plan.Contract = contract;
        if (rows[3].Length != 0) {
            var workInterface = resolver.ResolveDefinition(rows[3], out var workGap);
            if (workInterface == null) return Invalid(rows[1], "work interface cannot be resolved: " + rows[3] + " (" + workGap + ")", out error);
            if (HasOpenArguments(workInterface) ||
                !workInterface.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, contract)) ||
                !job.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, workInterface)))
                return Invalid(rows[1], "work interface does not match the closed job/contract: " + rows[3], out error);
            plan.WorkInterface = workInterface;
        }
        plan.HasTypedArguments = rows[4] == "1";
        // Older exporters used TNull as the disabled aspect-role filter. Since
        // TNull is a real IComponent, a component-only job<TNull> was exported as
        // both C and A and incorrectly marked untyped. Recover only that exact
        // shape so stale inputs cannot block loading the corrected Editor code.
        var nullType = compilation.GetTypeByMetadataName("ME.BECS.TNull");
        var oldNullRole = !plan.HasTypedArguments && plan.WorkInterface != null && nullType != null &&
            (SymbolEqualityComparer.Default.Equals(contract, compilation.GetTypeByMetadataName("ME.BECS.IJobForComponentsBase")) ||
             SymbolEqualityComparer.Default.Equals(contract, compilation.GetTypeByMetadataName("ME.BECS.IJobParallelForComponentsBase"))) &&
            plan.WorkInterface.TypeArguments.Contains(nullType, SymbolEqualityComparer.Default);
        var oldNullAspectCount = 0;
        var seenSafety = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        string? selection = null;
        for (var index = 5; index < rows.Length; ++index) {
            var fields = rows[index].Split('\t');
            var safety = fields[0] == "S";
            if (rows[0] == "v2" && safety && fields.Length == 2) {
                if (selection != null || fields[1] is not ("source" or "legacy" or "il"))
                    return Invalid(rows[1], "duplicate or unknown safety selection at payload row " + (index + 1), out error);
                selection = fields[1];
                continue;
            }
            if (safety ? fields.Length != 3 : fields.Length != 2 || (fields[0] != "C" && fields[0] != "A"))
                return Invalid(rows[1], "malformed dependency record at payload row " + (index + 1), out error);
            var identity = fields[safety ? 2 : 1];
            var type = resolver.ResolveDefinition(identity, out var typeGap);
            if (type == null) return Invalid(rows[1], "dependency cannot be resolved: " + identity + " (" + typeGap + ")", out error);
            if (HasOpenArguments(type)) return Invalid(rows[1], "dependency contains open generic arguments: " + identity, out error);
            if (!type.IsUnmanagedType) return Invalid(rows[1], "dependency is not unmanaged: " + identity, out error);
            if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                return Invalid(rows[1], "dependency is inaccessible to " + compilation.AssemblyName + ": " + identity, out error);
            if (oldNullRole && fields[0] == "A" && SymbolEqualityComparer.Default.Equals(type, nullType)) {
                ++oldNullAspectCount;
                continue;
            }
            var expectedContract = compilation.GetTypeByMetadataName(fields[0] == "A" ? "ME.BECS.IAspect" : "ME.BECS.IComponentBase");
            if (expectedContract == null || !type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, expectedContract)))
                return Invalid(rows[1], "dependency does not implement " + (fields[0] == "A" ? "IAspect" : "IComponentBase") + ": " + identity, out error);
            if (safety) {
                if (fields[1] != "ReadOnly" && fields[1] != "WriteOnly" && fields[1] != "ReadWrite")
                    return Invalid(rows[1], "unknown safety mode '" + fields[1] + "' for " + identity, out error);
                if (!seenSafety.Add(type)) return Invalid(rows[1], "duplicate safety dependency: " + identity, out error);
                plan.Safety.Add((type, fields[1]));
            } else if (fields[0] == "C") plan.Components.Add(type);
            else plan.Aspects.Add(type);
        }
        if (oldNullRole && oldNullAspectCount == plan.WorkInterface!.TypeArguments.Count(type => SymbolEqualityComparer.Default.Equals(type, nullType)))
            plan.HasTypedArguments = true;
        if (plan.HasTypedArguments != (plan.WorkInterface != null))
            return Invalid(rows[1], "typed-arguments flag does not match the work interface", out error);
        if (plan.HasTypedArguments && (plan.WorkInterface == null ||
            plan.Components.Count + plan.Aspects.Count != plan.WorkInterface.TypeArguments.Length))
            return Invalid(rows[1], "component/aspect count does not match work-interface arguments", out error);
        if (!plan.HasTypedArguments && (plan.Components.Count != 0 || plan.Aspects.Count != 0))
            return Invalid(rows[1], "component/aspect arguments supplied without a typed work interface", out error);
        if (plan.HasTypedArguments) {
            var componentContract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
            var aspectContract = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
            var componentArguments = plan.WorkInterface!.TypeArguments.OfType<INamedTypeSymbol>()
                .Where(type => type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, componentContract)));
            var aspectArguments = plan.WorkInterface.TypeArguments.OfType<INamedTypeSymbol>()
                .Where(type => type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, aspectContract)));
            if (!componentArguments.SequenceEqual(plan.Components, SymbolEqualityComparer.Default) ||
                !aspectArguments.SequenceEqual(plan.Aspects, SymbolEqualityComparer.Default))
                return Invalid(rows[1], "component/aspect types or order do not match work-interface arguments", out error);
        }
        if (rows[0] == "v2" && selection == null) return Invalid(rows[1], "v2 plan is missing the safety selection", out error);
        if (rows[0] == "v2" && selection == "source" && plan.Safety.Count != 0)
            return Invalid(rows[1], "source selection must not contain an IL dependency snapshot", out error);
        if (selection == "il") {
            plan.SafetyOrigin = "il";
            error = "";
            return true;
        }
        var status = resolver.JobSafety.Read(job, compilation, out var dependencies, out error);
        if (status == CompilerJobCatalogs.Status.Invalid) return Invalid(rows[1], error, out error);
        if (status == CompilerJobCatalogs.Status.Complete) {
            plan.SourceSafety = true;
            plan.SafetyOrigin = "source";
            plan.Safety.Clear();
            plan.Safety.AddRange(dependencies);
        } else if (selection == "source") {
            // Never silently use an old/empty list.
            return Invalid(rows[1], "source safety selection is " + status + ": " + error, out error);
        }
        error = "";
        return true;
    }

    private static bool Invalid(string job, string reason, out string error) {
        error = "Invalid debug job plan for " + job + ": " + reason;
        return false;
    }

    private static bool HasOpenArguments(ITypeSymbol type) => type switch {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => HasOpenArguments(array.ElementType),
        IPointerTypeSymbol pointer => HasOpenArguments(pointer.PointedAtType),
        INamedTypeSymbol named => named.IsUnboundGenericType || named.TypeArguments.Any(HasOpenArguments) ||
            (named.ContainingType != null && HasOpenArguments(named.ContainingType)),
        _ => false,
    };
}
