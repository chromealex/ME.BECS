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
    private static readonly DiagnosticDescriptor StaleViewTracker = new DiagnosticDescriptor("BECSG102",
        "Ignored view tracker inputs require regeneration", "{0}", "ME.BECS", DiagnosticSeverity.Warning, true);
    private static readonly DiagnosticDescriptor StaleComponentFlags = new DiagnosticDescriptor("BECSG103",
        "Component classification inputs require regeneration", "{0}", "ME.BECS", DiagnosticSeverity.Warning, true);
    private static readonly DiagnosticDescriptor MissingEditorInputs = new DiagnosticDescriptor("BECSG104",
        "Editor bootstrap inputs require regeneration", "{0}", "ME.BECS", DiagnosticSeverity.Warning, true);
    internal static readonly DiagnosticDescriptor Recovery = new DiagnosticDescriptor("BECSG105",
        "BECS inputs await automatic recovery", "{0}", "ME.BECS", DiagnosticSeverity.Warning, true);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var manifests = context.AdditionalTextsProvider.Where(static file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsInput(file.Path))
            .Combine(context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? ""))
            // Native files reach all analyzer users. Do not load/split a project
            // manifest in unrelated owner assemblies.
            .Where(static input => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.TargetsCompilation(input.Left.Path, input.Right))
            .Select(static (input, cancellation) => (input.Left.Path, Content: input.Left.GetText(cancellation)?.ToString())).Collect();
        context.RegisterSourceOutput(manifests.Combine(context.CompilationProvider), static (contextOutput, input) => {
            using var output = new InputRecoveryOutput(contextOutput, input.Right);
            string? selected = null;
            var selectedCompilerBootstrap = false;
            var files = input.Left.AsEnumerable();
            // Project-owned native inputs supersede old framework/rsp snapshots,
            // including on failure: never run a stale registration selection.
            if (files.Any(file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsNative(file.Path)))
                files = files.Where(file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsNative(file.Path));
            else if (files.Any(file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsScoped(file.Path)))
                files = files.Where(file => ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsScoped(file.Path));
            foreach (var file in files.OrderBy(f => f.Path, StringComparer.Ordinal)) {
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
                if ((target == "ME.BECS.Gen.Editor" && header[2] != "editor") ||
                    (target == "ME.BECS.Gen.Runtime" && header[2] != "runtime")) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                        "BECS input profile does not match target assembly " + target + ": " + header[2]));
                    return;
                }
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
                var kinds = new HashSet<string>(new[] { "system", "system-registration", "component", "component-group", "job", "entity", "entity-registration", "aspect", "aspect-registration", "aspect-construction", "aspect-construction-auto", "destroy-schema", "destroy-registration", "config-mask-schema", "config-collection-count-schema", "config-collection-callback-schema", "graph-system-injection-schema", "graph-injection-schema" }, StringComparer.Ordinal);
                var next = new Dictionary<string, int>(StringComparer.Ordinal);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                var records = new List<string>();
                var bootstrapFeeders = new List<string>();
                var bootstrapKinds = new List<string>();
                var registrationKinds = new List<string>();
                var compilerBootstrap = false;
                var sourceOnlyBootstrap = false;
                string? graphInputSnapshot = null;
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
                var systemOwners = new SystemRegistrationOwners();
                var typeOwners = new TypeRegistrationOwners();
                var entityOwners = new EntityRegistrationOwners();
                var destroyOwners = new DestroyRegistrationOwners();
                var configOwners = new ConfigRegistrationOwners();
                var aspectOwners = new AspectRegistrationOwners();
                var aspectConstructionSelection = new List<INamedTypeSymbol>();
                var aspectRegistrations = new List<(INamedTypeSymbol Type, IMethodSymbol? Query)>();
                var aspectConstructors = new List<IMethodSymbol>();
                var componentRegistrations = new List<(string Identity, INamedTypeSymbol Type, int Flags)>();
                var groupRegistrations = new List<(INamedTypeSymbol Component, INamedTypeSymbol Group)>();
                var deltaJobs = new List<(string Key, INamedTypeSymbol Type, IFieldSymbol[] Fields)>();
                var graphRegistrations = new List<(string Prefix, int Id, int Capacity)>();
                var graphIds = new HashSet<int>();
                var graphJobs = new Dictionary<int, List<GraphJobPatchPlan>>();
                var graphInjections = new Dictionary<int, List<(GraphJobPatchPlan Plan, int Slot)>>();
                var compilerSystemInjections = false;
                var compilerJobInjections = false;
                var graphJobSelections = new Dictionary<int, List<GraphJobSelectionInput>>();
                var graphJobInjectionPlans = new Dictionary<int, List<GraphJobPatchPlan>>();
                var graphApply = new Dictionary<int, string[]>();
                var graphTopologies = new Dictionary<int, GraphTopologyInput>();
                var graphSlots = new Dictionary<int, List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex, string Identity)>>();
                var resolver = new InputManifestTypes(input.Right, output.CancellationToken);
                var componentLayouts = new ComponentLayoutReader(input.Right, output.CancellationToken);
                var viewTracker = new ViewTrackerInputEmitter();
                var viewTypes = new ViewTypeInputEmitter(input.Right, output.CancellationToken);
                var networkMethods = new NetworkMethodInputEmitter();
                var networkOwners = new NetworkRegistrationOwners();
                var viewsOwners = new ViewsRegistrationOwners();
                var themeMenu = new ThemeMenuInputEmitter();
                var debugPlans = new List<DebugJobInputPlan>();
                var jobWeights = new JobWeightInputEmitter();
                var jobEntityInitializers = new JobEntityInputEmitter();
                var jobBootstrap = new JobBootstrapInputEmitter();
                var jobInitOwners = new JobInitRegistrationOwners();
                var jobSetupOwners = new JobSetupRegistrationOwners();
                var jobDebugOwners = new JobDebugRegistrationOwners();
                var graphOwners = new GraphRegistrationOwners();
                var systemDependencies = new SystemDependencyInputEmitter();
                var debugSchema = false;
                for (var i = 1; i < footerIndex; ++i) {
                    output.CancellationToken.ThrowIfCancellationRequested();
                    var fields = lines[i].Split('\t');
                    if (fields[0] == "graph-publication-schema" || fields[0] == "graph-registration-owner") {
                        if (!graphOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph publication owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "jobdebug-publication-schema" || fields[0] == "jobdebug-registration-owner") {
                        if (!jobDebugOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job debug owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "jobsetup-publication-schema" || fields[0] == "jobsetup-registration-owner") {
                        if (!jobSetupOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job statistics owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "config-publication-schema" || fields[0] == "config-registration-owner") {
                        if (!configOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid config registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "destroy-publication-schema" || fields[0] == "destroy-registration-owner") {
                        if (!destroyOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid destroy registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "aspect-publication-schema" || fields[0] == "aspect-registration-owner") {
                        if (!aspectOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid aspect registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "entity-publication-schema" || fields[0] == "entity-registration-owner") {
                        if (!entityOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid entity registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "type-publication-schema" || fields[0] == "type-registration-owner") {
                        if (!typeOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid component/group registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "system-registration-owners-schema" || fields[0] == "system-registration-owner" || fields[0] == "system-publication-schema") {
                        if (!systemOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid system registration owner at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "graph-input-snapshot") {
                        string snapshot;
                        try { snapshot = fields.Length == 3 ? Decode(fields[2]) : ""; }
                        catch (FormatException) { snapshot = ""; }
                        if (graphInputSnapshot != null || fields.Length != 3 || fields[1] != "0" || snapshot.Length != 64 ||
                            snapshot.Any(character => !(character >= '0' && character <= '9' || character >= 'A' && character <= 'F'))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph input snapshot at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        graphInputSnapshot = snapshot;
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "bootstrap-schema") {
                        if (compilerBootstrap || fields.Length != 3 || fields[1] != "0" || (fields[2] != "djE=" && fields[2] != "djI=") ||
                            (target != "ME.BECS.Gen.Editor" && target != "ME.BECS.Gen.Runtime")) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid compiler bootstrap schema at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        compilerBootstrap = true;
                        sourceOnlyBootstrap = fields[2] == "djI=";
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "system-dependencies-schema" || fields[0] == "system-dependencies") {
                        var dependencyError = "System dependency plans are Editor-only";
                        if (header[2] != "editor" || !systemDependencies.Read(fields, resolver, input.Right, out dependencyError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, dependencyError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "jobinit-publication-schema" || fields[0] == "jobinit-registration-owner") {
                        if (!jobInitOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid job initialization owner at " + file.Path + ":" + (i + 1)));
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
                    if (fields[0] == "job-entity-il") {
                        if (!jobEntityInitializers.ReadIL(fields, resolver, input.Right, out var ilEntityError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, ilEntityError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-entity-fallback") {
                        if (!jobEntityInitializers.ReadFallback(fields, resolver, input.Right, out var fallbackError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, fallbackError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] == "job-entity-initializer") {
                        if (!jobEntityInitializers.Read(fields, resolver, input.Right, out var entityError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, entityError + " at " + file.Path + ":" + (i + 1)));
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
                        catch (FormatException) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid debug job plan: payload is not valid base64 at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        if (fields.Length != 3 || fields[1] != debugPlans.Count.ToString(CultureInfo.InvariantCulture)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid debug job plan: expected 3 fields and ordinal " + debugPlans.Count.ToString(CultureInfo.InvariantCulture) +
                                ", got " + fields.Length.ToString(CultureInfo.InvariantCulture) + " fields and ordinal '" +
                                (fields.Length > 1 ? fields[1] : "<missing>") + "' at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        if (!DebugJobInputPlan.TryRead(debugPayload, resolver, input.Right, out var debugPlan, out var debugError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, debugError + " at " + file.Path + ":" + (i + 1)));
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
                    if (ThemeMenuInputEmitter.IsRecord(fields[0])) {
                        if (!themeMenu.Read(fields, header[2] == "editor", out var themeError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, themeError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (ViewTypeInputEmitter.IsRecord(fields[0])) {
                        if (!viewTypes.Read(fields, resolver, input.Right, out var viewTypeError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, viewTypeError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] is "network-publication-schema" or "network-registration-owner") {
                        if (!networkOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid network publication selection"));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (fields[0] is "views-publication-schema" or "views-registration-owner") {
                        if (!viewsOwners.Read(fields)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid views publication selection"));
                            return;
                        }
                        records.Add(header[2] + "\t" + lines[i]);
                        continue;
                    }
                    if (NetworkMethodInputEmitter.IsRecord(fields[0])) {
                        if (!networkMethods.Read(fields, resolver, input.Right, out var networkError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, networkError + " at " + file.Path + ":" + (i + 1)));
                            return;
                        }
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
                    var automaticSystemInjection = fields[0] == "graph-system-injection-auto";
                    var graphInjectionRecord = fields[0] == "graph-system-injection" || automaticSystemInjection;
                    var graphApplyRecord = fields[0] == "graph-apply";
                    var topologyRecord = fields[0] == "graph-topology";
                    var jobSelectionRecord = fields[0] == "graph-job-selection";
                    var graphJobRecord = fields[0] == "graph-job" || graphInjectionRecord || graphApplyRecord || topologyRecord || jobSelectionRecord;
                    var slotIndex = 0;
                    uint slotSourceId = 0;
                    var slotNodeIndex = 0;
                    var graphId = 0;
                    var graphCapacity = 0;
                    var componentFlags = 0;
                    var valid = graphJobRecord ? fields.Length == (automaticSystemInjection ? 4 : 5) && header[2] == "runtime" &&
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
                            if (graphJobRecord && !automaticSystemInjection) {
                                groupIdentity = Decode(fields[4]);
                                valid = (graphApplyRecord || jobSelectionRecord || groupIdentity.Length > 0) &&
                                    (topologyRecord || jobSelectionRecord || !groupIdentity.Any(char.IsControl));
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
                    // Retired graph-independent helpers had no remaining consumers.
                    // Preserve old transport records for diagnostics, but never let
                    // their stale field names select or block graph initialization.
                    if (injectionRecord) continue;
                    if (fields[0] == "graph-system-injection-schema" || fields[0] == "graph-injection-schema") {
                        if (header[2] != "runtime" || type != "v1" || compilerSystemInjections) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph system injection schema"));
                            return;
                        }
                        compilerSystemInjections = true;
                        compilerJobInjections = fields[0] == "graph-injection-schema";
                        continue;
                    }
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
                            // System plans are derived from the complete slot table below.
                            if (action.StartsWith("s:", StringComparison.Ordinal)) return false;
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
                        graphSlots.Add(graphId, new List<(INamedTypeSymbol Type, bool UseDefault, uint SourceId, int NodeIndex, string Identity)>());
                        continue;
                    }
                    var symbol = resolver.ResolveDefinition(type, out var resolutionGap);
                    if (jobSelectionRecord) {
                        var selection = GraphJobSelectionInput.Create(symbol, groupIdentity, resolver);
                        if (selection == null || !graphSlots.TryGetValue(graphId, out var selectedSlots) ||
                            !selectedSlots.Any(slot => SymbolEqualityComparer.Default.Equals(slot.Type, symbol))) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph job selection for " + type + " in graph " + graphId));
                            return;
                        }
                        if (!graphJobSelections.TryGetValue(graphId, out var selections))
                            graphJobSelections.Add(graphId, selections = new List<GraphJobSelectionInput>());
                        selections.Add(selection);
                        continue;
                    }
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
                        var patchError = "graph layout unavailable";
                        GraphJobPatchPlan? plan = null;
                        graphSlots.TryGetValue(graphId, out var jobSlots);
                        if (jobSlots != null) {
                            var slotTypes = jobSlots.Select(static slot => slot.Type).ToArray();
                            plan = automaticSystemInjection
                                ? GraphJobPatchPlan.CreateSystem(input.Right, symbol, type, slotTypes, out patchError)
                                : GraphJobPatchPlan.Create(input.Right, symbol, type, groupIdentity, slotTypes);
                        }
                        if (plan == null) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid graph injection patch for " + type + " in graph " + graphId.ToString(CultureInfo.InvariantCulture) +
                                (automaticSystemInjection ? " (" + patchError + ")" : "") +
                                ". Check field/target layout and private-field setters in the referenced assembly. " +
                                "Jobs/systems with private injected fields and their containing types must be partial; regenerate inputs after compilation/reload."));
                            return;
                        }
                        if (graphInjectionRecord) {
                            var ownerSlot = jobSlots!.FindIndex(slot => SymbolEqualityComparer.Default.Equals(slot.Type, symbol));
                            if (ownerSlot < 0 || plan.Fields.Any(static field => field.Slot < 0)) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Invalid graph system injection owner: " + type));
                                return;
                            }
                            if (!graphInjections.TryGetValue(graphId, out var assignments)) graphInjections.Add(graphId, assignments = new List<(GraphJobPatchPlan Plan, int Slot)>());
                            if (assignments.Any(assignment => assignment.Plan.Key == plan.Key)) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Duplicate graph system injection owner: " + type + " in graph " + graphId));
                                return;
                            }
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
                        slots.Add((symbol, fields[5] == "1", slotSourceId, slotNodeIndex, type));
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
                        // The fourth column is accepted only to let old exports reload.
                        // It must never choose storage layout, Default evaluation or a
                        // registration phase over the current compiler classification.
                        var legacyFlags = componentFlags;
                        if (!componentLayouts.TryGetTag(symbol, out var isTag, out var layoutError)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Cannot classify component " + type + ": " + layoutError));
                            return;
                        }
                        componentFlags = ComponentRegistrationFlags.Get(symbol, isTag);
                        if (fields.Length == 4 && legacyFlags != componentFlags)
                            output.ReportDiagnostic(Diagnostic.Create(StaleComponentFlags, Location.None,
                                "Legacy component flags differ from compiler classification: " + type +
                                " (input=" + legacyFlags.ToString(CultureInfo.InvariantCulture) +
                                ", compiler=" + componentFlags.ToString(CultureInfo.InvariantCulture) +
                                "). Compiler flags are used. Regenerate inputs after Editor reload: " + file.Path));
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
                        if (aspectOwners.Distributed) {
                            if (fields[0] != "aspect-construction-auto") {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Distributed aspects require compiler-selected construction."));
                                return;
                            }
                            aspectConstructionSelection.Add(symbol);
                            continue;
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
                            !symbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, contract)) || (!aspectOwners.Distributed && queries?.Length != 1)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Invalid aspect registration " + fields[1] + ": " + type +
                                " (requires closed unmanaged IAspect and a generated query initializer; rebuild its source catalog)"));
                            return;
                        }
                        aspectRegistrations.Add((symbol, aspectOwners.Distributed ? null : queries![0]));
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
                if (!themeMenu.Validate(input.Right, out var themePlanError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, themePlanError + " in " + file.Path));
                    return;
                }
                if (!viewTypes.Validate(registrationKinds.Count(static kind => kind == "view-types"), viewTracker, out var viewTypePlanError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, viewTypePlanError + " in " + file.Path));
                    return;
                }
                if (!networkMethods.Validate(registrationKinds.Count(static kind => kind == "network-methods"), out var networkPlanError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, networkPlanError + " in " + file.Path));
                    return;
                }
                if (!viewTracker.Validate(resolver, input.Right, out var trackerError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, trackerError + " in " + file.Path));
                    return;
                }
                if (!viewsOwners.Matches(viewTracker, viewTypes)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "View publication owners do not match the exact compiler-selected tracker/type order. Regenerate inputs."));
                    return;
                }
                if (!systemDependencies.Validate(resolver, input.Right, out var dependencyPlanError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, dependencyPlanError + " in " + file.Path));
                    return;
                }
                if (viewTracker.HasIgnoredInputDependencies) output.ReportDiagnostic(Diagnostic.Create(StaleViewTracker, Location.None,
                    "View tracker inputs contain dependencies for owners now implementing IViewIgnoreTracker. " +
                    "Those owner dependencies were excluded using compiler symbols. Regenerate inputs after Editor reload: " + file.Path));
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
                if (!configOwners.Matches(maskRegistrations, collectionCallbackTypes, resolver) ||
                    configOwners.Distributed && (!compilerMaskFields || !compilerCollectionCounts || !compilerCollectionCallbacks)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Config publication phases differ from the compiler-owned selection."));
                    return;
                }
                if (compilerSystemInjections) {
                    if (graphInjections.Count != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                            "Cannot mix compiler-selected graph system injection schema with per-owner injection records. Regenerate inputs."));
                        return;
                    }
                    foreach (var graph in graphRegistrations) {
                        var slots = graphSlots[graph.Id];
                        var types = slots.Select(static slot => slot.Type).ToArray();
                        var owners = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                        var plans = new List<(GraphJobPatchPlan Plan, int Slot)>();
                        for (var slot = 0; slot < slots.Count; ++slot) {
                            if (!owners.Add(slots[slot].Type)) continue;
                            var plan = GraphJobPatchPlan.CreateSystem(input.Right, slots[slot].Type, slots[slot].Identity, types, out var error);
                            if (plan == null) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                    "Invalid graph system injection for " + slots[slot].Identity + " in graph " + graph.Id + ": " + error +
                                    ". Private injected fields and their containing types must be partial."));
                                return;
                            }
                            plans.Add((plan, slot));
                        }
                        graphInjections.Add(graph.Id, plans);
                    }
                }
                if (compilerJobInjections) {
                    if (graphApply.Count != 0 || graphJobs.Count != 0 || deltaJobs.Count != 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                            "Cannot mix compiler-selected graph injections with Editor-selected job fields or apply sequences. Regenerate inputs."));
                        return;
                    }
                    var deltaTypes = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
                    foreach (var graph in graphRegistrations) {
                        var owners = graphInjections[graph.Id];
                        var selections = graphJobSelections.TryGetValue(graph.Id, out var selectedJobs) ? selectedJobs : new List<GraphJobSelectionInput>();
                        if (!selections.Select(static selection => selection.Owner).SequenceEqual(owners.Select(static owner => owner.Plan.Job), SymbolEqualityComparer.Default)) {
                            output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                "Missing, duplicate or out-of-order graph job owner selections in graph " + graph.Id + ". Regenerate inputs."));
                            return;
                        }
                        var slotTypes = graphSlots[graph.Id].Select(static slot => slot.Type).ToArray();
                        var cached = new Dictionary<INamedTypeSymbol, GraphJobPatchPlan>(SymbolEqualityComparer.Default);
                        var plans = new List<GraphJobPatchPlan>();
                        var patches = new List<GraphJobPatchPlan>();
                        var actions = new List<string>();
                        for (var owner = 0; owner < owners.Count; ++owner) {
                            actions.Add("s:" + owners[owner].Plan.Key);
                            if (!selections[owner].Select(resolver, input.Right, out var selectionError)) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, selectionError + " in graph " + graph.Id +
                                    ". Recompile source catalogs and regenerate graph inputs."));
                                return;
                            }
                            foreach (var job in selections[owner].Jobs) {
                                if (!cached.TryGetValue(job.Type, out var plan)) {
                                    plan = GraphJobPatchPlan.CreateJob(input.Right, job.Type, job.Identity, slotTypes, out var error);
                                    if (plan == null) {
                                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                                            "Invalid graph job injection for " + job.Identity + " in graph " + graph.Id + ": " + error +
                                            ". Private injected fields and their containing types must be partial."));
                                        return;
                                    }
                                    cached.Add(job.Type, plan);
                                    plans.Add(plan);
                                    if (plan.HasSystem) patches.Add(plan);
                                    else if (plan.Fields.Count != 0) {
                                        if (deltaTypes.TryGetValue(job.Type, out var previous)) {
                                            if (previous != job.Identity) {
                                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Conflicting delta job identities: " + job.Identity));
                                                return;
                                            }
                                        } else {
                                            deltaTypes.Add(job.Type, job.Identity);
                                            deltaJobs.Add((plan.Key, job.Type, plan.Fields.Select(static field => field.Field).ToArray()));
                                        }
                                    }
                                }
                                if (plan.Identity != job.Identity) {
                                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Conflicting graph job identities: " + job.Identity));
                                    return;
                                }
                                if (plan.Fields.Count != 0) actions.Add((plan.HasSystem ? "j:" : "d:") + plan.Key);
                            }
                        }
                        graphJobs.Add(graph.Id, patches);
                        graphJobInjectionPlans.Add(graph.Id, plans);
                        graphApply.Add(graph.Id, actions.ToArray());
                    }
                } else if (graphJobSelections.Count != 0) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Graph job selections require compiler graph injection schema. Regenerate inputs."));
                    return;
                }
                foreach (var apply in graphApply) {
                    graphInjections.TryGetValue(apply.Key, out var injections);
                    var systemActions = apply.Value.Where(static action => action.StartsWith("s:", StringComparison.Ordinal)).ToArray();
                    var expected = injections?.Select(static assignment => "s:" + assignment.Plan.Key).ToArray() ?? Array.Empty<string>();
                    if (systemActions.Any(action => !expected.Contains(action, StringComparer.Ordinal)) ||
                        (compilerSystemInjections && !systemActions.SequenceEqual(expected, StringComparer.Ordinal))) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                            "Graph system injection actions are incomplete, duplicated or out of owner order in graph " + apply.Key + ". Regenerate inputs."));
                        return;
                    }
                }
                foreach (var topology in graphTopologies) {
                    if (!topology.Value.BindSlots(graphSlots[topology.Key].Select(static slot => slot.Type).ToArray(), resolver, out var bindingError)) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, bindingError + " in graph " + topology.Key));
                        return;
                    }
                }
                if (!systemOwners.Matches(systemRegistrations)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "System registration owners must match the exact selected registration order. Regenerate inputs."));
                    return;
                }
                if (!typeOwners.Matches(groupRegistrations, componentRegistrations, resolver)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Component/group owners must match the exact phase-major registration selection. Regenerate inputs."));
                    return;
                }
                if (!entityOwners.Matches(entityRegistrations, resolver)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Entity owners must match the exact group-ID order. Regenerate inputs."));
                    return;
                }
                var systemAot = SystemAotInputEmitter.Create(input.Right, systemRegistrations, out var aotError);
                if (!destroyOwners.Matches(destroyRegistrations, resolver)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Destroy owners must match the exact selected callback order. Regenerate inputs."));
                    return;
                }
                if (!aspectOwners.Matches(aspectRegistrations.Select(item => item.Type).ToArray(), aspectConstructionSelection, resolver)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Aspect owners and construction must match the exact selected registration order. Regenerate inputs."));
                    return;
                }
                if (systemAot == null) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, aotError));
                    return;
                }
                var source = new StringBuilder("// <auto-generated/>\n");
                source.Append(systemAot.Metadata);
                viewTracker.AppendMetadata(source);
                viewTypes.AppendMetadata(source);
                if (!networkOwners.Matches(networkMethods.Methods) || networkOwners.Distributed && !networkMethods.HasSchema) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Network publication owners do not match the ordered method selection"));
                    return;
                }
                networkMethods.AppendMetadata(source);
                themeMenu.AppendMetadata(source);
                foreach (var graph in graphRegistrations)
                    if (graphInjections.TryGetValue(graph.Id, out var injections))
                        foreach (var assignment in injections)
                            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphSystemInjectionPlan.v1\", ")
                                .Append(SymbolDisplay.FormatLiteral(assignment.Plan.DescribeSystem(graph.Id, assignment.Slot), true)).Append(")]\n");
                foreach (var graph in graphRegistrations) {
                    if (graphJobSelections.TryGetValue(graph.Id, out var selectedJobs))
                        foreach (var selection in selectedJobs)
                            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphJobSelection.v1\", ")
                                .Append(SymbolDisplay.FormatLiteral(selection.Describe(graph.Id), true)).Append(")]\n");
                    if (graphJobInjectionPlans.TryGetValue(graph.Id, out var jobs))
                        foreach (var job in jobs)
                            source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphJobInjectionPlan.v1\", ")
                                .Append(SymbolDisplay.FormatLiteral(job.DescribeJob(graph.Id), true)).Append(")]\n");
                    if (graphApply.TryGetValue(graph.Id, out var actions))
                        source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphInjectionActions.v1\", ")
                            .Append(SymbolDisplay.FormatLiteral("v1\n" + graph.Id.ToString(CultureInfo.InvariantCulture) + "\n" + string.Join(",", actions), true)).Append(")]\n");
                }
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
                if (graphInputSnapshot != null)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.GraphInputSnapshot.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(graphInputSnapshot, true)).Append(")]\n");
                DebugJobInputEmitter.AppendMetadata(source, debugPlans);
                jobWeights.AppendMetadata(source);
                jobEntityInitializers.AppendMetadata(source);
                systemDependencies.AppendMetadata(source);
                foreach (var record in records)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInput.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(record, true)).Append(")]\n");
                source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInputProfile.v1\", ")
                    .Append(SymbolDisplay.FormatLiteral(header[2], true)).Append(")]\n");
                source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.InputTransport.v1\", ")
                    .Append(SymbolDisplay.FormatLiteral(ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsScoped(file.Path) ?
                        "scoped-response" : ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsNative(file.Path) ?
                        "native-additionalfile" : "response-file", true)).Append(")]\n");
                if (ME.BECS.CodeGeneration.SourceGeneratorInputFiles.IsScoped(file.Path)) {
                    var symbol = ME.BECS.CodeGeneration.SourceGeneratorInputFiles.CompilationSymbol(file.Content);
                    // Evidence that Unity supplied the new compiler arguments as
                    // well as the data, rather than compiling a stale response.
                    source.Append("#if ").Append(symbol).Append("\n")
                        .Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.InputCompilerFingerprint.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(symbol, true)).Append(")]\n#endif\n");
                }
                source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.InputContentHash.v1\", ")
                    .Append(SymbolDisplay.FormatLiteral(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(file.Content), true)).Append(")]\n");
                foreach (var resolution in resolutions)
                    source.Append("[assembly: global::System.Reflection.AssemblyMetadataAttribute(\"ME.BECS.TypeInputResolution.v1\", ")
                        .Append(SymbolDisplay.FormatLiteral(resolution, true)).Append(")]\n");
                var bootstrapNamespace = header[2] == "editor" ? "ME.BECS.Editor" : "ME.BECS";
                if (sourceOnlyBootstrap && (bootstrapKinds.Contains("legacy") || registrationKinds.Contains("legacy"))) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                        "bootstrap-schema v2 requires compiler-owned feeder contracts; legacy C# hooks cannot be exported. Migrate the feeder to source inputs."));
                    return;
                }
                if (!compilerBootstrap && BootstrapGenerator.RequiresInputs(input.Right) && !BootstrapGenerator.HasExportedHooks(input.Right)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None,
                        "Missing bootstrap-schema for compiler-owned entry points. Regenerate active inputs using the current Editor exporter."));
                    return;
                }
                if (compilerBootstrap && !BootstrapGenerator.ValidateCompilerOwners(input.Right, debugSchema, out var compilerBootstrapError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, compilerBootstrapError));
                    return;
                }
                var dependencyOwner = input.Right.Assembly.GetTypeByMetadataName("ME.BECS.Editor.StaticMethods");
                if (header[2] == "editor" && (compilerBootstrap || dependencyOwner?.GetMembers("SourceSystemDependenciesV1").Length > 0)) {
                    if (!systemDependencies.HasSchema || dependencyOwner?.GetMembers("InitializeSystemDependenciesInfo").Length > 0) {
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
                if (!jobSetupOwners.Matches(records.Select(row => row.Substring(header[2].Length + 1)), entityRegistrations)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Job statistics owners do not match the IL plans, debug/layout union or global entity IDs."));
                    return;
                }
                if (!jobDebugOwners.Matches(records.Select(row => row.Substring(header[2].Length + 1)))) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Job debug owners do not match the exact ordered job/contract/safety selection."));
                    return;
                }
                source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapJobDebugSelection { public static void Publish() {\n")
                    .Append("global::ME.BECS.BootstrapRuntime.ExpectJobDebugPlan(")
                    .Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(jobDebugOwners.Plan, true)).Append(", ")
                    .Append(jobDebugOwners.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(", editor: ")
                    .Append(header[2] == "editor" ? "true" : "false").Append(");\n} } }\n");
                if (!jobBootstrap.Append(source, bootstrapNamespace, debugPlans, jobWeights, jobEntityInitializers, jobInitOwners, jobSetupOwners, header[2] == "editor", out var bootstrapJobError)) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, bootstrapJobError));
                    return;
                }
                var bootstrapType = input.Right.Assembly.GetTypeByMetadataName(bootstrapNamespace + ".StaticTypesInitializer");
                if (compilerBootstrap || bootstrapType?.GetMembers("SourceBootstrapPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (bootstrapFeeders.Count == 0 || bootstrapType?.GetMembers("RegisterAdditionalTypes").Length > 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing bootstrap feeder plan or mixed bootstrap templates. Regenerate active inputs."));
                        return;
                    }
                    var hooks = bootstrapFeeders.Select(identity => "InitializeFeeder_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity)).ToArray();
                    for (var hookIndex = 0; hookIndex < hooks.Length; ++hookIndex) {
                        var hook = hooks[hookIndex];
                        if (bootstrapKinds[hookIndex] != "legacy") {
                            if (bootstrapType?.GetMembers(hook).Length > 0) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Compiler-owned bootstrap hook is still exported: " + hook + ". Regenerate bootstrap."));
                                return;
                            }
                            continue;
                        }
                        if (bootstrapType?.GetMembers(hook).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 0 && method.Arity == 0) != true) {
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
                if (compilerBootstrap || registrationType?.GetMembers("SourceRegistrationPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (bootstrapFeeders.Count == 0 || registrationType?.GetMembers("RegisterGeneratedMethods").Length > 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing feeder registration plan or mixed bootstrap templates. Regenerate active inputs."));
                        return;
                    }
                    var hooks = bootstrapFeeders.Select(identity => "RegisterFeeder_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity)).ToArray();
                    for (var index = 0; index < hooks.Length; ++index) {
                        var hook = hooks[index];
                        if (registrationKinds[index] != "legacy") {
                            if (registrationType?.GetMembers(hook).Length > 0) {
                                output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Compiler-owned registration hook is still exported: " + hook + ". Regenerate bootstrap."));
                                return;
                            }
                            continue;
                        }
                        if (registrationType?.GetMembers(hook).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 0 && method.Arity == 0) != true) {
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
                if ((compilerBootstrap && debugSchema) || debugType?.GetMembers("SourceDebugPlanV1").OfType<IMethodSymbol>().Any() == true) {
                    if (!debugSchema || debugType?.GetMembers("InitializeJobsDebug").Length > 0) {
                        output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Missing debug job schema or mixed debug templates. Regenerate bootstrap and inputs together."));
                        return;
                    }
                    if (jobDebugOwners.Distributed) DebugJobInputEmitter.AppendAdapter(source, bootstrapNamespace, header[2] == "editor");
                    else DebugJobInputEmitter.Append(source, bootstrapNamespace, debugPlans);
                }
                if (!jobSetupOwners.Distributed) {
                    JobLayoutInputEmitter.Append(source, input.Right, debugPlans);
                    jobWeights.Append(source);
                }
                string entityInitializerError;
                if (!(jobSetupOwners.Distributed ? jobEntityInitializers.ValidateGroups(entityRegistrations, out entityInitializerError) :
                      jobEntityInitializers.Append(source, entityRegistrations, out entityInitializerError))) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, entityInitializerError));
                    return;
                }
                DestroyInputEmitter.Append(source, destroyRegistrations, destroyOwners, header[2] == "editor", target);
                networkMethods.Append(source, networkOwners, header[2] == "editor");
                if (!output.Failed) DestroyInputEmitter.EmitCatalog(output.Context, input.Right, destroyRegistrations);
                ConfigInputEmitter.Append(source, configOwners, header[2] == "editor", maskRegistrations, collectionCallbackBodies, collectionCounts);
                source.Append("namespace ME.BECS.SourceGenerated { internal static class EntityInputs {\npublic const uint GroupCount = ")
                    .Append(entityRegistrations.Count.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                for (var entityId = 0; entityId < entityRegistrations.Count; ++entityId) {
                    var entity = entityRegistrations[entityId];
                    var identity = entity.ContainingAssembly.Identity + "\t" + entity.GetDocumentationCommentId();
                    source.Append("public const uint Id_").Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(identity))
                        .Append(" = ").Append(entityId.ToString(CultureInfo.InvariantCulture)).Append("u;\n");
                }
                source.Append("public static void Initialize() => global::ME.BECS.BootstrapRuntime.RegisterInstalledEntities(editor: ")
                    .Append(header[2] == "editor" ? "true" : "false").Append(");\n");
                if (!entityOwners.Distributed) {
                    // One-way upgrade of pre-publication snapshots. New snapshots
                    // emit no typed entity registration body in the aggregate.
                    for (var entityId = 0; entityId < entityRegistrations.Count; ++entityId)
                        source.Append("public static void Register_").Append(entityId.ToString(CultureInfo.InvariantCulture))
                            .Append("() => global::ME.BECS.EntityTypes.Register<")
                            .Append(entityRegistrations[entityId].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            .Append(">(").Append(entityId.ToString(CultureInfo.InvariantCulture)).Append(");\n");
                }
                source.Append("} }\n");
                if (entityOwners.Distributed) {
                    source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapEntitySelection { public static void Publish() => ")
                        .Append("global::ME.BECS.BootstrapRuntime.ExpectEntityPlan(").Append(SymbolDisplay.FormatLiteral(entityOwners.Plan, true))
                        .Append(", ").Append(entityOwners.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ")
                        .Append(header[2] == "editor" ? "true" : "false").Append("); } }\n");
                } else {
                    var entityPlan = new BootstrapTypePlanEmitter();
                    for (var entityId = 0; entityId < entityRegistrations.Count; ++entityId)
                        entityPlan.Add(target, "global::ME.BECS.SourceGenerated.EntityInputs.Register_" + entityId.ToString(CultureInfo.InvariantCulture));
                    entityPlan.Append(source, header[2] == "editor", kind: "Entity");
                }
                if (!graphOwners.Matches(records.Select(row => row.Substring(header[2].Length + 1))) ||
                    graphOwners.Distributed && graphRegistrations.Count != 0 && !compilerJobInjections) {
                    output.ReportDiagnostic(Diagnostic.Create(Invalid, Location.None, "Graph publications differ from the complete compiler-owned graph selection."));
                    return;
                }
                if (header[2] == "runtime") source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapGraphSelection { public static void Publish() => ")
                    .Append("global::ME.BECS.BootstrapRuntime.ExpectGraphPlan(").Append(SymbolDisplay.FormatLiteral(graphOwners.Plan, true))
                    .Append(", ").Append(graphOwners.Count.ToString(CultureInfo.InvariantCulture)).Append("); } }\n");
                if (graphOwners.Distributed) {
                    source.Append("namespace ME.BECS.SourceGenerated { internal static class GraphInputs {\n");
                    if (header[2] == "runtime") source.Append("[global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]\n")
                        .Append("private static void Initialize() => global::ME.BECS.CustomModules.RegisterFirstPass(Register);\n");
                    source.Append("[global::UnityEngine.Scripting.Preserve] public static void Register() ")
                        .Append(header[2] == "runtime" ? "=> global::ME.BECS.BootstrapRuntime.RegisterInstalledGraphs();" : "{ }").Append("\n} }\n");
                } else GraphInputEmitter.Append(source, input.Right, resolver, graphRegistrations, graphSlots, graphTopologies, lifecyclePlans,
                    graphJobs, graphInjections, graphApply, installFirstPass: header[2] == "runtime");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class SystemInputs {\n");
                foreach (var registration in systemRegistrations) {
                    source.Append("public static void Register_")
                        .Append(ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity))
                        .Append("() => ");
                    if (GenericSystemGenerator.TryResolve(registration.Type, input.Right, "Register", out var systemOwnerTarget))
                        source.Append(systemOwnerTarget).Append("();\n");
                    else source.Append("global::ME.BECS.StaticSystemTypes<")
                        .Append(registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append(">.Validate();\n");
                }
                source.Append("} }\n");
                if (!graphOwners.Distributed) GraphInputEmitter.AppendDeltas(source, deltaJobs);
                // Preserve the historical registration passes, not per-component grouping:
                // shared/static validation has its own counters and must follow all normal IDs.
                var typePlan = new BootstrapTypePlanEmitter();
                var systemPlan = new BootstrapTypePlanEmitter();
                for (var systemOrdinal = 0; systemOrdinal < systemRegistrations.Count; ++systemOrdinal) {
                    var registration = systemRegistrations[systemOrdinal];
                    var forwarded = GenericSystemGenerator.TryResolve(registration.Type, input.Right, "Register", out var ownerTarget);
                    systemPlan.Add(systemOwners.Owner(systemOrdinal, forwarded ? registration.Type.ContainingAssembly.Name : target), forwarded ? ownerTarget :
                        "global::ME.BECS.SourceGenerated.SystemInputs.Register_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity));
                }
                if (systemOwners.Distributed) {
                    source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapSystemSelection { public static void Publish() => ")
                        .Append("global::ME.BECS.BootstrapRuntime.ExpectSystemPlan(").Append(SymbolDisplay.FormatLiteral(systemOwners.Plan, true))
                        .Append(", ").Append(systemRegistrations.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ")
                        .Append(header[2] == "editor" ? "true" : "false").Append("); } }\n");
                } else systemPlan.Append(source, header[2] == "editor", kind: "System");
                typePlan.Add(target, "global::ME.BECS.SourceGenerated.GroupInputs.Initialize");
                foreach (var phase in new[] { (Flag: 0, Name: "Register"), (Flag: 8, Name: "RegisterShared"),
                             (Flag: 2, Name: "RegisterStatic"), (Flag: 32, Name: "RegisterConfig") }) {
                    foreach (var registration in componentRegistrations.Where(item => phase.Flag == 0 || (item.Flags & phase.Flag) != 0)) {
                        var forwarded = ComponentRegistrationGenerator.TryResolve(registration.Type, registration.Flags, input.Right,
                            out var owner, out var key, out var arguments);
                        typePlan.Add(forwarded ? registration.Type.ContainingAssembly.Name : target,
                            forwarded ? owner + "." + phase.Name + "_" + key + arguments :
                            "global::ME.BECS.SourceGenerated.ComponentInputs." + phase.Name + "_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity));
                    }
                }
                if (typeOwners.Distributed) {
                    source.Append("namespace ME.BECS.SourceGenerated { internal static class BootstrapTypeInputs { public static void Publish() => ")
                        .Append("global::ME.BECS.BootstrapRuntime.ExpectTypePlan(").Append(SymbolDisplay.FormatLiteral(typeOwners.Plan, true))
                        .Append(", ").Append(typeOwners.Count.ToString(CultureInfo.InvariantCulture)).Append(", editor: ")
                        .Append(header[2] == "editor" ? "true" : "false").Append("); } }\n");
                } else typePlan.Append(source, header[2] == "editor");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class CoreTypeInputs { public static void Initialize() => ")
                    .Append("global::ME.BECS.BootstrapRuntime.RegisterInstalledTypes(editor: ").Append(header[2] == "editor" ? "true" : "false")
                    .Append(");\n[global::UnityEngine.Scripting.PreserveAttribute] public static void AotComponents() {\n");
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
                ViewsInputEmitter.Append(source, viewsOwners, viewTracker, viewTypes, header[2] == "editor");
                themeMenu.Append(source);
                source.Append("namespace ME.BECS.SourceGenerated { internal static class ComponentInputs {\n");
                foreach (var registration in componentRegistrations) {
                    var name = registration.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var key = ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(registration.Identity);
                    ComponentRegistrationGenerator.TryResolve(registration.Type, registration.Flags, input.Right,
                        out var componentOwner, out var ownerKey, out var ownerArguments);
                    // Keep the selected, phase-major dispatch stable. The typed body
                    // lives in its declaring assembly whenever that contract exists.
                    // Precompiled components still use compiler emission here.
                    ComponentRegistrationEmitter.Append(source, name, registration.Flags, key,
                        forwardOwner: componentOwner, forwardKey: ownerKey, arguments: ownerArguments);
                }
                source.Append("} }\n");
                source.Append("namespace ME.BECS.SourceGenerated { internal static class GroupInputs {\n");
                if (!typeOwners.Distributed) {
                    source.Append("public static void Initialize() {\n");
                    foreach (var registration in groupRegistrations)
                        source.Append("global::ME.BECS.StaticTypes<")
                            .Append(registration.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                            .Append(">.ApplyGroup(typeof(")
                            .Append(registration.Group.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append("));\n");
                    source.Append("}\n");
                }
                source.Append("public static global::System.Type[] GetComponents() => new global::System.Type[] { ")
                    .Append(string.Join(",", groupRegistrations.Select(registration => "typeof(" + registration.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append(" };\npublic static global::System.Type[] GetGroups() => new global::System.Type[] { ")
                    .Append(string.Join(",", groupRegistrations.Select(registration => "typeof(" + registration.Group.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")")))
                    .Append(" };\n} }\n");
                AspectInputEmitter.Append(source, aspectOwners, aspectRegistrations, aspectConstructors, header[2] == "editor", target);
                selected = source.ToString();
                selectedCompilerBootstrap = compilerBootstrap;
            }
            if (selected != null && !output.Failed) {
                output.AddSource("ME.BECS.TypeInputs.g.cs", SourceText.From(selected, Encoding.UTF8));
                BootstrapGenerator.Emit(output.Context, input.Right, selectedCompilerBootstrap);
            } else if (selected == null && !output.Failed && BootstrapGenerator.RequiresInputs(input.Right)) {
                // Recovery permits the Editor exporter to reload, but emits no
                // bootstrap or freshness evidence. Player compilation remains strict.
                var descriptor = input.Right.AssemblyName == "ME.BECS.Gen.Editor" ? MissingEditorInputs : Invalid;
                output.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None,
                    "Missing BECS input manifest for " + input.Right.AssemblyName +
                    ". Check the first Editor input export exception, then retry ME.BECS/Source Generator/Rebuild Inputs (Full Analysis). " +
                    "If export succeeded, check Unity's project-owned native additional-file inputs. Bootstrap entry points were not emitted; " +
                    "regenerate and compile inputs before running worlds or integration tests."));
            }
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
        "network-methods" => "global::ME.BECS.SourceGenerated.NetworkMethodInputs.Initialize();",
        "view-types" => "global::ME.BECS.SourceGenerated.ViewTypeInputs.Initialize();",
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
