using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Semantic call graphs for non-allocating lookup/removal in Collections
// aea9d3bd5e19. Callbacks are ordinary analyzed bodies, NOT container leaves.
internal static class NativeHashMapMethodSummaries {
    internal static void Add(Compilation compilation, Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods) {
        var equatable = compilation.GetTypeByMetadataName("System.IEquatable`1");
        if (equatable?.TypeKind != TypeKind.Interface) return;
        foreach (var name in new[] { "Unity.Collections.NativeHashMap`2", "Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2" }) {
            var owner = compilation.GetTypeByMetadataName(name);
            if (owner?.ContainingAssembly.Name != "Unity.Collections" || owner.TypeKind != TypeKind.Struct || owner.Arity != 2) continue;
            var key = owner.TypeParameters[0];
            var value = owner.TypeParameters[1];
            var equals = equatable.Construct(key).GetMembers("Equals").OfType<IMethodSymbol>().SingleOrDefault(method =>
                !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, key) && method.ReturnType.SpecialType == SpecialType.System_Boolean);
            if (equals == null) continue;
            foreach (var type in new[] { owner }.Concat(owner.GetTypeMembers("ReadOnly")))
                foreach (var method in MethodSummaryInterfaceMap.Members(type)) {
                    if (method.DeclaringSyntaxReferences.Length != 0 || method.DeclaredAccessibility != Accessibility.Public ||
                        method.IsStatic || method.IsAbstract || method.IsVirtual || method.IsVararg || method.Arity != 0 ||
                        method.ReturnsByRef || method.ReturnsByRefReadonly) continue;
                    var args = method.Parameters;
                    if (args.Length == 0 || args[0].RefKind != RefKind.None || !SymbolEqualityComparer.Default.Equals(args[0].Type, key)) continue;
                    var lookup = method.MethodKind == MethodKind.Ordinary && method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                        (args.Length == 1 && method.Name == "ContainsKey" || args.Length == 2 && method.Name == "TryGetValue" &&
                         args[1].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(args[1].Type, value));
                    var remove = method.MethodKind == MethodKind.Ordinary && method.Name == "Remove" && args.Length == 1 &&
                        method.ReturnType.SpecialType == SpecialType.System_Boolean && SymbolEqualityComparer.Default.Equals(type, owner);
                    var indexer = method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Item" && args.Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(method.ReturnType, value);
                    if (!lookup && !remove && !indexer) continue;
                    var id = MethodSummaryIdentity.Get(method);
                    if (id == null) continue;
                    var identity = (method.ContainingAssembly.Identity.ToString(), id);
                    if (methods.ContainsKey(identity)) continue; // Real producer summaries take precedence.
                    var modes = new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default);
                    var hash = NativeHashKeyCall.Operation(key);
                    var compare = MethodSummaryContracts.Operation("call", 0, equals, compilation, modes, key)!.Split('\t');
                    var memory = MethodSummaryContracts.Operation("call", 0, method, compilation, modes)!.Split('\t')
                        .Concat(new[] { "!ecs-leaf", "!native-memory-access", "!native-component-read=" + Type(key) }).ToArray();
                    if (indexer || args.Length == 2) memory = memory.Concat(new[] { "!native-component-read=" + Type(value) }).ToArray();
                    // UnsafeHashMap.ReadOnly's indexer returns default on failure;
                    // the other getters may format the key under package checks.
                    var format = indexer && (name == "Unity.Collections.NativeHashMap`2" || SymbolEqualityComparer.Default.Equals(type, owner));
                    var formatting = new[] { "call", "0", key.ContainingAssembly.Identity.ToString(), "M:__ImplicitFormatting", Type(key), "!implicit-formatting=1:format" };
                    var summary = new MethodSummaryGraph.Summary {
                        Id = id, Environment = MethodSummaryType.Environment(method),
                        Flags = new[] { MethodSummaryContracts.SafetySchema, MethodSummaryContracts.SchedulingSchema, MethodSummaryContracts.SystemAccessSchema,
                            DestroyDispatchContracts.Schema, ImplicitFormattingContracts.Schema, GenericConstructionContracts.Schema,
                            SystemQueryFilterContracts.Schema, QueryScheduleModeFlow.Schema, MethodSummaryControlFlow.EffectUnionSchema,
                            "weight-schema=1", "native-hash-map-projection=1" }
                            .Concat(Flow(compilation, method, hash, compare, memory, formatting, format).Split(',')).ToArray(),
                    };
                    summary.Operations.Add(hash);
                    var repeating = (string[])compare.Clone(); repeating[1] = "1";
                    summary.Operations.Add(repeating); // Collision traversal can call Equals many times.
                    summary.Operations.Add(memory);
                    if (format) summary.Operations.Add(formatting);
                    methods.Add(identity, summary);
                }
        }
    }

    private static string Type(ITypeSymbol type) => MethodSummaryType.From(type).Encode();
    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private static string Flow(Compilation compilation, IMethodSymbol method, string[] hash, string[] compare, string[] memory, string[] formatting, bool format) {
        var key = method.Parameters[0].Type;
        var returnsValue = method.MethodKind == MethodKind.PropertyGet;
        string Role(ITypeSymbol type) => type is ITypeParameterSymbol ? "T" : "U";
        var body = new StringBuilder("v3\nM\t").Append(Role(method.ReturnType)).Append("\tvalue\t").Append(Type(method.ReturnType))
            .Append("\nI\tthis\tU\tref\t").Append(Type(method.ContainingType)).Append('\n');
        foreach (var parameter in method.Parameters)
            body.Append("P\tp").Append(parameter.Ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Role(parameter.Type)).Append('\t')
                .Append(parameter.RefKind == RefKind.Out ? "out" : "value").Append('\t').Append(Type(parameter.Type)).Append('\n');
        body.Append("V\tv0\tT\t").Append(Type(key)).Append('\n');
        if (returnsValue) body.Append("V\tv1\tT\t").Append(Type(method.ReturnType)).Append('\n');
        var calls = new[] { hash, compare, memory, formatting };
        for (var index = 0; index < calls.Length; ++index)
            body.Append("S\ts").Append(index.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Encode(string.Join("\t", calls[index]))).Append('\n');
        var nativeAccess = "A\t" + Type(compilation.GetSpecialType(SpecialType.System_Byte)) + "\n";
        // Native storage snapshots do not invent handle ownership. The value is
        // untracked unless substitution makes it tracked; O then keeps an explicit gap.
        body.Append("B\t0\tEntry\tNone\t1:Regular\t-\n")
            .Append("B\t1\tBlock\tWhenTrue\t8:Regular\t2:Regular\n").Append(nativeAccess)
            .Append("B\t2\tBlock\tNone\t3:Regular\t-\nC\t-\ts0\tvalue\tp0\n")
            .Append("B\t3\tBlock\tWhenTrue\t8:Regular\t4:Regular\n").Append(nativeAccess)
            .Append("B\t4\tBlock\tWhenTrue\t5:Regular\t6:Regular\n").Append(nativeAccess)
            .Append("O\t").Append(Type(key)).Append("\nZ\tv0\nC\t-\ts1\tvalue\tv0\t0:value:p0\n")
            .Append("B\t5\tBlock\tWhenTrue\t8:Regular\t4:Regular\n").Append(nativeAccess)
            .Append("B\t6\tBlock\tNone\t9:Return\t-\n");
        AppendMemory(); // Found value or metadata removal, after callback effects.
        if (format) {
            // The package's checks can omit the formatting call entirely. The
            // checked path formats and throws; it never returns a missing value.
            body.Append("B\t8\tBlock\tWhenTrue\t7:Regular\t10:Regular\n")
                .Append("B\t7\tBlock\tNone\t-:Throw\t-\nT\ts3\tp0\n")
                .Append("B\t10\tBlock\tNone\t9:Return\t-\n");
        } else body.Append("B\t8\tBlock\tNone\t9:Return\t-\n");
        AppendMemory(); // Missing/default out values also remain opaque if handle-typed.
        body.Append("B\t9\tExit\tNone\t-\t-\n");
        return MethodSynchronizationFlow.Schema + "," + MethodSynchronizationFlow.ExceptionSchema + "," +
            MethodSynchronizationFlow.FlagPrefix + Encode(body.ToString());

        void AppendMemory() {
            body.Append("C\t").Append(returnsValue ? "v1" : "-").Append("\ts2\tvalue\t-\t0:value:p0");
            if (method.Parameters.Length == 2) body.Append("\t1:out:p1");
            body.Append("\nR\t").Append(returnsValue ? "v1" : "-").Append('\n');
        }
    }
}
