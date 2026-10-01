using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace ME.BECS.SourceGenerator;

// Transport for Unity-selected type inputs. Graph topology/config payloads require separate
// records before this can replace asset discovery. Entity registration is compiler-owned;
// its IDs follow the explicit entity-registration order, not symbol enumeration order.
[Generator(LanguageNames.CSharp)]
public sealed class InputManifestGenerator : IIncrementalGenerator {
    private static readonly DiagnosticDescriptor Invalid = new DiagnosticDescriptor("BECSG100",
        "Invalid BECS input manifest", "{0}", "ME.BECS", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor StaleInjectionPlan = new DiagnosticDescriptor("BECSG101",
        "Graph injection inputs require regeneration", "{0}", "ME.BECS", DiagnosticSeverity.Warning, true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var manifests = context.AdditionalTextsProvider.Where(static file => file.Path.EndsWith(".becs-inputs", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellation) => (file.Path, Content: file.GetText(cancellation)?.ToString())).Collect();
        context.RegisterSourceOutput(manifests.Combine(context.CompilationProvider), static (output, input) => {
            string? selected = null;
            foreach (var file in input.Left.OrderBy(f => f.Path, StringComparer.Ordinal)) {
                if (file.Content == null) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Cannot read " + file.Path)); continue; }
                var lines = file.Content.Replace("\r\n", "\n").Split('\n');
                var header = lines[0].Split('\t');
                if (header.Length != 3 || header[0] != "ME.BECS.TypeInputs.v3" || (header[2] != "editor" && header[2] != "runtime")) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid header in " + file.Path)); continue;
                }
                string target;
                try { target = Decode(header[1]); }
                catch (FormatException) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid assembly encoding in " + file.Path)); continue; }
                if (target != input.Right.AssemblyName) continue;
                if (selected != null) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Multiple manifests target " + target)); return; }
                // Classification and injection planning inspect private instance fields
                // in referenced component/aspect assemblies. Default public-only metadata
                // import is insufficient. This is a generator-local compilation view;
                // normal accessibility checks below still govern emitted source access.
                if (input.Right.Options.MetadataImportOptions != MetadataImportOptions.All)
                    input.Right = input.Right.WithOptions(input.Right.Options.WithMetadataImportOptions(MetadataImportOptions.All));
                var footerIndex = lines.Length - 2;
                var footer = footerIndex >= 1 ? lines[footerIndex].Split('\t') : Array.Empty<string>();
                var payload = footerIndex >= 1 ? string.Join("\n", lines.Take(footerIndex)) + "\n" : "";
                if (lines[lines.Length - 1].Length != 0 || footer.Length != 3 || footer[0] != "end" ||
                    footer[1] != (footerIndex - 1).ToString(CultureInfo.InvariantCulture) ||
                    footer[2] != ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(payload)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                        "Incomplete or altered manifest: " + file.Path + ". Regenerate type inputs using the current Editor exporter."));
                    return;
                }
                var kinds = new HashSet<string>(new[] { "system", "system-registration", "component", "component-group", "job", "entity", "entity-registration", "aspect", "aspect-registration", "aspect-construction", "aspect-construction-auto", "destroy-schema", "destroy-registration", "config-mask-schema", "config-collection-count-schema", "config-collection-callback-schema" }, StringComparer.Ordinal);
                var next = new Dictionary<string, int>(StringComparer.Ordinal);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                var records = new List<string>();
                var bootstrapFeeders = new List<string>();
                var bootstrapKinds = new List<string>();
                var registrationKinds = new List<string>();
                var resolutions = new List<string>();
                var entityRegistrations = new List<INamedTypeSymbol>();
                var destroyRegistrations = new List<INamedTypeSymbol>();
                var destroySchema = false;
                var maskSchema = false;
                var compilerMaskFields = false;
                var collectionCountSchema = false;
                var compilerCollectionCounts = false;
                var compilerCollectionCallbacks = false;
                var collectionCallbackSchema = false;
                var collectionCallbackTypes = new List<INamedTypeSymbol>();
                var collectionCallbackBodies = new List<string>();
                var collectionCounts = new List<(INamedTypeSymbol Type, uint Count)>();
                var maskRegistrations = new List<ConfigMaskInputEmitter>();
                var systemRegistrations = new List<(string Identity, INamedTypeSymbol Type)>();
                var aspectRegistrations = new List<(INamedTypeSymbol Type, IMethodSymbol Query)>();
                var aspectConstructors = new List<IMethodSymbol>();
                var componentRegistrations = new List<(string Identity, INamedTypeSymbol Type, int Flags)>();
                var groupRegistrations = new List<(INamedTypeSymbol Component, INamedTypeSymbol Group)>();
                var systemInjections = new List<(string Key, INamedTypeSymbol Owner, IFieldSymbol Field, ITypeSymbol Target)>();
                var deltaJobs = new List<(string Key, INamedTypeSymbol Type, IFieldSymbol[] Fields)>();
                var graphRegistrations = new List<(string Prefix, int Id, int Capacity)>();
                var graphIds = new HashSet<int>();
                var graphJobs = new Dictionary<int, List<GraphJobPatchPlan>>();
                var graphInjections = new Dictionary<int, List<(GraphJobPatchPlan Plan, int Slot)>>();
                var graphApply = new Dictionary<int, string[]>();
                var graphTopologies = new Dictionary<int, GraphTopologyInput>();
                var graphSlots = new Dictionary<int, List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex)>>();
                var resolver = new InputManifestTypes(input.Right, output.CancellationToken);
                var viewTracker = new ViewTrackerInputEmitter();
                var debugPlans = new List<DebugJobInputPlan>();
                var jobWeights = new JobWeightInputEmitter();
                var jobEntityInitializers = new JobEntityInputEmitter();
                var jobBootstrap = new JobBootstrapInputEmitter();
                var systemDependencies = new SystemDependencyInputEmitter();
                var debugSchema = false;
                for (var i = 1; i < footerIndex; ++i) {
                    output.CancellationToken.ThrowIfCancellationRequested();
                    var fields = lines[i].Split('\t');
                    if (fields[0] == "system-dependencies-schema" || fields[0] == "system-dependencies") {
                        if (header[2] != "editor" || !systemDependencies.Read(fields, resolver, input.Right)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid system dependency plan at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-early-init-schema" || fields[0] == "job-early-init") {
                        if (!jobBootstrap.Read(fields, resolver, input.Right)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job EarlyInit plan at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-entity-fallback") {
                        if (!jobEntityInitializers.ReadFallback(fields, resolver, input.Right)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job entity fallback at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-entity-initializer") {
                        if (!jobEntityInitializers.Read(fields, resolver, input.Right)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job entity initializer at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-weight") {
                        if (!jobWeights.Read(fields, resolver, input.Right, out var weightError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, weightError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-debug-schema") {
                        if (debugSchema || fields.Length != 3 || fields[1] != "0" || fields[2] != "djE=") {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid debug job schema at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        debugSchema = true;
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-debug") {
                        string debugPayload;
                        try { debugPayload = fields.Length == 3 ? Decode(fields[2]) : ""; }
                        catch (FormatException) { debugPayload = ""; }
                        if (fields.Length != 3 || fields[1] != debugPlans.Count.ToString(CultureInfo.InvariantCulture) ||
                            !DebugJobInputPlan.TryRead(debugPayload, resolver, input.Right, out var debugPlan, out _)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid debug job plan at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        if (debugPlans.Any(plan => SymbolEqualityComparer.Default.Equals(plan.Job, debugPlan.Job) &&
                            SymbolEqualityComparer.Default.Equals(plan.Contract, debugPlan.Contract))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Duplicate debug job plan at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        debugPlans.Add(debugPlan);
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "bootstrap-feeder") {
                        string identity;
                        try { identity = fields.Length >= 3 && fields.Length <= 5 ? Decode(fields[2]) : ""; }
                        catch (FormatException) { identity = ""; }
                        var kind = fields.Length >= 4 ? fields[3] : "legacy";
                        var registrationKind = fields.Length == 5 ? fields[4] : "legacy";
                        if (fields.Length < 3 || fields.Length > 5 || (registrationKind != "legacy" && BootstrapRegistrationBody(registrationKind) == null) ||
                            (kind != "legacy" && kind != "none" && BootstrapInitializationTarget(kind) == null) || fields[1] != bootstrapFeeders.Count.ToString(CultureInfo.InvariantCulture) ||
                            string.IsNullOrWhiteSpace(identity) || bootstrapFeeders.Contains(identity, StringComparer.Ordinal)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid or duplicate bootstrap feeder at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        // Editor-only feeder identities are transport data, not runtime
                        // type references. Preserve their order without loading/resolving them.
                        bootstrapFeeders.Add(identity);
                        bootstrapKinds.Add(kind);
                        registrationKinds.Add(registrationKind);
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (ViewTrackerInputEmitter.IsRecord(fields[0])) {
                        if (!viewTracker.Read(fields, resolver, input.Right, out var viewError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, viewError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    var componentRecord = fields[0] == "component-registration";
                    var groupRecord = fields[0] == "group-registration";
                    var injectionRecord = fields[0] == "system-injection";
                    var maskRecord = fields[0] == "config-mask-registration";
                    var collectionCallbackRecord = fields[0] == "config-collection-callback";
                    var collectionCountRecord = fields[0] == "config-collection-count";
                    uint collectionCount = 0;
                    var deltaRecord = fields[0] == "job-delta-registration";
                    var graphRecord = fields[0] == "graph-registration";
                    var slotRecord = fields[0] == "graph-system";
                    var graphInjectionRecord = fields[0] == "graph-system-injection";
                    var graphApplyRecord = fields[0] == "graph-apply";
                    var topologyRecord = fields[0] == "graph-topology";
                    var graphJobRecord = fields[0] == "graph-job" || graphInjectionRecord || graphApplyRecord || topologyRecord;
                    var slotIndex = 0;
                    uint slotSourceId = 0;
                    var slotNodeIndex = 0;
                    var graphId = 0;
                    var graphCapacity = 0;
                    var componentFlags = 0;
                    var valid = graphJobRecord ? fields.Length == 5 && header[2] == "runtime" &&
                        int.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out graphId) &&
                        fields[3] == graphId.ToString(CultureInfo.InvariantCulture) : collectionCountRecord
                        ? fields.Length == 4 && uint.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out collectionCount) &&
                            collectionCount > 0 && fields[3] == collectionCount.ToString(CultureInfo.InvariantCulture) : componentRecord
                        ? fields.Length == 3 || fields.Length == 4 && int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out componentFlags) &&
                            componentFlags >= 0 && componentFlags <= 63 && fields[3] == componentFlags.ToString(CultureInfo.InvariantCulture) &&
                            (componentFlags & 5) != 5 && ((componentFlags & 16) == 0 || (componentFlags & 8) != 0)
                        : collectionCallbackRecord ? fields.Length == (compilerCollectionCallbacks ? 3 : 4)
                        : maskRecord ? fields.Length == (compilerMaskFields ? 3 : 4)
                        : groupRecord ? fields.Length == 3 || fields.Length == 4
                        : injectionRecord || deltaRecord ? fields.Length == 4 : graphRecord
                            ? fields.Length == 5 && header[2] == "runtime" && int.TryParse(fields[4], NumberStyles.None,
                                CultureInfo.InvariantCulture, out graphCapacity) && graphCapacity >= 0 &&
                                fields[4] == graphCapacity.ToString(CultureInfo.InvariantCulture) && int.TryParse(fields[3], NumberStyles.AllowLeadingSign,
                                CultureInfo.InvariantCulture, out graphId) && graphId != int.MinValue &&
                                fields[3] == graphId.ToString(CultureInfo.InvariantCulture) && graphIds.Add(graphId)
                            : slotRecord ? fields.Length == 8 && header[2] == "runtime" &&
                                int.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out graphId) &&
                                fields[3] == graphId.ToString(CultureInfo.InvariantCulture) &&
                                int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out slotIndex) &&
                                fields[4] == slotIndex.ToString(CultureInfo.InvariantCulture) && (fields[5] == "0" || fields[5] == "1") &&
                                uint.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out slotSourceId) &&
                                fields[6] == slotSourceId.ToString(CultureInfo.InvariantCulture) &&
                                int.TryParse(fields[7], NumberStyles.None, CultureInfo.InvariantCulture, out slotNodeIndex) &&
                                fields[7] == slotNodeIndex.ToString(CultureInfo.InvariantCulture) &&
                                (fields[5] == "1" ? slotSourceId == 0 && slotNodeIndex == 0 : slotSourceId != 0)
                            : fields.Length == 3 && kinds.Contains(fields[0]);
                    string type = "";
                    string groupIdentity = "";
                    if (valid) {
                        try {
                            type = Decode(fields[2]);
                            if ((groupRecord && fields.Length == 4) || injectionRecord || deltaRecord || (maskRecord && !compilerMaskFields) || (collectionCallbackRecord && !compilerCollectionCallbacks)) {
                                groupIdentity = Decode(fields[3]);
                                valid = groupIdentity.Length > 0 && !groupIdentity.Any(char.IsControl);
                            }
                            if (graphJobRecord) {
                                groupIdentity = Decode(fields[4]);
                                valid = (graphApplyRecord || groupIdentity.Length > 0) && (topologyRecord || !groupIdentity.Any(char.IsControl));
                            }
                        } catch (FormatException) { valid = false; }
                    }
                    if (valid) {
                        next.TryGetValue(fields[0], out var ordinal);
                        valid = fields[1] == ordinal.ToString(CultureInfo.InvariantCulture) && type.Length != 0 &&
                            !type.Any(char.IsControl) && unique.Add(fields[0] + "\n" + (slotRecord ? fields[3] + "\n" + fields[4] : type) +
                                (injectionRecord ? "\n" + groupIdentity : graphJobRecord ? "\n" + fields[3] : ""));
                        if (valid) next[fields[0]] = ordinal + 1;
                    }
                    if (!valid) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid or duplicate record at " + file.Path + ":" + (i + 1))); return; }
                    records.Add(header[2] + "\t" + lines[i]);
                    if (fields[0] == "config-collection-callback-schema") {
                        if ((type != "v1" && type != "v2") || collectionCallbackSchema) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid collection callback schema")); return; }
                        collectionCallbackSchema = true;
                        compilerCollectionCallbacks = type == "v2";
                        continue;
                    }
                    if (fields[0] == "config-collection-count-schema") {
                        if ((type != "v1" && type != "v2") || collectionCountSchema) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid collection count schema")); return; }
                        collectionCountSchema = true;
                        compilerCollectionCounts = type == "v2";
                        continue;
                    }
                    if (fields[0] == "config-mask-schema") {
                        if ((type != "v1" && type != "v2") || maskSchema) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid config mask schema")); return; }
                        maskSchema = true;
                        compilerMaskFields = type == "v2";
                        continue;
                    }
                    if (fields[0] == "destroy-schema") {
                        if (type != "v1" || destroySchema) { output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid destroy registration schema")); return; }
                        destroySchema = true;
                        continue;
                    }
                    if (topologyRecord) {
                        if (type != "topology" || !graphSlots.ContainsKey(graphId) || graphTopologies.ContainsKey(graphId)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid topology graph reference: " + graphId));
                            return;
                        }
                        if (!GraphTopologyInput.TryParse(groupIdentity, graphId, out var topology, out var topologyError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph topology: " + topologyError));
                            return;
                        }
                        graphTopologies.Add(graphId, topology!);
                        continue;
                    }
                    if (graphApplyRecord) {
                        var actions = groupIdentity.Length == 0 ? Array.Empty<string>() : groupIdentity.Split(',');
                        if (type != "apply" || !graphSlots.ContainsKey(graphId) || graphApply.ContainsKey(graphId) || actions.Any(action => {
                            if (action.StartsWith("s:", StringComparison.Ordinal)) return !graphInjections.TryGetValue(graphId, out var values) || !values.Any(v => v.Plan.Key == action.Substring(2));
                            if (action.StartsWith("j:", StringComparison.Ordinal)) return !graphJobs.TryGetValue(graphId, out var values) || !values.Any(v => v.Key == action.Substring(2));
                            if (action.StartsWith("d:", StringComparison.Ordinal)) return !deltaJobs.Any(v => v.Key == action.Substring(2));
                            return true;
                        })) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph apply sequence: " + lines[i]));
                            return;
                        }
                        graphApply.Add(graphId, actions);
                        continue;
                    }
                    if (graphRecord) {
                        if (!type.StartsWith("ME.BECS.", StringComparison.Ordinal) ||
                            type.Split('.').Any(part => !SyntaxFacts.IsValidIdentifier(part))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph callback prefix: " + type));
                            return;
                        }
                        graphRegistrations.Add((type, graphId, graphCapacity));
                        graphSlots.Add(graphId, new List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex)>());
                        continue;
                    }
                    var symbol = resolver.ResolveDefinition(type, out var resolutionGap);
                    if (collectionCallbackRecord) {
                        var body = ConfigCollectionsInputEmitter.Describe(symbol, input.Right,
                            compilerCollectionCallbacks ? null : groupIdentity.Split(','), fields[1]);
                        if (body == null) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid config collection fields/construction contract: " + type));
                            return;
                        }
                        collectionCallbackTypes.Add(symbol!);
                        collectionCallbackBodies.Add(body);
                    }
                    if (collectionCountRecord) {
                        if (symbol == null || !symbol.IsUnmanagedType || symbol.IsRefLikeType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) ||
                            !symbol.AllInterfaces.Any(static contract => contract.ToDisplayString() is "ME.BECS.IConfigComponent" or "ME.BECS.IConfigComponentStatic" or "ME.BECS.IConfigComponentShared") ||
                            ConfigCollectionsInputEmitter.GetCollectionCount(symbol) != collectionCount) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid config collection count: " + type));
                            return;
                        }
                        collectionCounts.Add((symbol, collectionCount));
                    }
                    if (maskRecord) {
                        if (!ConfigMaskInputEmitter.TryCreate(symbol, compilerMaskFields ? null : groupIdentity, input.Right, out var maskEntry)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid config mask type/field order: " + type));
                            return;
                        }
                        maskRegistrations.Add(maskEntry!);
                    }
                    if (graphJobRecord) {
                        var plan = graphSlots.TryGetValue(graphId, out var jobSlots)
                            ? GraphJobPatchPlan.Create(input.Right, symbol, type, groupIdentity, jobSlots.Select(static s => s.Type).ToArray()) : null;
                        if (plan == null) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid graph injection patch for " + type + " in graph " + graphId.ToString(CultureInfo.InvariantCulture) +
                                ". Check field/target layout and private-field setters in the referenced assembly. " +
                                "Jobs/systems with private injected fields and their containing types must be partial; regenerate inputs after compilation/reload."));
                            return;
                        }
                        if (graphInjectionRecord) {
                            var ownerSlot = jobSlots.FindIndex(slot => SymbolEqualityComparer.Default.Equals(slot.Type, symbol));
                            if (ownerSlot < 0 || plan.Fields.Any(static field => field.Slot < 0)) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph system injection owner: " + type));
                                return;
                            }
                            if (!graphInjections.TryGetValue(graphId, out var assignments)) graphInjections.Add(graphId, assignments = new List<(GraphJobPatchPlan Plan, int Slot)>());
                            assignments.Add((plan, ownerSlot));
                        } else {
                            if (!graphJobs.TryGetValue(graphId, out var plans)) graphJobs.Add(graphId, plans = new List<GraphJobPatchPlan>());
                            plans.Add(plan);
                        }
                    }
                    if (deltaRecord) {
                        var attribute = input.Right.GetTypeByMetadataName("ME.BECS.InjectDeltaTimeAttribute");
                        var inject = input.Right.GetTypeByMetadataName("ME.BECS.IInject");
                        var softFloat = input.Right.GetTypeByMetadataName("sfloat");
                        var instanceFields = symbol?.GetMembers().OfType<IFieldSymbol>().Where(static f => !f.IsStatic).ToArray() ?? Array.Empty<IFieldSymbol>();
                        var names = groupIdentity.Split(',');
                        var patched = names.Select(n => instanceFields.SingleOrDefault(f => f.Name == n)).ToArray();
                        if (header[2] != "runtime" || symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) ||
                            names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
                            instanceFields.Any(f => f.Type.SpecialType == SpecialType.System_Boolean || f.Type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, inject))) ||
                            instanceFields.Count(f => f.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute))) != names.Length ||
                            patched.Any(f => f == null || f.IsReadOnly ||
                                (f.DeclaredAccessibility != Accessibility.Public && !HasDeltaSetter(symbol, f, input.Right)) ||
                                !f.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)) ||
                                (f.Type.SpecialType != SpecialType.System_UInt32 && f.Type.SpecialType != SpecialType.System_Single && !SymbolEqualityComparer.Default.Equals(f.Type, softFloat)))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid delta-time job: " + type));
                            return;
                        }
                        deltaJobs.Add((ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(type), symbol, patched.Select(static f => f!).ToArray()));
                    }
                    if (slotRecord) {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.ISystem");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) || contract == null ||
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) ||
                            !graphSlots.TryGetValue(graphId, out var slots) || slots.Count != slotIndex ||
                            slotIndex >= graphRegistrations.First(g => g.Id == graphId).Capacity) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph system slot: " + lines[i]));
                            return;
                        }
                        slots.Add((symbol, fields[5] == "1", slotSourceId, slotNodeIndex));
                    }
                    if (injectionRecord) {
                        var field = symbol?.GetMembers(groupIdentity).OfType<IFieldSymbol>().SingleOrDefault();
                        var injection = input.Right.GetTypeByMetadataName("ME.BECS.InjectSystem`1");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) ||
                            field == null || field.IsStatic || field.IsReadOnly || field.DeclaredAccessibility != Accessibility.Public ||
                            field.Type is not INamedTypeSymbol data || !SymbolEqualityComparer.Default.Equals(data.OriginalDefinition, injection) ||
                            data.TypeArguments.Length != 1 || !data.TypeArguments[0].IsUnmanagedType ||
                            !input.Right.IsSymbolAccessibleWithin(data.TypeArguments[0], input.Right.Assembly)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid system injection: " + type + " / " + groupIdentity));
                            return;
                        }
                        systemInjections.Add((ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(type + "\n" + groupIdentity), symbol, field, data.TypeArguments[0]));
                    }
                    if (groupRecord) {
                        INamedTypeSymbol? group;
                        string? groupGap = null;
                        if (fields.Length == 3) {
                            var groupAttribute = input.Right.GetTypeByMetadataName("ME.BECS.ComponentGroupAttribute");
                            var attributes = symbol?.GetAttributes().Where(attribute =>
                                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, groupAttribute)).ToArray();
                            group = null;
                            if (attributes?.Length == 1 && attributes[0].ConstructorArguments.Length == 1) {
                                group = attributes[0].ConstructorArguments[0].Value as INamedTypeSymbol;
                                foreach (var argument in attributes[0].NamedArguments)
                                    if (argument.Key == "groupType") group = argument.Value.Value as INamedTypeSymbol;
                            }
                            if (group == null) groupGap = "Exactly one ComponentGroupAttribute with a named group type is required";
                        } else group = resolver.ResolveDefinition(groupIdentity, out groupGap);
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.IComponentBase");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) || contract == null ||
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) ||
                            group == null || !input.Right.IsSymbolAccessibleWithin(group, input.Right.Assembly)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid component group registration: " + type + " -> " + groupIdentity + " (" + groupGap + ")"));
                            return;
                        }
                        if (MethodSummaryType.From(group).IsOpen) group = group.ConstructUnboundGenericType();
                        groupRegistrations.Add((symbol, group));
                    }
                    if (componentRecord) {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.IComponentBase");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) || contract == null ||
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid component registration: " + type));
                            return;
                        }
                        if (fields.Length == 3) componentFlags = ComponentRegistrationFlags.Get(symbol);
                        if ((componentFlags & 1) == 0 && ComponentRegistrationFlags.HasInvalidDefault(symbol)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid component Default property: " + type + ". Expected a public static getter returning the component type."));
                            return;
                        }
                        foreach (var phase in new[] { (Flag: 2, Contract: "ME.BECS.IConfigComponentStatic"),
                                     (Flag: 8, Contract: "ME.BECS.IComponentShared"), (Flag: 32, Contract: "ME.BECS.IConfigInitialize") }) {
                            var phaseContract = input.Right.GetTypeByMetadataName(phase.Contract);
                            var implements = phaseContract != null && symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, phaseContract));
                            if (implements != ((componentFlags & phase.Flag) != 0)) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                    "Component phase flags differ from compiler symbols: " + type + " / " + phase.Contract));
                                return;
                            }
                        }
                        componentRegistrations.Add((type, symbol, componentFlags));
                    }
                    if (fields[0] == "aspect-construction" || fields[0] == "aspect-construction-auto") {
                        if (symbol == null || !aspectRegistrations.Any(a => SymbolEqualityComparer.Default.Equals(a.Type, symbol))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Aspect construction requires preceding registration: " + type));
                            return;
                        }
                        if (fields[0] == "aspect-construction-auto" && !symbol.GetMembers().OfType<IFieldSymbol>().Any(field =>
                                !field.IsStatic && field.Type.AllInterfaces.Any(contract => contract.ToDisplayString() == "ME.BECS.IAspectData"))) continue;
                        var catalog = symbol?.ContainingAssembly.GetTypeByMetadataName("ME.BECS.SourceGenerated.Catalog_" +
                            ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(symbol.ContainingAssembly.Name));
                        var constructorName = "ConstructAspect_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(
                            symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "");
                        var world = input.Right.GetTypeByMetadataName("ME.BECS.World");
                        var constructors = catalog?.GetMembers(constructorName).OfType<IMethodSymbol>().Where(m =>
                            m.IsStatic && m.Arity == 0 && m.ReturnsVoid && m.Parameters.Length == 1 &&
                            m.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, world) &&
                            input.Right.IsSymbolAccessibleWithin(m, input.Right.Assembly)).ToArray();
                        if (symbol == null || !aspectRegistrations.Any(a => SymbolEqualityComparer.Default.Equals(a.Type, symbol)) ||
                            constructors?.Length != 1) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid aspect construction: " + type + " (requires preceding registration and generated constructor)"));
                            return;
                        }
                        aspectConstructors.Add(constructors[0]);
                    }
                    if (fields[0] == "aspect-registration") {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.IAspect");
                        var catalog = symbol?.ContainingAssembly.GetTypeByMetadataName("ME.BECS.SourceGenerated.Catalog_" +
                            ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(symbol.ContainingAssembly.Name));
                        var queryName = "InitializeAspectQuery_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(
                            symbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "");
                        var queries = catalog?.GetMembers(queryName).OfType<IMethodSymbol>().Where(m =>
                            m.IsStatic && m.Arity == 0 && m.Parameters.Length == 0 && m.ReturnsVoid &&
                            input.Right.IsSymbolAccessibleWithin(m, input.Right.Assembly)).ToArray();
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) || contract == null ||
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) || queries?.Length != 1) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid aspect registration " + fields[1] + ": " + type +
                                " (requires closed unmanaged IAspect and a generated query initializer; rebuild its source catalog)"));
                            return;
                        }
                        aspectRegistrations.Add((symbol, queries[0]));
                    }
                    if (fields[0] == "system-registration") {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.ISystem");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) ||
                            contract == null || !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid system registration " + fields[1] + ": " + type + " (" + (resolutionGap ?? "requires accessible closed unmanaged ISystem") + ")"));
                            return;
                        }
                        systemRegistrations.Add((type, symbol));
                    }
                    if (fields[0] == "entity-registration") {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.IEntityType");
                        if (symbol == null || !symbol.IsUnmanagedType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) ||
                            contract == null || !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid entity registration " + fields[1] + ": " + type + " (" + (resolutionGap ?? "requires accessible closed unmanaged IEntityType") + ")"));
                            return;
                        }
                        if (entityRegistrations.Count > ushort.MaxValue) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Entity registration IDs exceed UInt16 capacity"));
                            return;
                        }
                        entityRegistrations.Add(symbol);
                    }
                    if (fields[0] == "destroy-registration") {
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.IComponentDestroy");
                        if (symbol == null || !symbol.IsUnmanagedType || symbol.IsRefLikeType || MethodSummaryType.From(symbol).IsOpen ||
                            !input.Right.IsSymbolAccessibleWithin(symbol, input.Right.Assembly) || contract == null ||
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid destroy component registration: " + type));
                            return;
                        }
                        destroyRegistrations.Add(symbol);
                    }
                    resolutions.Add(header[2] + "\t" + fields[0] + "\t" + fields[1] + "\t" +
                        (symbol == null ? "unresolved\t" + resolutionGap : "resolved\t" + symbol.GetDocumentationCommentId() + "\t" + MethodSummaryType.From(symbol).Encode()));
                }
                if (next.ContainsKey("aspect-construction") && next.ContainsKey("aspect-construction-auto")) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                        "Cannot mix Editor-selected and compiler-selected aspect constructors. Regenerate type inputs."));
                    return;
                }
                if (!destroySchema || !maskSchema || !collectionCountSchema || !collectionCallbackSchema) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing destroy/config registration schema. Regenerate type inputs with the current Editor exporter."));
                    return;
                }
                if (compilerCollectionCounts) {
                    if (collectionCounts.Count != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                            "Collection count schema v2 does not accept Editor-computed counts. Regenerate type inputs."));
                        return;
                    }
                    // Callback contracts were validated above. Use the same compiler symbols,
                    // so allocation counts cannot lag behind the fields compiled into callbacks.
                    foreach (var component in collectionCallbackTypes)
                        collectionCounts.Add((component, ConfigCollectionsInputEmitter.GetCollectionCount(component)));
                }
                if (!collectionCounts.Select(static entry => entry.Type).SequenceEqual(collectionCallbackTypes, SymbolEqualityComparer.Default)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Collection count and callback selections differ. Regenerate type inputs."));
                    return;
                }
                if (graphRegistrations.Any(g => graphSlots[g.Id].Count != g.Capacity)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Graph system slots do not match declared capacity. Regenerate inputs."));
                    return;
                }
                foreach (var topology in graphTopologies) {
                    if (!topology.Value.BindSlots(graphSlots[topology.Key].Select(static slot => slot.Type).ToArray(), resolver, out var bindingError)) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, bindingError + " in graph " + topology.Key));
                        return;
                    }
                }
                var systemAot = SystemAotInputEmitter.Create(input.Right, systemRegistrations, out var aotError);
                if (systemAot == null) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, aotError));
                    return;
                }
                var source = new StringBuilder("// <auto-generated/>\n");
                source.Append(systemAot.Metadata);
                viewTracker.AppendMetadata(source);
                var lifecyclePhases = new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" };
                var lifecyclePlans = new Dictionary<(int Graph, string Phase), GraphLifecyclePlan>();
                var flatQueries = input.Right.SyntaxTrees.Any(tree => tree.Options is Microsoft.CodeAnalysis.CSharp.CSharpParseOptions options &&
                    options.PreprocessorSymbolNames.Contains("ENABLE_BECS_FLAT_QUERIES"));
                foreach (var topology in graphTopologies.OrderBy(static pair => pair.Key)) {
                    for (var phaseIndex = 0; phaseIndex < lifecyclePhases.Length; ++phaseIndex) {
                        var phase = lifecyclePhases[phaseIndex];
                        var available = GraphLifecyclePlan.TryCreate(topology.Value, resolver, phase, phaseIndex + 1, flatQueries,
                            out var plan, out var planError);
                        if (!available) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Cannot generate graph lifecycle " + topology.Key.ToString(CultureInfo.InvariantCulture) + " / " + phase + ": " + planError));
                            return;
                        }
                        if (available) lifecyclePlans.Add((topology.Key, phase), plan!);
                        if (available)
                            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphSyncComparison.v1\", ")
                                .Append(SymbolDisplay.FormatLiteral(topology.Key.ToString(CultureInfo.InvariantCulture) + "\n" + phase + "\n" + plan!.SyncDifferences, true)).Append(")]\n");
                        var planPayload = topology.Key.ToString(CultureInfo.InvariantCulture) + "\n" + phase + "\n" +
                            (available ? plan!.Serialize() : "unavailable\n" + planError);
                        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphLifecyclePlan.v1\", ")
                            .Append(SymbolDisplay.FormatLiteral(planPayload, true)).Append(")]\n");
                    }
                }
                foreach (var graph in graphRegistrations) {
                    if (!graphApply.ContainsKey(graph.Id)) {
                        // Old exporters omitted plans based on setters in the loaded DLL.
                        // Let Editor reload corrected source, but never run an incomplete graph
                        // or allow it into a player. Regeneration must replace this stale input.
                        var editorCompilation = input.Right.SyntaxTrees.Any(tree => tree.Options is CSharpParseOptions options &&
                            options.PreprocessorSymbolNames.Contains("UNITY_EDITOR"));
                        output.ReportDiagnostic(Diagnostic.Create(editorCompilation ? StaleInjectionPlan : Invalid, Location.None,
                            MissingInjectionMessage(graph.Id)));
                        if (!editorCompilation) return;
                    }
                    foreach (var phase in lifecyclePhases) {
                        if (!lifecyclePlans.ContainsKey((graph.Id, phase))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Missing source lifecycle plan for registered graph " + graph.Id.ToString(CultureInfo.InvariantCulture) + " / " + phase));
                            return;
                        }
                        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphLifecycleExecution.v1\", ")
                            .Append(SymbolDisplay.FormatLiteral(graph.Id.ToString(CultureInfo.InvariantCulture) + "\n" + phase + "\nsource-plan", true)).Append(")]\n");
                    }
                }
                foreach (var topology in graphTopologies.OrderBy(static pair => pair.Key))
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphDependencyOrder.v2\", ")
                        .Append(SymbolDisplay.FormatLiteral(topology.Key.ToString(CultureInfo.InvariantCulture) + "\n" + GraphDependencyOrder.Analyze(topology.Value), true))
                        .Append(")]\n");
                foreach (var component in componentRegistrations)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.ComponentFlags.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(component.Identity + "\n" + component.Flags.ToString(CultureInfo.InvariantCulture), true)).Append(")]\n");
                foreach (var record in records)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInput.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(record, true)).Append(")]\n");
                source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInputProfile.v1\", ")
                    .Append(SymbolDisplay.FormatLiteral(header[2], true)).Append(")]\n");
                foreach (var resolution in resolutions)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInputResolution.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(resolution, true)).Append(")]\n");
                var bootstrapNamespace = header[2] == "editor" ? "ME.BECS.Editor" : "ME.BECS";
                var dependencyOwner = input.Right.Assembly.GetTypeByMetadataName("ME.BECS.Editor.StaticMethods");
                if (header[2] == "editor" && dependencyOwner?.GetMembers("SourceSystemDependenciesV1").Length > 0) {
                    if (!systemDependencies.HasSchema || dependencyOwner.GetMembers("InitializeSystemDependenciesInfo").Length != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing system dependency schema or mixed dependency templates. Regenerate bootstrap and inputs together."));
                        return;
                    }
                    systemDependencies.Append(source);
                }
                if (bootstrapKinds.Contains("jobs") && !jobBootstrap.HasSchema) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing ordered job EarlyInit schema. Regenerate bootstrap and inputs together."));
                    return;
                }
                if (jobBootstrap.HasSchema && !debugSchema) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Ordered job EarlyInit requires the debug/layout schema, including when collection checks are disabled."));
                    return;
                }
                if (!jobBootstrap.Append(source, bootstrapNamespace, debugPlans, jobWeights, jobEntityInitializers, out var bootstrapJobError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, bootstrapJobError));
                    return;
                }
                var bootstrapType = input.Right.Assembly.GetTypeByMetadataName(bootstrapNamespace + ".StaticTypesInitializer");
                if (bootstrapType?.GetMembers("SourceBootstrapPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (bootstrapFeeders.Count == 0 || bootstrapType.GetMembers("RegisterAdditionalTypes").Length != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing bootstrap feeder plan or mixed bootstrap templates. Regenerate active inputs."));
                        return;
                    }
                    var hooks = bootstrapFeeders.Select(identity => "InitializeFeeder_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity)).ToArray();
                    for (var hookIndex = 0; hookIndex < hooks.Length; ++hookIndex) {
                        var hook = hooks[hookIndex];
                        if (bootstrapKinds[hookIndex] != "legacy") {
                            if (bootstrapType.GetMembers(hook).Length != 0) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Compiler-owned bootstrap hook is still exported: " + hook + ". Regenerate bootstrap."));
                                return;
                            }
                            continue;
                        }
                        if (!bootstrapType.GetMembers(hook).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 0 && method.Arity == 0)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing bootstrap feeder hook " + hook + ". Regenerate active inputs and bootstrap together."));
                            return;
                        }
                    }
                    source.Append("namespace ").Append(bootstrapNamespace).Append(" { public static unsafe partial class StaticTypesInitializer {\n")
                        .Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\nprivate static void RegisterAdditionalTypes() {\n");
                    foreach (var hook in hooks) source.Append(hook).Append("();\n");
                    source.Append("}\n");
                    for (var hookIndex = 0; hookIndex < hooks.Length; ++hookIndex) {
                        if (bootstrapKinds[hookIndex] == "none") {
                            source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\nprivate static void ")
                                .Append(hooks[hookIndex]).Append("() { }\n");
                            continue;
                        }
                        var initializationTarget = BootstrapInitializationTarget(bootstrapKinds[hookIndex]);
                        if (initializationTarget == null) continue;
                        source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\nprivate static void ")
                            .Append(hooks[hookIndex]).Append("() { global::ME.BECS.SourceGenerated.")
                            .Append(initializationTarget).Append(".Initialize(); }\n");
                    }
                    source.Append("} }\n");
                }
                var registrationType = input.Right.Assembly.GetTypeByMetadataName(bootstrapNamespace + ".StaticMethods");
                if (registrationType?.GetMembers("SourceRegistrationPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (bootstrapFeeders.Count == 0 || registrationType.GetMembers("RegisterGeneratedMethods").Length != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing feeder registration plan or mixed bootstrap templates. Regenerate active inputs."));
                        return;
                    }
                    var hooks = bootstrapFeeders.Select(identity => "RegisterFeeder_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity)).ToArray();
                    for (var index = 0; index < hooks.Length; ++index) {
                        var hook = hooks[index];
                        if (registrationKinds[index] != "legacy") {
                            if (registrationType.GetMembers(hook).Length != 0) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Compiler-owned registration hook is still exported: " + hook + ". Regenerate bootstrap."));
                                return;
                            }
                            continue;
                        }
                        if (!registrationType.GetMembers(hook).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 0 && method.Arity == 0)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing feeder registration hook " + hook + ". Regenerate bootstrap and inputs together."));
                            return;
                        }
                    }
                    source.Append("namespace ").Append(bootstrapNamespace).Append(" { public static unsafe partial class StaticMethods {\n")
                        .Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\nprivate static void RegisterGeneratedMethods() {\n");
                    foreach (var hook in hooks) source.Append(hook).Append("();\n");
                    source.Append("}\n");
                    for (var index = 0; index < hooks.Length; ++index) {
                        var body = BootstrapRegistrationBody(registrationKinds[index]);
                        if (body == null) continue;
                        source.Append("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]\nprivate static void ")
                            .Append(hooks[index]).Append("() { ").Append(body).Append(" }\n");
                    }
                    source.Append("} }\n");
                }
                var debugType = input.Right.Assembly.GetTypeByMetadataName(bootstrapNamespace + ".DebugJobs");
                if (debugType?.GetMembers("SourceDebugPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (!debugSchema || debugType.GetMembers("InitializeJobsDebug").Length != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing debug job schema or mixed debug templates. Regenerate bootstrap and inputs together."));
                        return;
                    }
                    DebugJobInputEmitter.Append(source, bootstrapNamespace, debugPlans);
                }
                JobLayoutInputEmitter.Append(source, input.Right, debugPlans);
                jobWeights.Append(source);
                if (!jobEntityInitializers.Append(source, entityRegistrations, out var entityInitializerError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, entityInitializerError));
                    return;
                }
                DestroyInputEmitter.Append(source, destroyRegistrations);
                DestroyInputEmitter.EmitCatalog(output, input.Right, destroyRegistrations);
                ConfigMaskInputEmitter.Append(source, maskRegistrations);
                ConfigCollectionsInputEmitter.Append(source, collectionCallbackBodies);
                source.Append("namespace ME.BECS.SourceGenerated { internal static class ConfigCollectionCounts { public static void Initialize() {\n")
                    .Append("global::ME.BECS.StaticTypes.collectionsCount.Resize(global::ME.BECS.StaticTypes.counter + 1u);\n");
                foreach (var entry in collectionCounts)
                    source.Append("global::ME.BECS.StaticTypes<").Append(entry.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">.SetCollectionsCount(").Append(entry.Count.ToString(CultureInfo.InvariantCulture)).Append("u);\n");
                source.Append("} } }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class EntityInputs {\npublic const uint GroupCount = ")
                    .Append(entityRegistrations.Count.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                for (var entityId = 0; entityId < entityRegistrations.Count; ++entityId) {
                    var entity = entityRegistrations[entityId];
                    var identity = entity.ContainingAssembly.Identity + "\t" + entity.GetDocumentationCommentId();
                    source.Append("public const uint Id_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity))
                        .Append(" = ").Append(entityId.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                }
                source.Append("public static void Initialize() {\nglobal::ME.BECS.EntityTypes.Init();\n");
                for (var entityId = 0; entityId < entityRegistrations.Count; ++entityId)
                    source.Append("global::ME.BECS.EntityTypes.Register<")
                        .Append(entityRegistrations[entityId].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">(").Append(entityId.ToString(CultureInfo.InvariantCulture)).Append(");\n");
                source.Append("global::ME.BECS.EntityTypes.groupsCount = ")
                    .Append(entityRegistrations.Count.ToString(CultureInfo.InvariantCulture)).Append("u;\n} } }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static unsafe class GraphInputs {\n");
                if (header[2] == "runtime") source
                    .Append("[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]\n")
                    .Append("private static void Initialize() => global::ME.BECS.CustomModules.RegisterFirstPass(Register);\n");
                source.Append("[global::UnityEngine.Scripting.Preserve]\npublic static void Register() {\n");
                foreach (var graph in graphRegistrations) {
                    var suffix = Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture) + "_SystemsCodeGenerator";
                    var id = graph.Id.ToString(CultureInfo.InvariantCulture);
                    foreach (var phase in new[] { (Register: "RegisterMethod", Class: "Initialize", Method: "GraphInitialize"),
                                 (Register: "RegisterAwakeMethod", Class: "Awake", Method: "GraphOnAwake"),
                                 (Register: "RegisterStartMethod", Class: "Start", Method: "GraphOnStart"),
                                 (Register: "RegisterUpdateMethod", Class: "Update", Method: "GraphOnUpdate"),
                                 (Register: "RegisterDrawGizmosMethod", Class: "DrawGizmos", Method: "GraphOnDrawGizmos"),
                                 (Register: "RegisterDestroyMethod", Class: "Destroy", Method: "GraphOnDestroy"),
                                 (Register: "RegisterGetSystemMethod", Class: "Initialize", Method: "GraphGetSystem") })
                        source.Append("global::ME.BECS.SystemsStatic.").Append(phase.Register).Append("(global::")
                            .Append(graph.Prefix).Append(phase.Class).Append('.').Append(phase.Method).Append('_').Append(suffix)
                            .Append(", ").Append(id).Append(", false);\n");
                }
                source.Append("} } }\n");
                foreach (var graph in graphRegistrations) {
                    var split = graph.Prefix.LastIndexOf('.');
                    var className = graph.Prefix.Substring(split + 1) + "Initialize";
                    var suffix = Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture) + "_SystemsCodeGenerator";
                    var storage = "graphNodes" + suffix;
                    foreach (var phase in new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" }) {
                        source.Append("namespace ").Append(graph.Prefix.Substring(0, split)).Append(" { public static unsafe partial class ")
                            .Append(graph.Prefix.Substring(split + 1)).Append(phase).Append(" {\n")
                            .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.On").Append(phase).Append("))]\n")
                            .Append("public static void GraphOn").Append(phase).Append('_').Append(suffix)
                            .Append("(uint dt, ref global::ME.BECS.World world, ref global::Unity.Jobs.JobHandle dependsOn) => PlannedLifecycle.Execute(dt, ref world, ref dependsOn);\n");
                        var contract = input.Right.GetTypeByMetadataName("ME.BECS.I" + phase);
                        var phaseSlots = graphSlots[graph.Id];
                        for (var slot = 0; slot < phaseSlots.Count; ++slot) {
                            var system = phaseSlots[slot].Type;
                            if (contract == null || !system.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract))) continue;
                            var systemName = system.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            source.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n")
                                .Append("private static void InvokeSystem_").Append(slot.ToString(CultureInfo.InvariantCulture))
                                .Append("(global::System.IntPtr pointer, ref global::ME.BECS.SystemContext context) => global::ME.BECS.SourceGeneratorSystemCalls.")
                                .Append(phase).Append('<').Append(systemName).Append(">(ref *((").Append(systemName).Append("*)pointer), ref context);\n");
                        }
                        if (graphTopologies.TryGetValue(graph.Id, out var topology)) {
                            var groups = new HashSet<(int Start, int Count, bool Parallel)>();
                            foreach (var node in topology.Occurrences.SelectMany(static occurrence => occurrence.Nodes)) {
                                if (node.SystemType.Length == 0) continue;
                                var definition = resolver.ResolveDefinition(node.SystemType, out _);
                                if (definition == null || !definition.IsGenericType || contract == null ||
                                    !definition.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) ||
                                    !groups.Add((node.SlotStart, node.SlotCount, node.Parallel))) continue;
                                source.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n")
                                    .Append("private static global::Unity.Jobs.JobHandle Invoke").Append(node.Parallel ? "Parallel_" : "Sequential_").Append(node.SlotStart.ToString(CultureInfo.InvariantCulture))
                                    .Append('_').Append(node.SlotCount.ToString(CultureInfo.InvariantCulture))
                                    .Append("(uint dt, in global::ME.BECS.World world, global::Unity.Jobs.JobHandle dependsOn, global::System.IntPtr* systems")
                                    .Append(node.Parallel ? ") {\n" : ", bool apply) {\n");
                                if (node.Parallel && node.SlotCount > 0)
                                    source.Append("var results = new global::Unity.Collections.NativeArray<global::Unity.Jobs.JobHandle>(")
                                        .Append(node.SlotCount.ToString(CultureInfo.InvariantCulture)).Append(", global::ME.BECS.Constants.ALLOCATOR_TEMP);\n");
                                for (var slot = node.SlotStart; slot < node.SlotStart + node.SlotCount; ++slot) {
                                    source.Append("{ var context = global::ME.BECS.SystemContext.Create(dt, in world, dependsOn);\nInvokeSystem_")
                                        .Append(slot.ToString(CultureInfo.InvariantCulture)).Append("(systems[").Append(slot.ToString(CultureInfo.InvariantCulture))
                                        .Append("], ref context);\n");
                                    if (node.Parallel) source.Append("results[").Append((slot - node.SlotStart).ToString(CultureInfo.InvariantCulture)).Append("] = context.dependsOn; }\n");
                                    else source.Append("dependsOn = apply ? global::ME.BECS.Batches.Apply(context.dependsOn, in world) : context.dependsOn; }\n");
                                }
                                if (node.Parallel && node.SlotCount > 0)
                                    source.Append("dependsOn = global::Unity.Jobs.JobHandle.CombineDependencies(results);\n");
                                source.Append("return dependsOn;\n}\n");
                            }
                        }
                        if (lifecyclePlans.TryGetValue((graph.Id, phase), out var lifecyclePlan))
                            GraphLifecycleEmitter.Append(source, lifecyclePlan, "global::" + graph.Prefix + "Initialize." + storage);
                        source.Append("} }\n");
                    }
                    source.Append("namespace ").Append(graph.Prefix.Substring(0, split)).Append(" { using Unity.Collections; public unsafe partial class ")
                        .Append(className).Append(" {\nprivate static readonly global::Unity.Burst.SharedStatic<global::Unity.Collections.NativeArray<global::System.IntPtr>> ")
                        .Append(storage).Append("Data = global::Unity.Burst.SharedStatic<global::Unity.Collections.NativeArray<global::System.IntPtr>>.GetOrCreate<")
                        .Append(className).Append(">();\npublic static ref global::Unity.Collections.NativeArray<global::System.IntPtr> ")
                        .Append(storage).Append(" => ref ").Append(storage).Append("Data.Data;\n")
                        .Append("private static void ResetStorage(global::Unity.Collections.AllocatorManager.AllocatorHandle allocator) {\nif (")
                        .Append(storage).Append(".IsCreated) global::ME.BECS.Utils.DisposePtrArray(").Append(storage).Append(", allocator);\n")
                        .Append(storage).Append(" = global::Unity.Collections.CollectionHelper.CreateNativeArray<global::System.IntPtr>(")
                        .Append(graph.Capacity.ToString(CultureInfo.InvariantCulture)).Append(", allocator);\n}\n")
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.InitializeGraph))]\npublic static void GraphInitialize_")
                        .Append(suffix).Append("() {\nValidateInjectionPlan();\nvar allocator = (global::Unity.Collections.AllocatorManager.AllocatorHandle)global::ME.BECS.Constants.ALLOCATOR_DOMAIN;\n")
                        .Append("ResetStorage(allocator);\n");
                    var slots = graphSlots[graph.Id];
                    for (var slotIndex = 0; slotIndex < slots.Count; ++slotIndex) {
                        var slot = slots[slotIndex];
                        var typeName = slot.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var indexText = slotIndex.ToString(CultureInfo.InvariantCulture);
                        source.Append("{ var item = allocator.Allocate(global::ME.BECS.TSize<").Append(typeName)
                            .Append(">.sizeInt, global::ME.BECS.TAlign<").Append(typeName).Append(">.alignInt);\n*(")
                            .Append(typeName).Append("*)item = ").Append(slot.UseDefault ? "default" :
                                "(" + typeName + ")((global::ME.BECS.FeaturesGraph.Nodes.SystemNode)global::ME.BECS.ObjectReferenceRegistry.GetObjectBySourceId<global::ME.BECS.FeaturesGraph.SystemsGraph>(" +
                                slot.SourceId.ToString(CultureInfo.InvariantCulture) + "u).nodes[" + slot.NodeIndex.ToString(CultureInfo.InvariantCulture) + "]).system")
                            .Append(";\nglobal::ME.BECS.TSystemGraph.Register<").Append(typeName).Append(">(")
                            .Append(Math.Abs(graph.Id).ToString(CultureInfo.InvariantCulture)).Append(", item);\n")
                            .Append(storage).Append('[').Append(indexText).Append("] = (global::System.IntPtr)item; }\n");
                    }
                    source.Append("ApplyInjections();\n}\n");
                    source.Append("private static void ValidateInjectionPlan() {");
                    if (!graphApply.ContainsKey(graph.Id)) source.Append("throw new global::System.InvalidOperationException(")
                        .Append(SymbolDisplay.FormatLiteral(MissingInjectionMessage(graph.Id), true)).Append(");");
                    source.Append("}\n");
                    if (graphJobs.TryGetValue(graph.Id, out var patches))
                        foreach (var patch in patches) patch.Append(source, storage);
                    if (graphInjections.TryGetValue(graph.Id, out var assignments))
                        foreach (var assignment in assignments) assignment.Plan.AppendSystem(source, storage, assignment.Slot);
                    if (graphApply.TryGetValue(graph.Id, out var actions)) {
                        source.Append("private static void ApplyInjections() {\n");
                        foreach (var action in actions) source.Append(action[0] == 's' ? "InjectSystem_" : action[0] == 'j' ? "RegisterJob_" : "global::ME.BECS.SourceGenerated.GenericJobDeltaInputs.Register_")
                            .Append(action.Substring(2)).Append("();\n");
                        source.Append("}\n");
                    } else {
                        source.Append("private static void ApplyInjections() => ValidateInjectionPlan();\n");
                    }
                    source
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.SystemsStatic.GetSystem))]\npublic static void GraphGetSystem_")
                        .Append(suffix).Append("(int index, out void* ptr) { ptr = (void*)").Append(storage).Append("[index]; }\n} }\n");
                }
                source.Append("namespace ME.BECS.SourceGenerated { internal static class SystemInputs {\n");
                foreach (var registration in systemRegistrations)
                    source.Append("public static void Register_")
                        .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity))
                        .Append("() => global::ME.BECS.StaticSystemTypes<")
                        .Append(registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">.Validate();\n");
                source.Append("} }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static unsafe class GenericJobDeltaInputs {\n");
                foreach (var job in deltaJobs) {
                    var name = job.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    source.Append("public static void Register_").Append(job.Key).Append("() => global::ME.BECS.JobInject<").Append(name)
                        .Append(">.Register(Patch_").Append(job.Key).Append(");\n")
                        .Append("[global::AOT.MonoPInvokeCallback(typeof(global::ME.BECS.JobPatchInjectDelegate.PatchDelegate))]\n[global::Unity.Burst.BurstCompile]\n")
                        .Append("public static void Patch_").Append(job.Key).Append("(void* jobPtr, ushort worldId) { var job = (").Append(name).Append("*)jobPtr;\n");
                    foreach (var field in job.Fields) {
                        if (field.DeclaredAccessibility != Accessibility.Public) {
                            source.Append(name).Append('.').Append(DeltaSetterName(field)).Append("(ref *job, worldId);\n");
                            continue;
                        }
                        source.Append("{ var dtMs = global::ME.BECS.Worlds.GetWorldDeltaTime(worldId);\nvar context = global::ME.BECS.SystemContext.Create(dtMs, default, default);\njob->@")
                            .Append(field.Name).Append(" = context.").Append(field.Type.SpecialType == SpecialType.System_UInt32 ? "deltaTimeMs" : "deltaTime").Append("; }\n");
                    }
                    source.Append("}\n");
                }
                source.Append("} }\nnamespace ME.BECS.SourceGenerated { internal static unsafe class SystemInjectionInputs {\n");
                foreach (var injection in systemInjections) {
                    var targetTypeName = injection.Target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    source.Append("public static void Inject_").Append(injection.Key).Append("(ref ")
                        .Append(injection.Owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(" system, void* target) { system.@")
                        .Append(injection.Field.Name).Append(" = global::ME.BECS.InjectSystem<").Append(targetTypeName)
                        .Append(">.FromPointer(target); }\n");
                }
                source.Append("} }\n");
                // Preserve the historical registration passes, not per-component grouping:
                // shared/static validation has its own counters and must follow all normal IDs.
                source.Append("namespace ME.BECS.SourceGenerated { internal static class CoreTypeInputs { public static void Initialize() {\n");
                foreach (var registration in systemRegistrations)
                    source.Append("SystemInputs.Register_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                source.Append("GroupInputs.Initialize();\n");
                foreach (var registration in componentRegistrations)
                    source.Append("ComponentInputs.Register_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 8) != 0))
                    source.Append("ComponentInputs.RegisterShared_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 2) != 0))
                    source.Append("ComponentInputs.RegisterStatic_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 32) != 0))
                    source.Append("ComponentInputs.RegisterConfig_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                source.Append("}\n[global::UnityEngine.Scripting.PreserveAttribute] public static void AotComponents() {\n");
                foreach (var registration in componentRegistrations)
                    source.Append("ComponentInputs.Aot_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 8) != 0))
                    source.Append("ComponentInputs.AotShared_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 2) != 0))
                    source.Append("ComponentInputs.AotStatic_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                foreach (var registration in componentRegistrations.Where(item => (item.Flags & 32) != 0))
                    source.Append("ComponentInputs.AotConfig_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity)).Append("();\n");
                source.Append("} } }\n");
                source.Append(systemAot.Body);
                viewTracker.Append(source);
                source.Append("namespace ME.BECS.SourceGenerated { internal static class ComponentInputs {\n");
                foreach (var registration in componentRegistrations) {
                    var name = registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var key = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity);
                    source.Append("public static uint Size_").Append(key)
                        .Append("() => (uint)global::Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<")
                        .Append(name).Append(">();\n");
                    source.Append("public static void Register_").Append(key).Append("() { global::ME.BECS.StaticTypes<")
                        .Append(name).Append(">.Validate(isTag: ").Append((registration.Flags & 1) != 0 ? "true" : "false")
                        .Append(", isStatic: ").Append((registration.Flags & 2) != 0 ? "true" : "false").Append(");\n");
                    if ((registration.Flags & 4) != 0) source.Append("global::ME.BECS.StaticTypes<").Append(name)
                        .Append(">.SetDefaultValue(").Append(name).Append(".Default);\n");
                    source.Append("}\npublic static void Aot_").Append(key).Append("() => global::ME.BECS.StaticTypes<")
                        .Append(name).Append(">.AOT();\n");
                    if ((registration.Flags & 8) != 0) {
                        source.Append("public static void RegisterShared_").Append(key).Append("() => global::ME.BECS.StaticTypes<")
                            .Append(name).Append(">.ValidateShared(isTag: ").Append((registration.Flags & 1) != 0 ? "true" : "false")
                            .Append(", hasCustomHash: ").Append((registration.Flags & 16) != 0 ? "true" : "false").Append(");\n")
                            .Append("public static void AotShared_").Append(key).Append("() => global::ME.BECS.StaticTypesShared<")
                            .Append(name).Append(">.AOT();\n");
                    }
                    if ((registration.Flags & 2) != 0) {
                        source.Append("public static void RegisterStatic_").Append(key).Append("() => global::ME.BECS.StaticTypes<")
                            .Append(name).Append(">.ValidateStatic(isTag: ").Append((registration.Flags & 1) != 0 ? "true" : "false").Append(");\n")
                            .Append("public static void AotStatic_").Append(key).Append("() => global::ME.BECS.StaticTypesStatic<")
                            .Append(name).Append(">.AOT();\n");
                    }
                    if ((registration.Flags & 32) != 0) {
                        source.Append("public static void RegisterConfig_").Append(key).Append("() => global::ME.BECS.StaticTypes<")
                            .Append(name).Append(">.Validate(isTag: ").Append((registration.Flags & 1) != 0 ? "true" : "false")
                            .Append(", isStatic: ").Append((registration.Flags & 2) != 0 ? "true" : "false").Append(");\n")
                            .Append("public static void AotConfig_").Append(key).Append("() => global::ME.BECS.ConfigInitializeTypes<")
                            .Append(name).Append(">.AOT();\n");
                    }
                }
                source.Append("} }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class GroupInputs { public static void Initialize() {\n");
                foreach (var registration in groupRegistrations)
                    source.Append("global::ME.BECS.StaticTypes<")
                        .Append(registration.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">.ApplyGroup(typeof(")
                        .Append(registration.Group.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("));\n");
                source.Append("}\npublic static global::System.Type[] GetComponents() => new global::System.Type[] { ")
                    .Append(string.Join(",", groupRegistrations.Select(registration => "typeof(" + registration.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append(" };\npublic static global::System.Type[] GetGroups() => new global::System.Type[] { ")
                    .Append(string.Join(",", groupRegistrations.Select(registration => "typeof(" + registration.Group.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append(" };\n} }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class AspectInputs { public static void Initialize() {\n");
                foreach (var registration in aspectRegistrations) {
                    source.Append("global::ME.BECS.AspectTypeInfo<")
                        .Append(registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">.Validate();\n")
                        .Append(registration.Query.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append('.').Append(registration.Query.Name).Append("();\n");
                }
                source.Append("}\npublic static void RegisterConstruction() => global::ME.BECS.WorldStaticCallbacks.RegisterCallback<global::ME.BECS.World>(Construct);\n")
                    .Append("public static void Construct(ref global::ME.BECS.World world) {\n");
                foreach (var constructor in aspectConstructors)
                    source.Append(constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append('.').Append(constructor.Name).Append("(ref world);\n");
                source.Append("} } }\n");
                selected = source.ToString();
            }
            if (selected != null) output.AddSource("ME.BECS.TypeInputs.g.cs", SourceText.From(selected, Encoding.UTF8));
        });
    }

    internal static string DeltaSetterName(IFieldSymbol field) =>
        "__BecsInjectDelta_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(field.Name);

    private static string MissingInjectionMessage(int graphId) =>
        "Missing source injection plan for registered graph " + graphId.ToString(CultureInfo.InvariantCulture) +
        ". The input manifest is stale or injection fields are unsupported. After Editor compilation/reload, regenerate codegen inputs. " +
        "Graph initialization is disabled until its complete injection plan is exported. Comparison reports describe loaded assemblies, not pending source edits.";

    internal static bool HasDeltaSetter(INamedTypeSymbol owner, IFieldSymbol field, Compilation compilation) {
        var marker = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        return owner.GetMembers(DeltaSetterName(field)).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.Arity == 0 && method.ReturnsVoid && method.Parameters.Length == 2 &&
            method.DeclaredAccessibility == Accessibility.Public && compilation.IsSymbolAccessibleWithin(method, compilation.Assembly) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, owner) &&
            method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_UInt16 &&
            method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker)));
    }

    private static string? BootstrapRegistrationBody(string kind) => kind switch {
        "none" => "",
        "aspect-construction" => "global::ME.BECS.SourceGenerated.AspectInputs.RegisterConstruction();",
        "config-callbacks" => "global::ME.BECS.SourceGenerated.ConfigMaskInputs.Initialize(); global::ME.BECS.SourceGenerated.ConfigCollectionsInputs.Initialize();",
        "destroy-callbacks" => "global::ME.BECS.SourceGenerated.DestroyInputs.Initialize();",
        _ => null,
    };

    private static string? BootstrapInitializationTarget(string kind) => kind switch {
        "aspects" => "AspectInputs",
        "entities" => "EntityInputs",
        "config-counts" => "ConfigCollectionCounts",
        "views" => "ViewTrackerInputs",
        "jobs" => "JobBootstrapInputs",
        _ => null,
    };

    private static string Decode(string value) {
        var bytes = Convert.FromBase64String(value);
        if (Convert.ToBase64String(bytes) != value) throw new FormatException();
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { throw new FormatException(); }
    }
}
