using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Job control progresses work that was submitted at a scheduling site. It is NOT
// an ECS leaf: jobs/counts/weights must not erase arbitrary deferred work. System
// catalogs describe direct lifecycle effects and jobs submitted by that lifecycle;
// Execute effects belong to each job's separate safety summary, including when
// Unity executes the job on the waiting thread. Handle ownership is proved by the
// synchronization interpreter, not by the presence of this effect-scope contract.
internal static class JobControlContracts {
    internal const string Schema = "job-control-schema=1";

    internal static string? Classify(IMethodSymbol method, Compilation compilation) {
        if (method.DeclaringSyntaxReferences.Length != 0 || method.Arity != 0 || method.IsAbstract || method.IsVirtual ||
            method.DeclaredAccessibility != Accessibility.Public || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.ContainingAssembly.Name != "UnityEngine.CoreModule") return null;
        var handle = compilation.GetTypeByMetadataName("Unity.Jobs.JobHandle");
        if (handle == null || handle.TypeKind != TypeKind.Struct || handle.Arity != 0 ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, handle)) return null;
        if (method.Parameters.Length == 0) {
            if (!method.IsStatic && method.MethodKind == MethodKind.PropertyGet && method.Name == "get_IsCompleted" &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean) return "poll";
            if (method.MethodKind != MethodKind.Ordinary || !method.ReturnsVoid) return null;
            if (!method.IsStatic && method.Name == "Complete") return "complete";
            if (method.IsStatic && method.Name == "ScheduleBatchedJobs") return "flush";
            return null;
        }
        if (!method.IsStatic || method.MethodKind != MethodKind.Ordinary || method.Name != "CompleteAll" || !method.ReturnsVoid) return null;
        if (method.Parameters.Length is 2 or 3 && method.Parameters.All(parameter => parameter.RefKind == RefKind.Ref &&
            SymbolEqualityComparer.Default.Equals(parameter.Type, handle))) return "complete-all";
        if (method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
            method.Parameters[0].Type is INamedTypeSymbol container && container.TypeKind == TypeKind.Struct &&
            container.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(container.TypeArguments[0], handle) &&
            SymbolEqualityComparer.Default.Equals(container.ContainingAssembly, method.ContainingAssembly) &&
            SymbolEqualityComparer.Default.Equals(container.OriginalDefinition, compilation.GetTypeByMetadataName("Unity.Collections.NativeArray`1")))
            return "complete-array";
        return null;
    }

    internal static bool SystemCall(string[] operation, string[] producerFlags, ISet<string> gaps) {
        if (operation[0] != "call" || MethodSummaryContracts.Value(operation, "job-control") is not string control) return false;
        if (producerFlags.Count(flag => flag == Schema) != 1 ||
            operation.Count(token => token.StartsWith("!job-control=", System.StringComparison.Ordinal)) != 1 ||
            control is not ("complete" or "complete-all" or "complete-array" or "poll" or "flush"))
            gaps.Add("UnsupportedJobControlContract: " + operation[3]);
        return true; // A malformed contract leaves a gap; never publish it as complete.
    }
}
