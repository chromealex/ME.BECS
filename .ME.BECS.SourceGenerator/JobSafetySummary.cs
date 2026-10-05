using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ME.BECS.SourceGenerator;

internal static class JobSafetySummary {
    internal const string MetadataKey = "ME.BECS.JobSafety.v1";

    internal static string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode, bool emitInitializer = true, bool emitSizeInitializer = true,
        string rootTypePrefix = "job-type=", bool readonlyArguments = false) {
        var gaps = new HashSet<string>(StringComparer.Ordinal);
        var dependencies = new SortedDictionary<string, (int Bits, bool IsArgument)>(StringComparer.Ordinal);
        var parameterOverrides = new Dictionary<string, int>(StringComparer.Ordinal);
        var systemDependencies = new SortedSet<string>(StringComparer.Ordinal);
        var queryFilters = new SortedSet<string>(StringComparer.Ordinal);
        var componentTypes = new Dictionary<string, bool?>(StringComparer.Ordinal);
        var componentSymbols = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var pending = new Queue<(string Assembly, string Id, MethodSummaryType[] Arguments)>();
        var visited = new HashSet<(string Assembly, string Id, string Context)>();
        long contextSize = 0;
        var rootArguments = root.RootArguments ?? root.Environment;
        if (rootArguments.Any(static a => a.IsOpen)) gaps.Add("OpenGenericRoot");

        bool Add(MethodSummaryType? type, int mode, bool parameter, bool implicitAccess = false) {
            if (type == null || type.Kind != 'n' || type.IsUnsupported || type.IsOpen || type.Arguments.Length != 0) {
                gaps.Add("UnresolvedOrGenericComponent"); return false;
            }
            if (!componentTypes.TryGetValue(type.Identity, out var component)) {
                var identity = type.Identity.Split('\n');
                var symbols = identity.Length == 2 ? DocumentationCommentId.GetSymbolsForDeclarationId(identity[1], compilation)
                    .OfType<INamedTypeSymbol>().Where(s => s.ContainingAssembly.Identity.ToString() == identity[0]).ToArray() : Array.Empty<INamedTypeSymbol>();
                component = symbols.Length == 1 ? symbols[0].AllInterfaces.Any(static i => i.ToDisplayString() == "ME.BECS.IComponentBase") : (bool?)null;
                componentTypes.Add(type.Identity, component);
                if (symbols.Length == 1) componentSymbols[type.Identity] = symbols[0];
            }
            if (!component.HasValue) { gaps.Add("UnresolvedComponentDefinition: " + type.Identity.Replace('\n', '|')); return false; }
            if (!component.Value) { if (parameter) gaps.Add("NonComponentParameterOverride"); return false; }
            var bits = mode == 2 ? 3 : mode + 1;
            dependencies.TryGetValue(type.Identity, out var previous);
            if (parameter && !implicitAccess) {
                if (parameterOverrides.TryGetValue(type.Identity, out var previousOverride) && previousOverride != bits) {
                    gaps.Add("ConflictingParameterOverrides: " + type.Identity.Replace('\n', '|'));
                    bits |= previousOverride;
                }
                parameterOverrides[type.Identity] = bits;
                dependencies[type.Identity] = (bits, true);
            } else dependencies[type.Identity] = (previous.Bits | bits, previous.IsArgument || parameter);
            return true;
        }

        pending.Enqueue((assembly, root.Id, rootArguments));
        while (pending.Count != 0) {
            output.CancellationToken.ThrowIfCancellationRequested();
            var node = pending.Dequeue();
            var key = (node.Assembly, node.Id);
            var context = string.Join(";", node.Arguments.Select(static a => a.Encode()));
            if (!visited.Add((node.Assembly, node.Id, context))) continue;
            contextSize += context.Length;
            if (visited.Count > 10000 || contextSize > 16777216) { gaps.Add("TraversalLimit"); break; }
            if (conflicts.Contains(key)) { gaps.Add("ConflictingSummary: " + node.Id); continue; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingSummary: " + node.Assembly + " | " + node.Id); continue; }
            if (!method.Flags.Contains(MethodSummaryContracts.SafetySchema)) gaps.Add("MissingSafetyContracts: " + node.Id);
            if (!method.Flags.Contains(DestroyDispatchContracts.Schema)) gaps.Add("MissingDestroyContracts: " + node.Id);
            if (!method.Flags.Contains(ImplicitFormattingContracts.Schema)) gaps.Add("MissingImplicitFormattingContracts: " + node.Id);
            if (!method.Flags.Contains(GenericConstructionContracts.Schema)) gaps.Add("MissingGenericConstructionContracts: " + node.Id);
            if (MethodSummaryContracts.IsConstructor(node.Id) && !method.Flags.Contains(MethodSummaryContracts.ConstructorSchema))
                gaps.Add("MissingConstructorContract: " + node.Id);
            if (rootTypePrefix == "system-type=" && !method.Flags.Contains(MethodSummaryContracts.SchedulingSchema))
                gaps.Add("MissingScheduleSchema: " + node.Id);
            if (rootTypePrefix == "system-type=" && !method.Flags.Contains(MethodSummaryContracts.SystemAccessSchema))
                gaps.Add("MissingSystemAccessSchema: " + node.Id);
            if (rootTypePrefix == "system-type=" && !method.Flags.Contains(SystemQueryFilterContracts.Schema))
                gaps.Add("MissingQueryFilterSchema: " + node.Id);
            if (node.Arguments.Any(static a => a.IsOpen)) gaps.Add("UnboundTypeParameter: " + node.Id);
            if (method.Environment.Length != node.Arguments.Length) { gaps.Add("GenericArityMismatch: " + node.Id); continue; }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var i = 0; i < node.Arguments.Length; ++i) environment[method.Environment[i].Identity] = node.Arguments[i];
            foreach (var unresolved in method.Unresolved) {
                // Safety is a set union, not an execution-order proof. Only the new
                // producer certifies that it visited exception-region effects too;
                // old summaries and all other gaps remain incomplete. Entity
                // multiplicity and synchronization need their own proofs.
                if (unresolved == "ExceptionControlFlow" && method.Flags.Contains(MethodSummaryControlFlow.EffectUnionSchema)) continue;
                gaps.Add(unresolved + ": " + node.Id);
            }
                foreach (var rawOperation in method.Operations) {
                    var operation = JobConstrainedCall.Resolve(rawOperation, compilation, environment, gaps, methods, conflicts);
                    if (BurstStorageContracts.HasUnversionedAccess(operation, compilation))
                        gaps.Add("MissingBurstStorageAccessContract: " + operation[3]);
                    foreach (var token in operation) {
                        if (!token.StartsWith("!native-component-read=", StringComparison.Ordinal) &&
                            !token.StartsWith("!native-component-write=", StringComparison.Ordinal)) continue;
                        var type = decode(token.Substring(token.IndexOf('=') + 1))?.Substitute(environment);
                        var symbol = type == null || type.IsOpen || type.IsUnsupported ? null : MethodSummaryTypeResolver.Resolve(type, compilation);
                        if (symbol == null) gaps.Add("UnresolvedNativeMemoryType: " + operation[3]);
                        else if (MethodSummaryContracts.IsComponentType(symbol, compilation))
                            Add(type, token.StartsWith("!native-component-read=", StringComparison.Ordinal) ? 0 : 2, false);
                    }
                    if (MethodSummaryContracts.Has(operation, "scalar-comparison") || MethodSummaryContracts.Has(operation, "ecs-leaf")) continue;
                if (operation.Length < 5) { gaps.Add("MalformedOperation: " + node.Id); continue; }
                if (rootTypePrefix == "system-type=" && JobControlContracts.SystemCall(operation, method.Flags, gaps)) continue;
                if (!MethodSummaryContracts.Has(operation, "disable-safety")) {
                    var destroyed = DestroyDispatchContracts.Component(operation, compilation, environment, decode, gaps);
                    if (destroyed != null && (MethodSummaryContracts.Has(operation, "ignore") ||
                        MethodSummaryContracts.Value(operation, "safety") is "0" or "1" or "2")) {
                        Add(MethodSummaryType.From(destroyed), 2, false);
                        var target = DestroyDispatchContracts.DefaultTarget(destroyed, compilation, methods, conflicts, gaps);
                        if (target.HasValue) pending.Enqueue(target.Value);
                    }
                }
                if (rootTypePrefix == "system-type=" && operation[0] != "method-ref" &&
                    SystemQueryFilterContracts.Collect(operation, compilation, environment, decode, queryFilters, gaps)) continue;
                if (rootTypePrefix == "system-type=" && operation[0] != "method-ref" && MethodSummaryContracts.Has(operation, "query-mode-only")) continue;
                if (rootTypePrefix == "system-type=" && operation[0] != "method-ref" &&
                    MethodSummaryContracts.Value(operation, "system-access") is string systemToken) {
                    var expression = decode(systemToken)?.Substitute(environment);
                    var system = expression == null || expression.IsOpen || expression.IsUnsupported ? null : MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol;
                    var contract = compilation.GetTypeByMetadataName("ME.BECS.ISystem");
                    if (system == null || !system.IsUnmanagedType || contract == null || !system.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
                        gaps.Add("UnresolvedSystemAccess: " + operation[3]);
                    else {
                        var identity = ReflectionIdentity(system);
                        if (identity == null) gaps.Add("UnsupportedSystemIdentity: " + operation[3]);
                        else systemDependencies.Add(identity);
                    }
                    continue;
                }
                if (rootTypePrefix == "system-type=" && operation[0] != "method-ref" && MethodSummaryContracts.Value(operation, "scheduled-job") != null) {
                    var scheduled = MethodSummaryContracts.Value(operation, "scheduled-job");
                    var job = scheduled == null ? null : decode(scheduled)?.Substitute(environment);
                    if (job == null || job.IsOpen || job.IsUnsupported) gaps.Add("UnresolvedScheduledJob: " + operation[3]);
                    // Receiver/argument operations have already been analyzed. The
                    // scheduled Execute body belongs to a separate job safety summary.
                    continue;
                }
                if (operation[0] == "parameter-override" || MethodSummaryContracts.Has(operation, "disable-safety")) continue;
                var modeText = MethodSummaryContracts.Value(operation, operation[0] == "field" ? "ref" : "safety");
                if (modeText != null) {
                    var componentToken = MethodSummaryContracts.Value(operation, "component");
                    if (!TryMode(modeText, out var mode) || componentToken == null) gaps.Add("MalformedSafetyContract: " + operation[3]);
                    else if (Add(decode(componentToken)?.Substitute(environment), mode, false)) continue;
                }
                if (MethodSummaryContracts.Has(operation, "ref-unknown") || MethodSummaryContracts.Has(operation, "safety-unknown"))
                    gaps.Add("UnknownSafetyContract: " + operation[3]);
                if (operation[0] == "field" || MethodSummaryContracts.Has(operation, "ignore")) continue;
                if (operation[0] == "method-ref") gaps.Add("DeferredInvocation: " + operation[3]);
                // A constructor is an executed call, not an analysis gap merely because
                // legacy IL traversal omitted it. Require a summary with initializer coverage.
                if (operation[0] == "new" &&
                    (!methods.TryGetValue((operation[2], operation[3]), out var constructor) ||
                     !constructor.Flags.Contains(MethodSummaryContracts.ConstructorSchema)))
                    gaps.Add("MissingConstructorContract: " + operation[3]);
                var receiver = decode(operation[4])?.Substitute(environment);
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedReceiver: " + operation[3]); continue; }
                var arguments = new List<MethodSummaryType>(receiver.Arguments);
                var invalid = false;
                for (var i = 5; i < MethodSummaryContracts.ArgumentEnd(operation); ++i) {
                    var argument = decode(operation[i])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { invalid = true; break; }
                    arguments.Add(argument);
                }
                if (invalid) { gaps.Add("UnsupportedTypeArgument: " + operation[3]); continue; }
                pending.Enqueue((operation[2], operation[3], arguments.ToArray()));
            }
        }
        // Implicit ref/in contracts augment body accesses; in T must never erase a
        // Set<T> in the body. Explicit attributes keep their intentional override semantics.
        // Only the root parameters override the job's global access set, not helper parameters.
        var rootBindings = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
        for (var i = 0; i < root.Environment.Length; ++i) rootBindings[root.Environment[i].Identity] = rootArguments[i];
        foreach (var operation in root.Operations.Where(static o => o[0] == "parameter-override")
                     .OrderBy(static o => MethodSummaryContracts.Has(o, "implicit") ? 0 : 1)) {
            if (operation.Length < 6 || !TryMode(MethodSummaryContracts.Value(operation, "mode"), out var mode)) {
                gaps.Add("MalformedParameterOverride"); continue;
            }
            // AsReadonly applies to supplied component refs, not independent Get/Set/Ref
            // operations in Execute (even when they access the same component type).
            Add(decode(operation[5])?.Substitute(rootBindings), readonlyArguments ? 0 : mode, true,
                readonlyArguments || MethodSummaryContracts.Has(operation, "implicit"));
        }
        var jobType = root.Flags.FirstOrDefault(f => f.StartsWith(rootTypePrefix, StringComparison.Ordinal))?.Substring(rootTypePrefix.Length) ?? "";
        // A complete selectable catalog must be able to emit every typeof entry.
        // Otherwise private dependencies produced a zero-gap header with no A record,
        // indistinguishable from corrupt metadata when a view/job consumer selects it.
        if (emitInitializer)
            foreach (var dependency in dependencies.Keys)
                if (!compilation.IsSymbolAccessibleWithin(componentSymbols[dependency], compilation.Assembly))
                    gaps.Add("InaccessibleSafetyComponent: " + dependency.Replace('\n', '|'));
        var lines = new List<string> { jobType, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture) };
        foreach (var system in systemDependencies) lines.Add("Y\t" + system);
        lines.AddRange(queryFilters);
        foreach (var dependency in dependencies) {
            var identity = dependency.Key.Split('\n');
            if (identity.Length != 2) continue;
            var mode = dependency.Value.Bits == 3 ? 2 : dependency.Value.Bits - 1;
            lines.Add("D\t" + identity[0] + "\t" + identity[1] + "\t" + mode.ToString(CultureInfo.InvariantCulture) + "\t" + (dependency.Value.IsArgument ? "1" : "0"));
        }
        foreach (var gap in JobSummaryDiagnostics.Describe(gaps)) lines.Add("G\t" + gap);
        if (emitInitializer && gaps.Count == 0) {
            var dependencyTypes = dependencies.Keys.Select(key => componentSymbols[key]).ToArray();
            if (dependencyTypes.All(type => compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))) {
                var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(compilation.Assembly.Identity + "\n" + jobType + "\n" + root.Id);
                var name = "JobSafetyTypes_" + suffix;
                var body = "// <auto-generated/>\nnamespace ME.BECS.SourceGenerated { public static class " + name +
                    " { public static global::System.Type[] GetTypes() => new global::System.Type[] {" +
                    string.Join(", ", dependencyTypes.Select(static type => "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")) +
                    "}; } }\n";
                output.AddSource("ME.BECS." + name + ".g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(body, System.Text.Encoding.UTF8));
                lines.Add("A\t" + compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + name + "\tGetTypes\tv1");
            }
            var components = dependencies.Keys.Select(key => componentSymbols[key])
                .Where(static type => type.AllInterfaces.Any(static contract => contract.ToDisplayString() == "ME.BECS.IComponent")).ToArray();
            if (emitSizeInitializer && components.All(type => type.IsUnmanagedType && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) &&
                compilation.GetTypeByMetadataName("ME.BECS.JobStaticInfo`1") != null &&
                compilation.GetTypeByMetadataName("Unity.Collections.LowLevel.Unsafe.UnsafeUtility") != null) {
                var suffix = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(compilation.Assembly.Identity + "\n" + jobType + "\n" + root.Id);
                var className = "JobMaxStructSize_" + suffix;
                var body = new System.Text.StringBuilder("// <auto-generated/>\nnamespace ME.BECS.SourceGenerated { public static class ")
                    .Append(className).Append(" { public static void Apply<TJob>() where TJob : struct { uint maximum = 0u;\n");
                foreach (var component in components)
                    body.Append("{ var size = (uint)global::Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<")
                        .Append(component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">(); if (size > maximum) maximum = size; }\n");
                body.Append("global::ME.BECS.JobStaticInfo<TJob>.maxStructSize = maximum; }\n")
                    .Append("public static global::System.Type[] GetComponents() => new global::System.Type[] {")
                    .Append(string.Join(", ", components.Select(static type => "typeof(" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append("};\npublic static uint[] GetSizes() => new uint[] {")
                    .Append(string.Join(", ", components.Select(static type => "(uint)global::Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ">()")))
                    .Append("}; } }\n");
                output.AddSource("ME.BECS." + className + ".g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(body.ToString(), System.Text.Encoding.UTF8));
                lines.Add("S\t" + compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + className + "\tApply\tv1");
            }
        }
        return string.Join("\n", lines);
    }

    internal static string? ReflectionIdentity(INamedTypeSymbol type) {
        var owners = MethodSummaryType.TypeOwners(type).ToArray();
        var name = (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") +
            string.Join("+", owners.Select(owner => owner.MetadataName));
        var arguments = owners.SelectMany(owner => owner.TypeArguments).ToArray();
        if (arguments.Any(argument => !(argument is INamedTypeSymbol))) return null;
        var identities = arguments.Cast<INamedTypeSymbol>().Select(ReflectionIdentity).ToArray();
        if (identities.Any(identity => identity == null)) return null;
        if (arguments.Length > 0) name += "[[" + string.Join("],[", identities) + "]]";
        return name + ", " + type.ContainingAssembly.Identity;
    }

    private static bool TryMode(string? value, out int mode) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out mode) && mode >= 0 && mode <= 2;
}
