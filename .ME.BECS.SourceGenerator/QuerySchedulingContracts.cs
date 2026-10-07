using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Only a submission-discovery contract. Query creation allocates memory, filters
// read component presence and WaitForAllJobs completes a fence. None is an ECS
// leaf or a substitute for the separate access/count/synchronization analyses.
internal static class QuerySchedulingContracts {
    internal const string Schema = "query-scheduling-schema=1";

    internal static bool DoesNotSchedule(IMethodSymbol method, Compilation compilation, bool modeOnly) {
        method = method.ReducedFrom ?? method;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly,
                compilation.GetTypeByMetadataName("ME.BECS.Ent")?.ContainingAssembly) ||
            !SymbolEqualityComparer.Default.Equals(method.ReturnType, compilation.GetTypeByMetadataName("ME.BECS.QueryBuilder"))) return false;
        // The mode analyzer has already checked the exact instance builder API,
        // including filter contracts. Dispose(handle), Schedule and ForEach are
        // deliberately not in that set: they can submit internal or user jobs.
        if (modeOnly) return true;
        if (!method.IsStatic || method.Name != "Query") return false;
        // Do not treat arbitrary future overloads (e.g. with a callback) as known
        // factories just because their owner/name/return type happens to match.
        return method.OriginalDefinition.GetDocumentationCommentId() is
            "M:ME.BECS.API.Query(ME.BECS.World@,Unity.Jobs.JobHandle,System.Boolean)" or
            "M:ME.BECS.API.Query(ME.BECS.SystemContext@,System.Boolean)" or
            "M:ME.BECS.API.Query(ME.BECS.SystemContext@,Unity.Jobs.JobHandle,System.Boolean)" or
            "M:ME.BECS.API.Query(ME.BECS.QueryContext@,Unity.Jobs.JobHandle,System.Boolean)" or
            "M:ME.BECS.APIExt.Query(ME.BECS.SystemContext@,System.Boolean)" or
            "M:ME.BECS.APIExt.Query(ME.BECS.SystemContext@,Unity.Jobs.JobHandle,System.Boolean)" or
            "M:ME.BECS.APIExt.Query``1(``0,ME.BECS.SystemContext@,System.Boolean)" or
            "M:ME.BECS.APIExt.Query``1(``0,ME.BECS.SystemContext@,Unity.Jobs.JobHandle,System.Boolean)";
    }

    internal static bool Call(string[] operation, string[] producerFlags, ISet<string> gaps) {
        if (!MethodSummaryContracts.Has(operation, "query-no-schedule")) return false;
        if (operation[0] != "call" || producerFlags.Count(flag => flag == Schema) != 1 ||
            operation.Count(token => token == "!query-no-schedule") != 1)
            gaps.Add("UnsupportedQuerySchedulingContract: " + operation[3]);
        return true; // Malformed/old proof stays incomplete, never an empty complete set.
    }
}
