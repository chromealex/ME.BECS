using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Registration reachability, not dependency modes or execution counts. In
// particular, this follows scheduled Execute bodies and every aspect data field.
internal sealed class RuntimeTypeUsage {
    internal const string Schema = "type-usage-schema=1";
    internal const string MetadataKey = "ME.BECS.RuntimeTypeUsage.v1";
    internal const string AspectKey = "ME.BECS.AspectStorageTypes.v1";
    // Shared only across roots of this compilation, never across Unity reloads.
    private InputManifestTypes? resolver;
    private readonly Dictionary<INamedTypeSymbol, string?> aspectCatalogs = new(SymbolEqualityComparer.Default);

    // SafetyCheck describes access modes, not registration reachability. Only
    // these engine storage boundaries can stop usage traversal after recording T.
    // SetOneShot is deliberately excluded: its body can dispatch destruction.
    private static readonly HashSet<string> ComponentBoundaries = new(StringComparer.Ordinal) {
        "M:ME.BECS.EntExt.Enable``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.Disable``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.Set``1(ME.BECS.Ent@,``0@)",
        "M:ME.BECS.EntExt.Remove``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.Get``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.GetOrThrow``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.Has``1(ME.BECS.Ent@,System.Boolean)",
        "M:ME.BECS.EntExt.Read``1(ME.BECS.Ent@)",
        "M:ME.BECS.EntExt.TryRead``1(ME.BECS.Ent@,System.Boolean@)",
        "M:ME.BECS.EntExt.TryRead``1(ME.BECS.Ent@,``0@)",
        "M:ME.BECS.EntExt.SetTag``1(ME.BECS.Ent@,System.Boolean)",
        "M:ME.BECS.EntExt.HasTag``1(ME.BECS.Ent@,System.Boolean)",
        "M:ME.BECS.EntExt.IsEnabled``1(ME.BECS.EntRO@)",
        "M:ME.BECS.EntExt.Has``1(ME.BECS.EntRO@,System.Boolean)",
        "M:ME.BECS.EntExt.Read``1(ME.BECS.EntRO@)",
        "M:ME.BECS.EntExt.TryRead``1(ME.BECS.EntRO@,System.Boolean@)",
        "M:ME.BECS.EntExt.TryRead``1(ME.BECS.EntRO@,``0@)",
        "M:ME.BECS.EntExt.HasTag``1(ME.BECS.EntRO@,System.Boolean)",
        "M:ME.BECS.EntExt.ReadStatic``1(ME.BECS.EntRO@)",
        "M:ME.BECS.EntExt.HasStatic``1(ME.BECS.EntRO@)",
        "M:ME.BECS.EntExt.TryReadStatic``1(ME.BECS.EntRO@,``0@)",
        "M:ME.BECS.UnsafeEntityConfig.HasStatic``1",
        "M:ME.BECS.UnsafeEntityConfig.ReadStatic``1",
        "M:ME.BECS.UnsafeEntityConfig.TryReadStatic``1(``0@)",
    };

    internal static void Append(StringBuilder row, IMethodSymbol method, Compilation compilation) {
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, compilation.GetTypeByMetadataName("ME.BECS.Ent")?.ContainingAssembly) ||
            method.Arity != 1) return;
        var id = method.OriginalDefinition.GetDocumentationCommentId();
        if (id != null && ComponentBoundaries.Contains(id)) row.Append("\t!usage-component-only");
        if (id == "M:ME.BECS.WorldAspectStorage.InitializeObj``1(System.UInt16)")
            row.Append("\t!usage-aspect=").Append(MethodSummaryType.From(method.TypeArguments[0]).Encode());
        else if (id == "M:ME.BECS.Ent.NewEnt_INTERNAL``1(System.UInt16,ME.BECS.JobInfo@,Unity.Collections.FixedString32Bytes@)")
            row.Append("\t!usage-entity=").Append(MethodSummaryType.From(method.TypeArguments[0]).Encode());
    }

    private static bool IsVisible(INamedTypeSymbol type) => MethodSummaryType.TypeOwners(type).All(owner =>
        owner.DeclaredAccessibility == Accessibility.Public && owner.TypeArguments.All(argument => argument is INamedTypeSymbol named && IsVisible(named)));

    internal static string? DescribeAspect(INamedTypeSymbol? type, Compilation compilation) {
        var aspect = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
        var data = compilation.GetTypeByMetadataName("ME.BECS.IAspectData");
        if (type == null || type.TypeKind != TypeKind.Struct || !type.IsUnmanagedType || aspect == null || data == null ||
            !type.AllInterfaces.Contains(aspect, SymbolEqualityComparer.Default) || MethodSummaryType.From(type).IsOpen) return null;
        var components = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic)) {
            if (field.Type is not INamedTypeSymbol storage || !storage.AllInterfaces.Contains(data, SymbolEqualityComparer.Default)) continue;
            var argument = MethodSummaryType.TypeOwners(storage).SelectMany(owner => owner.TypeArguments).FirstOrDefault();
            if (argument == null || !argument.IsUnmanagedType || !MethodSummaryContracts.IsComponentType(argument, compilation)) return null;
            components.Add(MethodSummaryType.From(argument).Encode());
        }
        return "v1\n" + MethodSummaryType.From(type).Encode() + "\n" + string.Join("\n", components);
    }

    internal static void EmitAspects(SourceProductionContext output, IEnumerable<string?> descriptions) {
        var source = new StringBuilder("// <auto-generated/>\n");
        foreach (var payload in descriptions.Where(value => value != null).Distinct().OrderBy(value => value, StringComparer.Ordinal))
            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"").Append(AspectKey).Append("\", ")
                .Append(SymbolDisplay.FormatLiteral(payload!, true)).Append(")]\n");
        output.AddSource("ME.BECS.AspectStorageTypes.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    internal string Analyze(SourceProductionContext output, Compilation compilation, string assembly, MethodSummaryGraph.Summary root,
        IReadOnlyDictionary<(string Assembly, string Id), MethodSummaryGraph.Summary> methods,
        ISet<(string Assembly, string Id)> conflicts, Func<string, MethodSummaryType?> decode, string? rootBinding, string? rootIdentity = null) {
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        var types = new SortedDictionary<string, (string Kind, INamedTypeSymbol Type)>(StringComparer.Ordinal);
        var pending = new Queue<(string Assembly, string Id, MethodSummaryType[] Arguments)>();
        var visited = new HashSet<(string Assembly, string Id, string Context)>();
        var componentContract = compilation.GetTypeByMetadataName("ME.BECS.IComponentBase");
        var aspectContract = compilation.GetTypeByMetadataName("ME.BECS.IAspect");
        var entityContract = compilation.GetTypeByMetadataName("ME.BECS.IEntityType");
        long contextSize = 0;
        bool Implements(INamedTypeSymbol type, INamedTypeSymbol? contract) => contract != null && type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default);

        void Add(INamedTypeSymbol? type, string kind) {
            if (type == null || !type.IsUnmanagedType || type.IsRefLikeType || MethodSummaryType.From(type).IsOpen ||
                kind == "C" && !Implements(type, componentContract) || kind == "A" && !Implements(type, aspectContract) ||
                kind == "E" && !Implements(type, entityContract)) { gaps.Add("InvalidUsageType: " + kind + " " + type); return; }
            var identity = JobSafetySummary.ReflectionIdentity(type);
            if (identity == null || !IsVisible(type) || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)) { gaps.Add("InaccessibleUsageType: " + type); return; }
            var key = kind + "\t" + identity;
            if (types.ContainsKey(key)) return;
            types.Add(key, (kind, type));
            if (kind != "A") return;
            var header = "v1\n" + MethodSummaryType.From(type).Encode() + "\n";
            if (!this.aspectCatalogs.TryGetValue(type, out var payload)) {
                if (type.DeclaringSyntaxReferences.Length > 0) payload = DescribeAspect(type, compilation);
                else {
                    var matches = type.ContainingAssembly.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute" &&
                            attribute.ConstructorArguments.Length == 2 && attribute.ConstructorArguments[0].Value as string == AspectKey)
                        .Select(attribute => attribute.ConstructorArguments[1].Value as string).Where(value => value?.StartsWith(header, StringComparison.Ordinal) == true).ToArray();
                    payload = matches.Length == 1 ? matches[0] : null;
                }
                this.aspectCatalogs.Add(type, payload);
            }
            if (payload == null || !payload.StartsWith(header, StringComparison.Ordinal)) { gaps.Add("MissingAspectStorageCatalog: " + type); return; }
            var entries = payload.Substring(header.Length);
            if (entries.Length == 0) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries.Split('\n')) {
                if (!seen.Add(entry)) gaps.Add("DuplicateAspectStorageType: " + type);
                var expression = decode(entry);
                Add(expression == null ? null : MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol, "C");
            }
        }
        void AddExpression(string? token, IReadOnlyDictionary<string, MethodSummaryType> environment, string kind) {
            var expression = token == null ? null : decode(token)?.Substitute(environment);
            Add(expression == null || expression.IsOpen || expression.IsUnsupported ? null : MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol, kind);
        }
        void Job(INamedTypeSymbol? job) {
            Add(job, "J");
            if (job == null) return;
            foreach (var contract in job.AllInterfaces)
                foreach (var argument in contract.TypeArguments.OfType<INamedTypeSymbol>()) {
                    if (Implements(argument, componentContract)) Add(argument, "C");
                    else if (Implements(argument, aspectContract)) Add(argument, "A");
                }
            var targets = new HashSet<(string Assembly, string Id)>();
            var owner = MethodSummaryType.From(job);
            foreach (var contract in job.AllInterfaces.Where(type => type.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                         type.ContainingNamespace.ToDisplayString() is "ME.BECS.Jobs" or "Unity.Jobs")) {
                foreach (var member in contract.GetMembers("Execute").OfType<IMethodSymbol>()) {
                    var implementation = MethodSummaryInterfaceMap.Implementation(job, member);
                    var id = implementation == null ? null : MethodSummaryIdentity.Get(implementation);
                    if (id == null || !methods.ContainsKey((job.ContainingAssembly.Identity.ToString(), id)))
                        id = MethodSummaryIdentity.Get(member) is string memberId ? MethodSummaryInterfaceMap.FindSourceBody(owner,
                            MethodSummaryType.From(contract), member.ContainingAssembly.Identity.ToString(), memberId, methods, conflicts) : null;
                    if (id == null || member.Arity != 0 || implementation?.IsAbstract == true || implementation?.Arity > 0 ||
                        implementation != null && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, job)) gaps.Add("UnknownUsageExecute: " + job);
                    else targets.Add((job.ContainingAssembly.Identity.ToString(), id));
                }
            }
            if (targets.Count != 1) { gaps.Add("MissingOrAmbiguousUsageExecute: " + job); return; }
            var target = targets.Single();
            pending.Enqueue((target.Assembly, target.Id, owner.Arguments));
        }

        pending.Enqueue((assembly, root.Id, root.RootArguments ?? root.Environment));
        while (pending.Count != 0) {
            output.CancellationToken.ThrowIfCancellationRequested();
            var node = pending.Dequeue();
            var key = (node.Assembly, node.Id);
            var context = string.Join(";", node.Arguments.Select(type => type.Encode()));
            if (!visited.Add((node.Assembly, node.Id, context))) continue;
            contextSize += context.Length;
            if (visited.Count > 10000 || contextSize > 16777216) { gaps.Add("UsageTraversalLimit"); break; }
            if (conflicts.Contains(key)) { gaps.Add("ConflictingUsageSummary: " + node.Id); continue; }
            if (!methods.TryGetValue(key, out var method)) { gaps.Add("MissingUsageSummary: " + node.Assembly + " | " + node.Id); continue; }
            if (method.Flags.Contains("ME.BECS.CodeGeneratorIgnoreAttribute")) continue;
            foreach (var schema in new[] { Schema, MethodSummaryContracts.SafetySchema, MethodSummaryContracts.SchedulingSchema,
                         SystemQueryFilterContracts.Schema, DestroyDispatchContracts.Schema, ImplicitFormattingContracts.Schema, GenericConstructionContracts.Schema })
                if (method.Flags.Count(flag => flag == schema) != 1) gaps.Add("MissingUsageContract: " + schema + " " + node.Id);
            if (method.Flags.Count(flag => flag.StartsWith("type-usage-schema=", StringComparison.Ordinal)) != 1) gaps.Add("InvalidUsageSchema: " + node.Id);
            if (MethodSummaryContracts.IsConstructor(node.Id) && !method.Flags.Contains(MethodSummaryContracts.ConstructorSchema)) gaps.Add("MissingUsageConstructor: " + node.Id);
            if (method.Environment.Length != node.Arguments.Length || node.Arguments.Any(type => type.IsOpen || type.IsUnsupported)) { gaps.Add("UnboundUsageContext: " + node.Id); continue; }
            var environment = new Dictionary<string, MethodSummaryType>(StringComparer.Ordinal);
            for (var index = 0; index < node.Arguments.Length; ++index) environment[method.Environment[index].Identity] = node.Arguments[index];
            foreach (var gap in method.Unresolved)
                if (gap != "ExceptionControlFlow" || !method.Flags.Contains(MethodSummaryControlFlow.EffectUnionSchema)) gaps.Add(gap + ": " + node.Id);
            foreach (var raw in method.Operations) {
                var operation = JobConstrainedCall.Resolve(raw, compilation, environment, gaps, methods, conflicts);
                if (operation.Length < 5) { gaps.Add("MalformedUsageOperation: " + node.Id); continue; }
                foreach (var token in operation.Where(token => token.StartsWith("!native-component-read=", StringComparison.Ordinal) || token.StartsWith("!native-component-write=", StringComparison.Ordinal))) {
                    var expression = decode(token.Substring(token.IndexOf('=') + 1))?.Substitute(environment);
                    var type = expression == null ? null : MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol;
                    if (type == null) gaps.Add("UnresolvedUsageMemoryType: " + node.Id);
                    else if (Implements(type, componentContract)) Add(type, "C");
                }
                if (operation[0] == "parameter-override") { AddExpression(operation.Length > 5 ? operation[5] : null, environment, "C"); continue; }
                var component = MethodSummaryContracts.Value(operation, "component");
                if (component != null) AddExpression(component, environment, "C");
                if (operation[0] == "method-ref") { gaps.Add("DeferredUsageInvocation: " + operation[3]); continue; }
                var destroyed = DestroyDispatchContracts.Component(operation, compilation, environment, decode, gaps);
                if (destroyed != null) {
                    var target = DestroyDispatchContracts.DefaultTarget(destroyed, compilation, methods, conflicts, gaps);
                    if (target.HasValue) pending.Enqueue(target.Value);
                }
                var aspect = MethodSummaryContracts.Value(operation, "usage-aspect");
                if (aspect != null) { AddExpression(aspect, environment, "A"); continue; }
                var entity = MethodSummaryContracts.Value(operation, "usage-entity");
                if (entity != null) { AddExpression(entity, environment, "E"); continue; }
                var filters = new SortedSet<string>(StringComparer.Ordinal);
                if (SystemQueryFilterContracts.Collect(operation, compilation, environment, decode, filters, gaps)) {
                    foreach (var filter in filters) Add((this.resolver ??= new InputManifestTypes(compilation, output.CancellationToken))
                        .ResolveDefinition(filter.Split('\t')[2], out _), "C");
                    if (MethodSummaryContracts.Value(operation, "query-filter") == "aspect") AddExpression(MethodSummaryContracts.Value(operation, "query-type-0"), environment, "A");
                    continue;
                }
                var scheduled = MethodSummaryContracts.Value(operation, "scheduled-job");
                if (scheduled != null) {
                    var expression = decode(scheduled)?.Substitute(environment);
                    Job(expression == null || expression.IsOpen || expression.IsUnsupported ? null : MethodSummaryTypeResolver.Resolve(expression, compilation) as INamedTypeSymbol);
                }
                if (operation[0] == "field" || MethodSummaryContracts.Has(operation, "ecs-leaf") || MethodSummaryContracts.Has(operation, "scalar-comparison") ||
                    MethodSummaryContracts.Has(operation, "ignore") || MethodSummaryContracts.Has(operation, "deferred-job-call") ||
                    component != null && MethodSummaryContracts.Has(operation, "usage-component-only") && MethodSummaryContracts.Value(operation, "safety") is "0" or "1" or "2" ||
                    MethodSummaryContracts.Has(operation, "query-mode-only") || JobControlContracts.SystemCall(operation, method.Flags, gaps)) continue;
                var receiver = decode(operation[4])?.Substitute(environment);
                var arguments = new List<MethodSummaryType>();
                if (receiver == null || receiver.Kind != 'n' || receiver.IsUnsupported) { gaps.Add("UnsupportedUsageReceiver: " + operation[3]); continue; }
                arguments.AddRange(receiver.Arguments);
                var valid = true;
                for (var index = 5; index < MethodSummaryContracts.ArgumentEnd(operation); ++index) {
                    var argument = decode(operation[index])?.Substitute(environment);
                    if (argument == null || argument.IsUnsupported) { valid = false; break; }
                    arguments.Add(argument);
                }
                if (!valid) { gaps.Add("UnsupportedUsageArguments: " + operation[3]); continue; }
                pending.Enqueue((operation[2], operation[3], arguments.ToArray()));
            }
        }
        if (rootBinding == null) gaps.Add("MissingUsageRootBinding");
        var identity = rootIdentity ?? root.Flags.Single(flag => flag.StartsWith("system-type=", StringComparison.Ordinal)).Substring("system-type=".Length);
        var rows = new List<string> { identity, root.Id, gaps.Count.ToString(CultureInfo.InvariantCulture) };
        rows.AddRange(types.Keys);
        if (rootBinding != null) rows.Add(rootBinding);
        rows.AddRange(JobSummaryDiagnostics.Describe(gaps).Select(gap => "G\t" + gap));
        if (gaps.Count == 0) {
            var name = "RuntimeTypeUsage_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(compilation.Assembly.Identity + "\n" + identity + "\n" + root.Id);
            var source = new StringBuilder("// <auto-generated/>\nnamespace ME.BECS.SourceGenerated { public static class ").Append(name).Append(" {\n");
            foreach (var kind in new[] { ("C", "Components"), ("A", "Aspects"), ("E", "EntityTypes"), ("J", "Jobs") })
                source.Append("public static global::System.Type[] Get").Append(kind.Item2).Append("() => new global::System.Type[] {")
                    .Append(string.Join(",", types.Values.Where(value => value.Kind == kind.Item1).Select(value => "typeof(" + value.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append("};\n");
            source.Append("} }\n");
            output.AddSource("ME.BECS." + name + ".g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
            rows.Add("P\t" + compilation.Assembly.Identity + "\tME.BECS.SourceGenerated." + name + "\tv1");
        }
        return string.Join("\n", rows);
    }
}
