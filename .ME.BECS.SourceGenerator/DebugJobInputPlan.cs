using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Typed transport only. Wrapper emission switches over after plan coverage is checked.
internal sealed class DebugJobInputPlan {
    internal INamedTypeSymbol Job = null!;
    internal string JobIdentity = "";
    internal string ContractIdentity = "";
    internal INamedTypeSymbol Contract = null!;
    internal INamedTypeSymbol? WorkInterface;
    internal bool HasTypedArguments;
    internal readonly List<INamedTypeSymbol> Components = new();
    internal readonly List<INamedTypeSymbol> Aspects = new();
    internal readonly List<(INamedTypeSymbol Type, string Mode)> Safety = new();

    internal static bool TryRead(string payload, InputManifestTypes resolver, Compilation compilation,
        out DebugJobInputPlan plan, out string error) {
        plan = new DebugJobInputPlan();
        error = "Invalid debug job plan";
        var rows = payload.Split('\n');
        if (rows.Length < 5 || rows[0] != "v1" || (rows[4] != "0" && rows[4] != "1")) return false;
        var job = resolver.ResolveDefinition(rows[1], out _);
        var contract = resolver.ResolveDefinition(rows[2], out _);
        if (job == null || contract == null || !job.IsUnmanagedType || HasOpenArguments(job) ||
            !compilation.IsSymbolAccessibleWithin(job, compilation.Assembly) ||
            !job.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, contract))) return false;
        plan.Job = job;
        plan.JobIdentity = rows[1];
        plan.ContractIdentity = rows[2];
        plan.Contract = contract;
        if (rows[3].Length != 0) {
            var workInterface = resolver.ResolveDefinition(rows[3], out _);
            if (workInterface == null || HasOpenArguments(workInterface) ||
                !workInterface.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, contract)) ||
                !job.AllInterfaces.Any(type => SymbolEqualityComparer.Default.Equals(type, workInterface))) return false;
            plan.WorkInterface = workInterface;
        }
        plan.HasTypedArguments = rows[4] == "1";
        if (plan.HasTypedArguments != (plan.WorkInterface != null)) return false;
        var seenSafety = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        for (var index = 5; index < rows.Length; ++index) {
            var fields = rows[index].Split('\t');
            var safety = fields[0] == "S";
            if (safety ? fields.Length != 3 : fields.Length != 2 || (fields[0] != "C" && fields[0] != "A")) return false;
            var type = resolver.ResolveDefinition(fields[safety ? 2 : 1], out _);
            if (type == null || !type.IsUnmanagedType || HasOpenArguments(type) ||
                !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) return false;
            var expectedContract = compilation.GetTypeByMetadataName(fields[0] == "A" ? "ME.BECS.IAspect" : "ME.BECS.IComponentBase");
            if (expectedContract == null || !type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, expectedContract))) return false;
            if (safety) {
                if ((fields[1] != "ReadOnly" && fields[1] != "WriteOnly" && fields[1] != "ReadWrite") || !seenSafety.Add(type)) return false;
                plan.Safety.Add((type, fields[1]));
            } else if (fields[0] == "C") plan.Components.Add(type);
            else plan.Aspects.Add(type);
        }
        if (plan.HasTypedArguments && (plan.WorkInterface == null ||
            plan.Components.Count + plan.Aspects.Count != plan.WorkInterface.TypeArguments.Length)) return false;
        if (!plan.HasTypedArguments && (plan.Components.Count != 0 || plan.Aspects.Count != 0)) return false;
        if (plan.HasTypedArguments) {
            var componentContract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
            var aspectContract = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
            var componentArguments = plan.WorkInterface!.TypeArguments.OfType<INamedTypeSymbol>()
                .Where(type => type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, componentContract)));
            var aspectArguments = plan.WorkInterface.TypeArguments.OfType<INamedTypeSymbol>()
                .Where(type => type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, aspectContract)));
            if (!componentArguments.SequenceEqual(plan.Components, SymbolEqualityComparer.Default) ||
                !aspectArguments.SequenceEqual(plan.Aspects, SymbolEqualityComparer.Default)) return false;
        }
        error = "";
        return true;
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
