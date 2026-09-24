using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobEntitySummary {
    internal const string MetadataKey = "ME.BECS.JobEntityCounts.v1";

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode, bool emitInitializer = true) {
        var ent = compilation.GetTypeByMetadataName("ME.BECS.Ent");
        var terminal = ent?.GetMembers("NewEnt_INTERNAL").OfType<IMethodSymbol>()
            .SingleOrDefault(static m => m.Arity == 1);
        var terminalId = terminal?.GetDocumentationCommentId();
        var terminalAssembly = ent?.ContainingAssembly.Identity.ToString();
        // Roslyn's public metadata import can hide internal methods in referenced asmdefs.
        // The declaring compilation exports their summaries, including the allocation contract.
        if (terminalId == null && terminalAssembly != null) {
            var exported = methods.Where(m => m.Key.Assembly == terminalAssembly &&
                m.Value.Flags.Contains("entity-creation-contract=1") && !conflicts.Contains(m.Key)).ToArray();
            if (exported.Length == 1) terminalId = exported[0].Key.Id;
        }
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var maximumText = root.Flags.FirstOrDefault(static flag => flag.StartsWith("entity-max-count=", StringComparison.Ordinal))?.Substring(17);
        if (!root.Flags.Contains("entity-limit-schema=1")) gaps.Add("MissingEntityLimitContract");
        if (!uint.TryParse(maximumText, NumberStyles.None, CultureInfo.InvariantCulture, out var maximum)) gaps.Add("InvalidEntitiesJobMaxCount");
        var counts = new SortedDictionary<string, (int Inline, int Loop)>(StringComparer.Ordinal);
        var cachedCounts = new Dictionary<(string Assembly, string Id, string Context, bool InLoop), Dictionary<string, (int Inline, int Loop)>>();
        var active = new HashSet<(string Assembly, string Id, string Context)>();
        var work = 0;
        long contextSize = 0;
        long cachedContributionCount = 0;
        var rootArguments = root.RootArguments ?? root.Environment;
        if (rootArguments.Any(static a => a.IsOpen)) gaps.Add("OpenGenericRoot");
        if (terminalId == null) gaps.Add("MissingEntityCreationContract");

        void AddCount(string identity, int inline, int loop) {
            counts.TryGetValue(identity, out var previous);
            if (inline > int.MaxValue - previous.Inline || loop > int.MaxValue - previous.Loop) {
                gaps.Add("EntityCountOverflow");
                return;
            }
            counts[identity] = (previous.Inline + inline, previous.Loop + loop);
        }

        void Visit(string targetAssembly, string id, MethodSummaryType[] arguments, bool inLoop, int depth) {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (++work > 20000 || depth > 128) { gaps.Add("TraversalLimit"); return; }
            if (cachedContributionCount > 200000) { gaps.Add("EntityCountCacheLimit"); return; }
            var key = (targetAssembly, id);
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + id); return; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + targetAssembly + " | " + id); return; }
            if (method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreAttribute")) return;
            if (arguments.Any(static a => a.IsOpen)) gaps.Add("UnboundTypeParameter: " + id);
            var context = string.Join(";", arguments.Select(static a => a.Encode()));
            contextSize += context.Length;
            if (contextSize > 16777216) { gaps.Add("TraversalContextLimit"); return; }
            var instance = (targetAssembly, id, context);
            if (active.Contains(instance)) { gaps.Add("RecursiveCreationPath: " + id); return; }
            var cacheKey = (targetAssembly, id, context, inLoop);
            if (cachedCounts.TryGetValue(cacheKey, out var cached)) {
                // Reuse analysis, not its effect: every call site reserves its own entities.
                foreach (var contribution in cached) AddCount(contribution.Key, contribution.Value.Inline, contribution.Value.Loop);
                return;
            }
            if (method.Environment.Length != arguments.Length) { gaps.Add("GenericArityMismatch: " + id); return; }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Length; ++i) environment[method.Environment[i].Identity] = arguments[i];
            foreach (var unresolved in method.Unresolved) gaps.Add(unresolved + ": " + id);
            var before = new Dictionary<string, (int Inline, int Loop)>(counts, StringComparer.Ordinal);
            active.Add(instance);
                foreach (var rawOperation in method.Operations) {
                    var operation = JobConstrainedCall.Resolve(rawOperation, compilation, environment, gaps, methods, conflicts);
                    if (MethodSummaryContracts.Has(operation, "scalar-comparison") || MethodSummaryContracts.Has(operation, "ecs-leaf")) continue;
                if (work > 20000) break;
                if (operation.Length < 5 || !int.TryParse(operation[1], NumberStyles.None, CultureInfo.InvariantCulture, out var loopDepth)) {
                    gaps.Add("MalformedOperation: " + id); continue;
                }
                if (operation[0] == "field" || operation[0] == "parameter-override" || MethodSummaryContracts.Has(operation, "ignore")) continue;
                if (operation[0] == "method-ref") { gaps.Add("DeferredInvocation: " + operation[3]); continue; }
                // A constructor is an executed call, not an analysis gap merely because
                // legacy IL traversal omitted it. Require a summary with initializer coverage.
                if (operation[0] == "new" &&
                    (!methods.TryGetValue((operation[2], operation[3]), out var constructor) ||
                     !constructor.Flags.Contains("constructor-schema=1")))
                    gaps.Add("MissingConstructorContract: " + operation[3]);
                var receiver = decode(operation[4])?.Substitute(environment);
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedReceiver: " + operation[3]); continue; }
                var targetArguments = new List<MethodSummaryType>(receiver.Arguments);
                var invalid = false;
                for (var i = 5; i < MethodSummaryContracts.ArgumentEnd(operation); ++i) {
                    var argument = decode(operation[i])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { invalid = true; break; }
                    targetArguments.Add(argument);
                }
                if (invalid) { gaps.Add("UnsupportedTypeArgument: " + operation[3]); continue; }
                var loop = inLoop || loopDepth > 0;
                if (operation[2] == terminalAssembly && operation[3] == terminalId && targetArguments.Count == 1) {
                    var entityType = targetArguments[0];
                    if (entityType.Kind != 'n' || entityType.IsOpen || entityType.Arguments.Length != 0) { gaps.Add("UnresolvedEntityType"); continue; }
                    AddCount(entityType.Identity, loop ? 0 : 1, loop ? 1 : 0);
                    // This is the allocation contract; do not count its implementation as a second creation.
                    continue;
                }
                Visit(operation[2], operation[3], targetArguments.ToArray(), loop, depth + 1);
            }
            active.Remove(instance);
            var contributionCounts = new Dictionary<string, (int Inline, int Loop)>(StringComparer.Ordinal);
            foreach (var count in counts) {
                before.TryGetValue(count.Key, out var previous);
                var delta = (Inline: count.Value.Inline - previous.Inline, Loop: count.Value.Loop - previous.Loop);
                if (delta.Inline != 0 || delta.Loop != 0) contributionCounts.Add(count.Key, delta);
            }
            cachedContributionCount += contributionCounts.Count;
            if (cachedContributionCount <= 200000) cachedCounts[cacheKey] = contributionCounts;
            else gaps.Add("EntityCountCacheLimit");
        }

        Visit(assembly, root.Id, rootArguments, false, 0);
        var loops = counts.Sum(static entry => (long)entry.Value.Loop);
        if (loops > uint.MaxValue) gaps.Add("EntityLoopCountOverflow");
        var jobType = root.Flags.FirstOrDefault(static f => f.StartsWith("job-type=", StringComparison.Ordinal))?.Substring(9) ?? "";
        var lines = new List<string> { jobType, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture) };
        lines.Add("L\t" + maximum.ToString(CultureInfo.InvariantCulture));
        foreach (var count in counts) {
            var identity = count.Key.Split('\n');
            if (identity.Length != 2) continue;
            lines.Add("C\t" + identity[0] + "\t" + identity[1] + "\t" + count.Value.Inline.ToString(CultureInfo.InvariantCulture) + "\t" + count.Value.Loop.ToString(CultureInfo.InvariantCulture));
        }
        if (emitInitializer && gaps.Count == 0 && compilation.Options is Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions options && options.AllowUnsafe &&
            compilation.GetTypeByMetadataName("ME.BECS.JobStaticInfo`1") != null) {
            var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(compilation.Assembly.Identity + "\n" + jobType + "\n" + root.Id);
            var className = "JobEntityCounts_" + suffix;
            var inline = counts.Where(entry => entry.Value.Inline > 0 || (maximum > 0u && entry.Value.Loop > 0)).ToArray();
            var body = new System.Text.StringBuilder("// <auto-generated/>\nnamespace ME.BECS.SourceGenerated { public static unsafe class ")
                .Append(className).Append(" { public static void Apply<TJob>(uint groupCount");
            for (var index = 0; index < inline.Length; ++index)
                body.Append(", uint group").Append(index.ToString(CultureInfo.InvariantCulture));
            body.Append(") where TJob : struct {\n");
            body.Append("global::ME.BECS.JobStaticInfo<TJob>.entitiesMaxCount = ").Append(maximum.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
            body.Append("global::ME.BECS.JobStaticInfo<TJob>.loopCount = ").Append(loops.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
            if (inline.Length > 0) {
                body.Append("global::ME.BECS.JobStaticInfo<TJob>.inlineCount = global::ME.BECS.Cuts._makeArray<uint>(groupCount, global::Unity.Collections.Allocator.Domain);\n");
                for (var index = 0; index < inline.Length; ++index)
                    body.Append("global::ME.BECS.JobStaticInfo<TJob>.inlineCount[group").Append(index.ToString(CultureInfo.InvariantCulture))
                        .Append("] = ").Append((maximum > 0u && inline[index].Value.Loop > 0 ? maximum : (uint)inline[index].Value.Inline).ToString(CultureInfo.InvariantCulture)).Append("u;\n");
            } else {
                body.Append("global::ME.BECS.JobStaticInfo<TJob>.inlineCount = default;\n");
            }
            body.Append("} } }\n");
            output.AddSource("ME.BECS." + className + ".g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(body.ToString(), System.Text.Encoding.UTF8));
            // The bootstrap supplies its selected group count and IDs in reserved C-row
            // order. This assembly cannot assign IDs owned by the ordered global manifest.
            // v3 guarantees call-site multiplicity and separate inline/loop traversal contexts.
            lines.Add("I\t" + compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + className + "\tApply\tv3");
        }
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) lines.Add("G\t" + gap);
        return string.Join("\n", lines);
    }
}
