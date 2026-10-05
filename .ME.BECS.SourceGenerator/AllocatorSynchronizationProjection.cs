using System;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Ordered transfer programs for the exact metadata signatures accepted by
// AllocatorMethodSummaries. Scalar/native storage is opaque; user callbacks are
// ordinary calls, never completion primitives. Both package-check configurations
// are modeled so a consuming asmdef's defines cannot certify the package's build.
internal static class AllocatorSynchronizationProjection {
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static string Type(ITypeSymbol type) => MethodSummaryType.From(type).Encode();

    private static StringBuilder Header(IMethodSymbol method, params string[][] calls) {
        var body = new StringBuilder("v3\nM\tU\tvalue\t").Append(Type(method.ReturnType)).Append('\n');
        foreach (var parameter in method.Parameters)
            body.Append("P\tp").Append(parameter.Ordinal.ToString(CultureInfo.InvariantCulture))
                .Append(parameter.Type is ITypeParameterSymbol ? "\tT\t" : "\tU\t")
                .Append(parameter.RefKind == RefKind.Ref ? "ref" : "value").Append('\t').Append(Type(parameter.Type)).Append('\n');
        for (var index = 0; index < calls.Length; ++index)
            body.Append("S\ts").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Encode(string.Join("\t", calls[index]))).Append('\n');
        return body;
    }

    // Between these callback events the audited wrappers only manipulate untracked
    // scalar/native storage. Before/after C/A exception states cover their effects;
    // user callback exceptional exits are propagated by the ordinary interpreter.
    private static string Finish(StringBuilder body) => MethodSynchronizationFlow.Schema + "," + MethodSynchronizationFlow.ExceptionSchema + "," +
        MethodSynchronizationFlow.FlagPrefix + Encode(body.ToString());

    internal static string Allocate(IMethodSymbol method, string[] getter, string[] tryCall) {
        var allocator = Type(method.TypeParameters[0]);
        var body = Header(method, getter, tryCall);
        body.Append("B\t0\tEntry\tNone\t1:Regular\t-\n")
            // CheckValid(t.Handle) is omitted without checks, including its argument.
            .Append("B\t1\tBlock\tWhenTrue\t3:Regular\t2:Regular\n")
            .Append("B\t2\tBlock\tNone\t3:Regular\t-\n")
            .Append("A\t").Append(allocator).Append("\nC\t-\ts0\tref\tp0\n")
            .Append("B\t3\tBlock\tNone\t4:Regular\t-\n")
            .Append("A\t").Append(allocator).Append("\nC\t-\ts0\tref\tp0\n")
            .Append("A\t").Append(allocator).Append("\nC\t-\ts1\tref\tp0\t0:ref:-\n")
            // Try receives a fresh local Block; no caller-owned handle is copied
            // into it. The pointer result has no synchronization-token identity.
            .Append("B\t4\tExit\tNone\t-\t-\n");
        return Finish(body);
    }

    internal static string ForwardAllocate(IMethodSymbol method, string[] target, bool typed) {
        var body = Header(method, target);
        body.Append("B\t0\tEntry\tNone\t1:Regular\t-\n")
            .Append("B\t1\tBlock\tNone\t2:Regular\t-\n")
            .Append("C\t-\ts0\tvalue\t-\t0:ref:p0")
            .Append(typed ? "\t1:value:-\t2:value:-\t3:value:p1\n" : "\t1:value:p1\t2:value:p2\t3:value:p3\n")
            .Append("B\t2\tExit\tNone\t-\t-\n");
        return Finish(body);
    }

    internal static string Free(IMethodSymbol method, string[] target) {
        var body = Header(method, target);
        body.Append("B\t0\tEntry\tNone\t1:Regular\t-\n")
            // A null pointer skips freeing. Without scalar value tracking both
            // branches are required; null in one call cannot hide other dispatch.
            .Append("B\t1\tBlock\tWhenTrue\t3:Regular\t2:Regular\n")
            .Append("B\t2\tBlock\tNone\t3:Regular\t-\n")
            .Append("C\t-\ts0\tref\tp0\t0:ref:-\n")
            .Append("B\t3\tExit\tNone\t-\t-\n");
        return Finish(body);
    }
}
