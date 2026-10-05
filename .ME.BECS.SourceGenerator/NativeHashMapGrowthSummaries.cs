using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Collections aea9d3bd5e19, HashMapHelper.ResizeExact/TryAdd/Find. Allocator
// provenance permits a projection, NOT a leaf: resizing reinserts stored keys
// and invokes both callbacks in loops. Real producer bodies take precedence.
internal static class NativeHashMapGrowthSummaries {
    private const string Marker = "native-map-local-allocator";
    private const string Version = "builtin-v1";
    private const string Suffix = "~NativeMapBuiltinV1";
    private const string Schema = "native-map-growth-projection=1";

    private static string? Kind(IMethodSymbol method, Compilation compilation) {
        if (!UnityContainerContracts.IsMap(method.ContainingType, compilation) || method.DeclaringSyntaxReferences.Length != 0 ||
            method.DeclaredAccessibility != Accessibility.Public || method.IsStatic || method.IsAbstract || method.IsVirtual ||
            method.IsVararg || method.Arity != 0 || method.ReturnsByRef || method.ReturnsByRefReadonly) return null;
        var args = method.Parameters;
        var types = method.ContainingType.TypeArguments;
        bool Value(int index, ITypeSymbol type) => args[index].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(args[index].Type, type);
        if (method.MethodKind == MethodKind.PropertySet && method.ReturnsVoid) {
            if (method.Name == "set_Capacity" && args.Length == 1 && args[0].RefKind == RefKind.None && args[0].Type.SpecialType == SpecialType.System_Int32) return "resize";
            if (method.Name == "set_Item" && args.Length == 2 && Value(0, types[0]) && Value(1, types[1])) return "set";
        }
        if (method.MethodKind != MethodKind.Ordinary) return null;
        if (method.ReturnsVoid && args.Length == 0 && method.Name is "TrimExcess" or "Dispose") return method.Name == "Dispose" ? "dispose" : "resize";
        if (args.Length == 2 && Value(0, types[0]) && Value(1, types[1])) {
            if (method.Name == "TryAdd" && method.ReturnType.SpecialType == SpecialType.System_Boolean) return "insert";
            if (method.Name == "Add" && method.ReturnsVoid) return "add";
        }
        return null;
    }

    internal static string Contract(IMethodSymbol method, Compilation compilation) =>
        Kind(method, compilation) == null ? "" : "\t!" + Marker + "=" + Version;

    internal static bool Preserves(IMethodSymbol method, Compilation compilation) {
        if (Kind(method, compilation) != null) return true;
        // Non-growing operations use the existing callback projection. They do
        // not expose the header/allocator to the caller or to the key callbacks.
        if (!UnityContainerContracts.IsMap(method.ContainingType, compilation) || method.DeclaringSyntaxReferences.Length != 0 ||
            method.DeclaredAccessibility != Accessibility.Public || method.IsStatic || method.IsAbstract || method.IsVirtual ||
            method.IsVararg || method.Arity != 0 || method.ReturnsByRef || method.ReturnsByRefReadonly) return false;
        var args = method.Parameters;
        var types = method.ContainingType.TypeArguments;
        if (args.Length == 0 || args[0].RefKind != RefKind.None || !SymbolEqualityComparer.Default.Equals(args[0].Type, types[0])) return false;
        return method.MethodKind == MethodKind.PropertyGet && method.Name == "get_Item" && args.Length == 1 && SymbolEqualityComparer.Default.Equals(method.ReturnType, types[1]) ||
            method.MethodKind == MethodKind.Ordinary && method.ReturnType.SpecialType == SpecialType.System_Boolean &&
            (args.Length == 1 && method.Name is "ContainsKey" or "Remove" || method.Name == "TryGetValue" && args.Length == 2 &&
             args[1].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(args[1].Type, types[1]));
    }

    internal static string[] Resolve(string[] row, Compilation compilation, IReadOnlyDictionary<string, MethodSummaryType> bindings,
        ISet<string> gaps, IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts) {
        var markers = row.Where(token => token.StartsWith("!" + Marker + "=", StringComparison.Ordinal)).ToArray();
        if (markers.Length == 0 && !row[3].EndsWith(Suffix, StringComparison.Ordinal)) return row;
        var constrained = row.Count(token => token.StartsWith("!constrained=", StringComparison.Ordinal));
        var values = row.Where(token => token.StartsWith("!schedule-value-", StringComparison.Ordinal)).ToArray();
        if (markers.Length != 1 || markers[0] != "!" + Marker + "=" + Version || constrained > 1 || row.Length != 6 + constrained + values.Length || row[0] != "call" ||
            !int.TryParse(row[1], NumberStyles.None, CultureInfo.InvariantCulture, out _) || !MethodSummaryType.TryDecode(row[4], out var expression)) {
            gaps.Add("MalformedNativeMapAllocatorContract: " + row[3]); return row;
        }
        var owner = MethodSummaryTypeResolver.Resolve(expression!.Substitute(bindings), compilation) as INamedTypeSymbol;
        var method = owner == null ? null : MethodSummaryInterfaceMap.Members(owner).SingleOrDefault(candidate => MethodSummaryIdentity.Get(candidate) == row[3]);
        if (method == null || method.ContainingAssembly.Identity.ToString() != row[2] || Kind(method, compilation) == null) {
            gaps.Add("InvalidNativeMapAllocatorTarget: " + row[3]); return row;
        }
        // Query-mode analysis annotates every bool argument, including map keys
        // and values. These facts do not alter allocator provenance or callbacks.
        // Accept only well-formed facts bound to actual bool parameters; never
        // relax the strict contract to permit leaf flags or unknown annotations.
        var ordinals = new HashSet<int>();
        foreach (var value in values) {
            var pair = value.Substring("!schedule-value-".Length).Split('=');
            if (pair.Length != 2 || !int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) ||
                pair[0] != ordinal.ToString(CultureInfo.InvariantCulture) || ordinal >= method.Parameters.Length || !ordinals.Add(ordinal) ||
                method.Parameters[ordinal].Type.SpecialType != SpecialType.System_Boolean || !BooleanValue(pair[1])) {
                gaps.Add("MalformedNativeMapAllocatorContract: " + row[3]); return row;
            }
        }
        // Never bypass conflicting/real package summaries with a projection.
        if (methods.ContainsKey((row[2], row[3])) || conflicts.Contains((row[2], row[3]))) return row;
        var projected = row[3] + Suffix;
        if (!methods.TryGetValue((row[2], projected), out var summary) || !summary.Flags.Contains(Schema)) {
            gaps.Add("MissingNativeMapGrowthProjection: " + row[3]); return row;
        }
        var result = (string[])row.Clone(); result[3] = projected;
        return result;
    }

    private static bool BooleanValue(string value) {
        if (value == "?") return true;
        var terms = value.Split('|');
        return terms.SequenceEqual(terms.Distinct().OrderBy(term => term, StringComparer.Ordinal)) && terms.All(term =>
            term is "0" or "1" || term.StartsWith("p", StringComparison.Ordinal) &&
            int.TryParse(term.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal) &&
            term == "p" + ordinal.ToString(CultureInfo.InvariantCulture));
    }

    internal static void Add(Compilation compilation, Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods) {
        var equatable = compilation.GetTypeByMetadataName("System.IEquatable`1");
        if (equatable?.TypeKind != TypeKind.Interface) return;
        foreach (var name in new[] { "Unity.Collections.NativeHashMap`2", "Unity.Collections.LowLevel.Unsafe.UnsafeHashMap`2" }) {
            var owner = compilation.GetTypeByMetadataName(name);
            if (owner == null || !UnityContainerContracts.IsMap(owner, compilation)) continue;
            var key = owner.TypeParameters[0];
            var equals = equatable.Construct(key).GetMembers("Equals").OfType<IMethodSymbol>().SingleOrDefault(method =>
                !method.IsStatic && method.Arity == 0 && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, key) && method.ReturnType.SpecialType == SpecialType.System_Boolean);
            if (equals == null) continue;
            foreach (var method in MethodSummaryInterfaceMap.Members(owner)) {
                var kind = Kind(method, compilation);
                var originalId = MethodSummaryIdentity.Get(method);
                if (kind == null || originalId == null) continue;
                var identity = (method.ContainingAssembly.Identity.ToString(), originalId + Suffix);
                if (methods.ContainsKey(identity)) continue;
                var modes = new Dictionary<INamedTypeSymbol, int?>(SymbolEqualityComparer.Default);
                var hash = NativeHashKeyCall.Operation(key);
                var compare = MethodSummaryContracts.Operation("call", 0, equals, compilation, modes, key)!.Split('\t');
                var memory = MethodSummaryContracts.Operation("call", 0, method, compilation, modes)!.Split('\t')
                    .Concat(new[] { "!ecs-leaf", "!native-memory-access" }).Concat(owner.TypeArguments.Select(type => "!native-component-write=" + Type(type))).ToArray();
                var formatting = new[] { "call", "0", key.ContainingAssembly.Identity.ToString(), "M:__ImplicitFormatting", Type(key), "!implicit-formatting=1:format" };
                var summary = new MethodSummaryGraph.Summary { Id = identity.Item2, Environment = MethodSummaryType.Environment(method) };
                var flow = new Projection(compilation, method, summary, hash, compare, memory, formatting);
                if (kind == "set") flow.Find("p0", 0);
                if (kind is "set" or "insert" or "add") flow.Find("p0", 0);
                if (kind != "dispose") {
                    // Rehash performs Find + insertion hashing for each old key.
                    // Independent optional loops conservatively include early exits,
                    // collisions and callbacks throwing/scheduling in either phase.
                    flow.Find("v0", 1); flow.Hash("v0", 1);
                }
                if (kind is "set" or "insert" or "add") flow.Hash("p0", 0);
                flow.Memory();
                if (kind == "add") flow.FormatAndThrow();
                summary.Flags = new[] { MethodSummaryContracts.SafetySchema, MethodSummaryContracts.SchedulingSchema, MethodSummaryContracts.SystemAccessSchema,
                    DestroyDispatchContracts.Schema, ImplicitFormattingContracts.Schema, GenericConstructionContracts.Schema,
                    SystemQueryFilterContracts.Schema, QueryScheduleModeFlow.Schema, MethodSummaryControlFlow.EffectUnionSchema,
                    "weight-schema=1", Schema }.Concat(flow.Finish().Split(',')).ToArray();
                methods.Add(identity, summary);
            }
        }
    }

    private static string Type(ITypeSymbol type) => MethodSummaryType.From(type).Encode();
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private sealed class Projection {
        private readonly StringBuilder text;
        private readonly MethodSummaryGraph.Summary summary;
        private readonly string[][] calls;
        private readonly string keyType;
        private readonly string access;
        private readonly string memoryArguments;
        private int block = 1;
        internal Projection(Compilation compilation, IMethodSymbol method, MethodSummaryGraph.Summary summary, params string[][] calls) {
            this.summary = summary; this.calls = calls; this.keyType = Type(method.ContainingType.TypeParameters[0]);
            this.access = "A\t" + Type(compilation.GetSpecialType(SpecialType.System_Byte)) + "\n";
            this.memoryArguments = string.Concat(method.Parameters.Select(parameter => "\t" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture) + ":value:p" + parameter.Ordinal.ToString(CultureInfo.InvariantCulture)));
            this.text = new StringBuilder("v3\nM\tU\tvalue\t").Append(Type(method.ReturnType)).Append("\nI\tthis\tU\tref\t")
                .Append(Type(method.ContainingType)).Append('\n');
            foreach (var parameter in method.Parameters)
                this.text.Append("P\tp").Append(parameter.Ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(parameter.Type is ITypeParameterSymbol ? "T" : "U").Append("\tvalue\t").Append(Type(parameter.Type)).Append('\n');
            this.text.Append("V\tv0\tT\t").Append(this.keyType).Append('\n');
            for (var i = 0; i < calls.Length; ++i)
                this.text.Append("S\ts").Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Encode(string.Join("\t", calls[i]))).Append('\n');
            this.text.Append("B\t0\tEntry\tNone\t1:Regular\t-\nB\t1\tBlock\tNone\t2:Regular\t-\n").Append(this.access);
            this.block = 2;
        }
        private void Operation(int index, int loop) { var row = (string[])this.calls[index].Clone(); row[1] = loop.ToString(CultureInfo.InvariantCulture); this.summary.Operations.Add(row); }
        private void Optional(string instructions, bool loop, bool throws = false) {
            var first = this.block; this.block += 2;
            this.text.Append("B\t").Append(Number(first)).Append("\tBlock\tWhenTrue\t").Append(Number(this.block)).Append(":Regular\t").Append(Number(first + 1)).Append(":Regular\n")
                .Append("B\t").Append(Number(first + 1)).Append("\tBlock\tNone\t")
                .Append(throws ? "-:Throw" : (loop ? first : this.block).ToString(CultureInfo.InvariantCulture) + ":Regular").Append("\t-\n").Append(instructions);
        }
        internal void Find(string value, int loop) {
            this.Hash(value, loop);
            this.Operation(1, 1);
            this.Optional(this.access + "O\t" + this.keyType + "\nZ\tv0\nC\t-\ts1\tvalue\tv0\t0:value:" + value + "\n" + this.access, true);
        }
        internal void Hash(string value, int loop) {
            this.Operation(0, loop);
            this.Optional(this.access + (value == "v0" ? "O\t" + this.keyType + "\nZ\tv0\n" : "") +
                "C\t-\ts0\tvalue\t" + value + "\n" + this.access, loop > 0);
        }
        internal void Memory() {
            this.Operation(2, 0);
            this.text.Append("B\t").Append(Number(this.block)).Append("\tBlock\tNone\t").Append(Number(this.block + 1)).Append(":Regular\t-\nC\t-\ts2\tvalue\t-").Append(this.memoryArguments).Append('\n');
            ++this.block;
        }
        internal void FormatAndThrow() { this.Operation(3, 0); this.Optional("T\ts3\tp0\n", false, true); }
        internal string Finish() {
            this.text.Append("B\t").Append(Number(this.block)).Append("\tBlock\tNone\t").Append(Number(this.block + 1)).Append(":Return\t-\nR\t-\nB\t")
                .Append(Number(this.block + 1)).Append("\tExit\tNone\t-\t-\n");
            return MethodSynchronizationFlow.Schema + "," + MethodSynchronizationFlow.ExceptionSchema + "," + MethodSynchronizationFlow.FlagPrefix + Encode(this.text.ToString());
        }
    }
}
