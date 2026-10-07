using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

// Instantiate allocator provenance independently of CLR type substitution. A
// specialization still executes the complete helper body; it is NEVER a leaf.
// Both effect rows and ordered synchronization symbols use the same identities.
// Raw, portable facts are exported; the finite specialization graph is run-local.
internal static class ContainerAllocatorBindings {
    internal const string Schema = "container-allocator-schema=1";
    internal const string Arguments = "!container-allocator-arguments=";
    internal const string Parameter = "!container-allocator-parameter=";
    private const string Prefix = "!container-allocator-";
    private const string Suffix = "~ContainerAllocatorV1:";

    internal static void Expand(Compilation compilation,
        Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, CancellationToken cancellation) {
        if (compilation.Options.MetadataImportOptions != MetadataImportOptions.All)
            compilation = compilation.WithOptions(compilation.Options.WithMetadataImportOptions(MetadataImportOptions.All));
        // Original rows are immutable snapshots: the unknown-allocator instance
        // must not erase conditional facts needed by later proven instances.
        var originals = new Dictionary<(string Assembly, string Id), MethodSummaryGraph.Summary>(methods);
        var pending = new Queue<((string Assembly, string Id) Key, MethodSummaryGraph.Summary Target, int[] Builtin)>();
        foreach (var pair in methods.Where(pair => pair.Value.Operations.Any(row => row.Any(token => token.StartsWith(Prefix, StringComparison.Ordinal))))
                     .OrderBy(pair => pair.Key.Assembly, StringComparer.Ordinal).ThenBy(pair => pair.Key.Id, StringComparer.Ordinal)) {
            originals[pair.Key] = Copy(pair.Value);
            pending.Enqueue((pair.Key, pair.Value, Array.Empty<int>()));
        }
        var symbols = new Dictionary<(string Assembly, string Id), IMethodSymbol?>();
        var variants = new Dictionary<(string Assembly, string Id), int>();
        var totalVariants = 0;
        IMethodSymbol? Symbol((string Assembly, string Id) key) {
            if (symbols.TryGetValue(key, out var value)) return value;
            var candidates = DocumentationCommentId.GetSymbolsForDeclarationId(key.Id, compilation).OfType<IMethodSymbol>()
                .Where(method => method.ContainingAssembly.Identity.ToString() == key.Assembly).ToArray();
            return symbols[key] = candidates.Length == 1 ? candidates[0] : null;
        }
        while (pending.Count != 0) {
            cancellation.ThrowIfCancellationRequested();
            var item = pending.Dequeue();
            var original = originals[item.Key];
            var errors = new SortedSet<string>(StringComparer.Ordinal);
            var caller = Symbol(item.Key);
            bool Borrow(IMethodSymbol? method, int ordinal) => method != null &&
                ContainerBorrowProof.Preserves(method, ordinal, compilation, cancellation);
            string[] Invalid(string[] row, string reason) {
                errors.Add(reason + ": " + (row.Length > 3 ? row[3] : item.Key.Id));
                return row.Where(token => !token.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            }
            string[] Bind(string[] row) {
                var markers = row.Where(token => token.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
                if (markers.Length == 0) return row;
                if (!original.Flags.Where(flag => flag.StartsWith("container-allocator-schema=", StringComparison.Ordinal)).SequenceEqual(new[] { Schema }) ||
                    caller == null || row.Length < 6 || row[0] != "call" || markers.Length != 1 ||
                    !MethodSummaryType.TryDecode(row[4], out var owner) || owner!.Kind != 'n')
                    return Invalid(row, "MalformedContainerAllocatorBinding");
                var clean = row.Where(token => !token.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
                var marker = markers[0];
                if (marker.StartsWith(Parameter + "v1:", StringComparison.Ordinal)) {
                    if (!Number(marker.Substring((Parameter + "v1:").Length), out var ordinal) || !Borrow(caller, ordinal) ||
                        caller.Parameters[ordinal].Type is not INamedTypeSymbol container)
                        return Invalid(row, "InvalidContainerAllocatorParameter");
                    IMethodSymbol? target = null;
                    if (MethodSummaryType.From(container).Encode() == row[4])
                        target = MethodSummaryInterfaceMap.Members(container).SingleOrDefault(method => MethodSummaryIdentity.Get(method) == row[3]);
                    else if (MethodSummaryContracts.Value(row, "constrained") == MethodSummaryType.From(container).Encode()) {
                        var contract = MethodSummaryTypeResolver.Resolve(owner, compilation) as INamedTypeSymbol;
                        var member = contract?.GetMembers().OfType<IMethodSymbol>().SingleOrDefault(method => MethodSummaryIdentity.Get(method) == row[3]);
                        if (member != null) target = container.FindImplementationForInterfaceMember(member) as IMethodSymbol;
                    }
                    if (target == null || row[2] != (MethodSummaryContracts.Value(row, "constrained") == null ? target.ContainingAssembly.Identity.ToString() : owner.Identity.Split('\n')[0]))
                        return Invalid(row, "InvalidContainerAllocatorTarget");
                    var contractText = LocalContainerAllocatorProof.AllocationContract(target, compilation);
                    if (contractText.Length == 0 || MethodSummaryContracts.ArgumentEnd(row) != 5 ||
                        MethodSummaryContracts.Has(clean, "ecs-leaf") || MethodSummaryContracts.Value(clean, "native-map-local-allocator") != null)
                        return Invalid(row, "InvalidContainerAllocatorContract");
                    return item.Builtin.Contains(ordinal) ? clean.Concat(contractText.Split('\t').Skip(1)).ToArray() : clean;
                }
                if (!marker.StartsWith(Arguments + "v1:", StringComparison.Ordinal)) return Invalid(row, "UnsupportedContainerAllocatorBinding");
                var calleeKey = (Assembly: row[2], Id: row[3]);
                var callee = Symbol(calleeKey);
                if (callee == null || !originals.TryGetValue(calleeKey, out var body) || conflicts.Contains(calleeKey) ||
                    owner.Identity != MethodSummaryType.From(callee.ContainingType).Identity ||
                    MethodSummaryContracts.ArgumentEnd(row) != 5 + callee.Arity || MethodSummaryContracts.Has(row, "ecs-leaf"))
                    return Invalid(row, "InvalidContainerAllocatorHelper");
                var variables = MethodSummaryType.Environment(callee);
                var values = owner.Arguments.ToList();
                for (var i = 5; i < MethodSummaryContracts.ArgumentEnd(row); ++i) {
                    if (!MethodSummaryType.TryDecode(row[i], out var value)) return Invalid(row, "InvalidContainerAllocatorTypeArgument");
                    values.Add(value!);
                }
                if (variables.Length != values.Count) return Invalid(row, "InvalidContainerAllocatorArity");
                var types = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
                for (var i = 0; i < variables.Length; ++i) types[variables[i].Identity] = values[i];
                var builtin = new List<int>();
                var previous = -1;
                foreach (var binding in marker.Substring((Arguments + "v1:").Length).Split('|')) {
                    var pair = binding.Split('=');
                    if (pair.Length != 2 || !Number(pair[0], out var ordinal) || ordinal <= previous || !Borrow(callee, ordinal))
                        return Invalid(row, "MalformedContainerAllocatorArguments");
                    previous = ordinal;
                    if (pair[1] == "b") builtin.Add(ordinal);
                    else if (pair[1].StartsWith("p", StringComparison.Ordinal) && Number(pair[1].Substring(1), out var source) && Borrow(caller, source)) {
                        if (MethodSummaryType.From(callee.Parameters[ordinal].Type).Substitute(types).Encode() != MethodSummaryType.From(caller.Parameters[source].Type).Encode())
                            return Invalid(row, "InvalidContainerAllocatorParameterType");
                        if (item.Builtin.Contains(source)) builtin.Add(ordinal);
                    } else return Invalid(row, "InvalidContainerAllocatorSource");
                }
                if (builtin.Count == 0) return clean;
                var id = calleeKey.Id + Suffix + string.Join("|", builtin.Select(index => index.ToString(CultureInfo.InvariantCulture)));
                var key = (calleeKey.Assembly, id);
                if (originals.ContainsKey(key) || conflicts.Contains(key)) return Invalid(row, "ConflictingContainerAllocatorInstance");
                if (!methods.ContainsKey(key)) {
                    variants.TryGetValue(calleeKey, out var count);
                    if (count >= 256 || totalVariants >= 10000) return Invalid(row, "ContainerAllocatorInstantiationLimit");
                    variants[calleeKey] = count + 1;
                    ++totalVariants;
                    var specialized = Copy(body);
                    specialized.Id = id;
                    specialized.AllocatorOrigin = calleeKey.Id;
                    specialized.Flags = specialized.Flags.Where(flag => flag != "job-root" && flag != "system-root").ToArray();
                    methods.Add(key, specialized);
                    pending.Enqueue((calleeKey, specialized, builtin.ToArray()));
                }
                clean[3] = id;
                return clean;
            }
            item.Target.Operations.Clear();
            foreach (var row in original.Operations) item.Target.Operations.Add(Bind(row));
            var flags = new List<string>();
            foreach (var flag in item.Target.Flags) {
                if (!flag.StartsWith(MethodSynchronizationFlow.FlagPrefix, StringComparison.Ordinal)) { flags.Add(flag); continue; }
                if (flag.Length > 16777216) { flags.Add(flag); errors.Add("ContainerAllocatorFlowSizeLimit"); continue; }
                try {
                    var lines = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(flag.Substring(MethodSynchronizationFlow.FlagPrefix.Length))).Split('\n');
                    for (var i = 0; i < lines.Length; ++i) {
                        var row = lines[i].Split('\t');
                        if (row.Length != 3 || row[0] != "S") continue;
                        var contract = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(row[2])).Split('\t');
                        lines[i] = row[0] + "\t" + row[1] + "\t" + Encode(string.Join("\t", Bind(contract)));
                    }
                    if (errors.Count != 0) lines = lines.Take(1).Concat(errors.Select(error => "G\t" + error)).Concat(lines.Skip(1)).ToArray();
                    flags.Add(MethodSynchronizationFlow.FlagPrefix + Encode(string.Join("\n", lines)));
                } catch (FormatException) { flags.Add(flag); errors.Add("MalformedContainerAllocatorFlow"); }
                catch (DecoderFallbackException) { flags.Add(flag); errors.Add("MalformedContainerAllocatorFlow"); }
            }
            item.Target.Flags = flags.ToArray();
            item.Target.Unresolved = original.Unresolved.Concat(errors).Distinct().ToArray();
        }
    }

    private static MethodSummaryGraph.Summary Copy(MethodSummaryGraph.Summary original) {
        var result = new MethodSummaryGraph.Summary { Id = original.Id, Flags = original.Flags, Unresolved = original.Unresolved, Environment = original.Environment };
        result.Operations.AddRange(original.Operations);
        return result;
    }
    private static bool Number(string value, out int number) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
        value == number.ToString(CultureInfo.InvariantCulture);
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
