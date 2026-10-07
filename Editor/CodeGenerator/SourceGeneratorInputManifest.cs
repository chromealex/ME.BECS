namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class SourceGeneratorInputManifest {
        internal static Type[] OrderFeederTypes(IEnumerable<Type> types) => types
            .Where(type => typeof(CustomCodeGenerator).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters)
            // Preserve nullable priority semantics of the existing exporter:
            // an absent attribute precedes explicit priorities, including negative ones.
            .OrderBy(type => ((CodeGeneratorOrderAttribute)Attribute.GetCustomAttribute(type, typeof(CodeGeneratorOrderAttribute)))?.order)
            .ThenBy(type => type.FullName, StringComparer.Ordinal)
            .ThenBy(type => type.Assembly.FullName, StringComparer.Ordinal).ToArray();

        internal static CustomCodeGenerator[] CreateFeeders() =>
            OrderFeederTypes(UnityEditor.TypeCache.GetTypesDerivedFrom<CustomCodeGenerator>())
                .Select(type => {
                    SourceGeneratorExportContract.ValidateType(type);
                    return (CustomCodeGenerator)Activator.CreateInstance(type);
                }).ToArray();

        internal static Type[] GetInputReferenceTypes(Systems.SystemDependenciesCodeGenerator.UsedObjects used) {
            var types = new HashSet<Type>();
            void Add(IEnumerable<Type> source) {
                if (source == null) throw new InvalidOperationException("Missing source input discovery list.");
                foreach (var type in source) {
                    if (type == null) throw new InvalidOperationException("Null source input dependency.");
                    types.Add(type);
                }
            }
            Add(used.systems);
            Add(used.components);
            Add(used.componentsGroup);
            Add(used.jobTypes);
            Add(used.entityTypes);
            Add(used.aspects);
            // Expansion works on a copy: the discovery order feeds registration IDs.
            var closedSystems = new List<Type>(used.systems);
            CodeGenerator.PatchSystemsList(closedSystems);
            Add(closedSystems);
            return types.OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
        }

        // Shared dependency planning for active inputs and their consumer assembly.
        // Never depend on generating C# text merely to discover assembly references.
        internal static string[] GetAssemblyReferenceNames(IEnumerable<AssemblyInfo> assemblies,
            IEnumerable<Type> types, bool editor) {
            var byName = new Dictionary<string, AssemblyInfo>(StringComparer.Ordinal);
            foreach (var assembly in assemblies)
                if (!byName.ContainsKey(assembly.name)) byName.Add(assembly.name, assembly);
            var names = new HashSet<string>(StringComparer.Ordinal);
            bool AddAssembly(string name) {
                if (!editor && byName.TryGetValue(name, out var info) && info.isEditor) return false;
                return names.Add(name);
            }
            var visited = new HashSet<Type>();
            void AddType(Type type, bool root) {
                if (type == null) return;
                if (type.HasElementType) { AddType(type.GetElementType(), root); return; }
                if (type.IsGenericParameter) return;
                if (!editor && byName.TryGetValue(type.Assembly.GetName().Name, out var info) && info.isEditor) return;
                // Framework/precompiled DLL references are not asmdef references.
                if (root || byName.ContainsKey(type.Assembly.GetName().Name)) AddAssembly(type.Assembly.GetName().Name);
                // A previously traversed generic argument may later be an explicit
                // root. Preserve its reference before skipping repeated traversal.
                if (!visited.Add(type)) return;
                // A closed system/job may contain a component from a different assembly.
                if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) AddType(argument, false);
            }
            foreach (var type in types) AddType(type, true);
            // Retain the existing direct-reference expansion; do not traverse into
            // unrelated assemblies and accidentally introduce generated assembly cycles.
            foreach (var name in names.ToArray())
                if (byName.TryGetValue(name, out var info) && info.references != null)
                    foreach (var reference in info.references) AddAssembly(reference);
            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        // Registers graph references and prepares input text; publishing input files
        // and updating assembly references remain the caller's responsibility.
        internal static System.Collections.IEnumerator PrepareActiveInputsSteps(StepResult<string> output, string targetAssembly, bool editor,
            IEnumerable<CustomCodeGenerator> addonFeeders, Systems.SystemDependenciesCodeGenerator.UsedObjects prepared) {
            CodeGeneratorTimings.Stage("Discover used types", 0.02f);
            var feeders = (addonFeeders ?? CreateFeeders()).ToArray();
            CodeGeneratorTimings.Stage("Prepare graph inputs", 0.15f);
            yield return null;
            var steps = SerializeSteps(output, targetAssembly, editor, prepared, registerGraphReferences: true, addonFeeders: feeders);
            try { while (steps.MoveNext()) yield return null; }
            finally { (steps as IDisposable)?.Dispose(); }
        }

        internal static string PrepareActiveInputs(string targetAssembly, bool editor,
            IEnumerable<CustomCodeGenerator> addonFeeders,
            out Systems.SystemDependenciesCodeGenerator.UsedObjects used, List<Type> references = null,
            Systems.SystemDependenciesCodeGenerator.UsedObjects? prepared = null) {
            CodeGeneratorTimings.Stage("Discover used types", 0.02f);
            if (prepared.HasValue) used = prepared.Value;
            else Systems.SystemDependenciesCodeGenerator.GetUsedObjects(editor, out used);
            var feeders = (addonFeeders ?? CreateFeeders()).ToArray();
            CodeGeneratorTimings.Stage("Prepare graph inputs", 0.15f);
            var manifest = Serialize(targetAssembly, editor, used, registerGraphReferences: true, addonFeeders: feeders);
            if (references != null) {
                references.AddRange(GetInputReferenceTypes(used));
                foreach (var feeder in feeders) feeder.AddPreparedInputReferences(references);
            }
            return manifest;
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Editor Type Inputs")]
        private static void ExportEditor() => Export(true);

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Runtime Type Inputs")]
        private static void ExportRuntime() => Export(false);

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Verify Published Inputs (Build Preflight)")]
        private static void VerifyPublishedInputs() {
            var ok = TryAnalyzePublishedInputs(out var reason);
            UnityEngine.Debug.Log("[ME.BECS] Build preflight " + (ok ? "passed." : "failed: " + reason));
        }

        // A build machine with a fresh Library has no local analysis receipt.
        // Reconstruct it by analyzing current IL and comparing the complete data,
        // without publishing assets, registering graph references or compiling.
        internal static bool TryAnalyzePublishedInputs(out string reason) {
            reason = "";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var timings = new StringBuilder();
            void Mark(string stage) {
                timings.Append("\n  ").Append(stage).Append(": ").Append(watch.ElapsedMilliseconds).Append(" ms");
                watch.Restart();
            }
            try {
                var code = SourceGeneratorGraphSnapshot.GetCodeFingerprint();
                Mark("code fingerprint");
                using var analysis = new ILAnalysisSession(code, false);
                using var incremental = new ILPersistentAnalysis(false);
                var fingerprint = SourceGeneratorGraphSnapshot.GetCurrent();
                Mark("graph fingerprint");
                var compilerSnapshot = SourceGeneratorGraphSnapshot.GetCompilerSnapshot();
                Mark("compiler snapshot");
                var content = new string[2];
                foreach (var editor in new[] { false, true }) {
                    using var lookup = SourceGeneratorBridge.BeginLookupScope();
                    Systems.SystemDependenciesCodeGenerator.UsedObjects used;
                    if (editor) Systems.SystemDependenciesCodeGenerator.GetUsedObjects(true, out used);
                    else {
                        var roots = Systems.SystemDependenciesCodeGenerator.CaptureRuntimeDiscovery();
                        Mark("Runtime discovery roots (assets)");
                        used = Systems.SystemDependenciesCodeGenerator.AnalyzeRuntimeDiscovery(roots, false);
                    }
                    Mark((editor ? "Editor" : "Runtime") + " used objects (IL)");
                    var index = editor ? 1 : 0;
                    content[index] = Serialize("ME.BECS.Gen." + (editor ? "Editor" : "Runtime"), editor, used);
                    Mark((editor ? "Editor" : "Runtime") + " serialize");
                    var published = File.ReadAllText(SourceGeneratorInputTransport.InputPath(editor));
                    Mark((editor ? "Editor" : "Runtime") + " read published");
                    if (content[index] != published) {
                        reason = "Current IL/assets require different " + (editor ? "Editor" : "Runtime") +
                            " source inputs. Regenerate inputs and compile them before building; build preflight does not modify assets.";
                        return false;
                    }
                }
                if (fingerprint != SourceGeneratorGraphSnapshot.GetCurrent()) {
                    reason = "Code/assets changed during source input analysis. Retry after imports settle.";
                    return false;
                }
                Mark("final graph fingerprint");
                SourceGeneratorAnalysisReceipt.Commit(fingerprint, compilerSnapshot, content[0], content[1]);
                Mark("commit receipt");
                return true;
            } catch (Exception exception) { reason = "Cannot analyze published source inputs: " + exception.Message; return false; }
            finally { UnityEngine.Debug.Log("[ME.BECS] Build preflight timings:" + timings); }
        }

        private static void Export(bool editor) {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before exporting type inputs.");
                return;
            }
            try {
                using var lookup = SourceGeneratorBridge.BeginLookupScope();
                Systems.SystemDependenciesCodeGenerator.GetUsedObjects(editor, out var used, useSourceCatalogs: false);
                var target = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
                var content = Serialize(target, editor, used);
                var directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Temp/ME.BECS.SourceGenerator"));
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, editor ? "Editor.becs-inputs" : "Runtime.becs-inputs");
                if (!File.Exists(path) || File.ReadAllText(path) != content) File.WriteAllText(path, content, new UTF8Encoding(false));
                UnityEngine.Debug.Log("[ME.BECS] Exported ordered type inputs: " + path +
                    "\nOrdered type and graph-slot snapshot; dependency topology and config payloads are not included. No C# output or registry entries written.");
            } catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }

        internal sealed class StepResult<T> { internal T value; }

        internal static string Serialize(string targetAssembly, bool editor, Systems.SystemDependenciesCodeGenerator.UsedObjects used, bool registerGraphReferences = false,
            IEnumerable<CustomCodeGenerator> addonFeeders = null) {
            var result = new StepResult<string>();
            var steps = SerializeSteps(result, targetAssembly, editor, used, registerGraphReferences, addonFeeders);
            try { while (steps.MoveNext()) { } }
            finally { (steps as IDisposable)?.Dispose(); }
            return result.value;
        }

        // The same preparation as one resumable sequence: the background export runs
        // a few steps per Editor frame instead of freezing the Editor for the whole
        // Runtime+Editor publication. Every yield is between independent stages.
        internal static System.Collections.IEnumerator SerializeSteps(StepResult<string> output, string targetAssembly, bool editor,
            Systems.SystemDependenciesCodeGenerator.UsedObjects used, bool registerGraphReferences = false,
            IEnumerable<CustomCodeGenerator> addonFeeders = null) {
            if (string.IsNullOrWhiteSpace(targetAssembly)) throw new ArgumentException("Target assembly is required.", nameof(targetAssembly));
            using var publicationBridges = SourceGeneratorPublicationBridges.BeginPlanning();
            var feeders = (addonFeeders ?? CreateFeeders()).ToArray();
            SourceGeneratorExportContract.Validate(feeders);
            CodeGeneratorTimings.Stage("Input fingerprints", 0.05f);
            yield return null;
            var graphSnapshot = SourceGeneratorGraphSnapshot.GetCurrent();
            var compilerSnapshot = SourceGeneratorGraphSnapshot.GetCompilerSnapshot();
            CodeGeneratorTimings.Stage("Select systems", 0.08f);
            yield return null;
            var result = new StringBuilder("ME.BECS.TypeInputs.v3\t").Append(Encode(targetAssembly))
                .Append('\t').Append(editor ? "editor" : "runtime").Append('\n');
            result.Append("bootstrap-schema\t0\t").Append(Encode("v2")).Append('\n');
            result.Append("graph-input-snapshot\t0\t").Append(Encode(compilerSnapshot)).Append('\n');
            Append(result, "system", used.systems);
            var selectedSystems = new List<Type>(used.systems);
            CodeGenerator.PatchSystemsList(selectedSystems);
            var assemblies = EditorUtils.GetAssembliesInfo();
            selectedSystems.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true));
            Append(result, "system-registration", selectedSystems);
            SourceGeneratorRegistrationOwners.Append(result, selectedSystems, editor);
            CodeGeneratorTimings.Stage("Select components, destroy and configs", 0.12f);
            yield return null;
            Append(result, "component", used.components);
            var componentOrdinal = 0;
            foreach (var component in used.components) {
                if (!component.IsValueType || !EditorUtils.IsValidTypeForAssembly(editor, component, assemblies, true)) continue;
                result.Append("component-registration\t").Append((componentOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\n');
            }
            Append(result, "component-group", used.componentsGroup);
            result.Append("destroy-schema\t0\t").Append(Encode("v1")).Append('\n');
            var destroyComponents = ComponentDestroyCodeGenerator.GetSelectedComponents(editor, assemblies);
            Append(result, "destroy-registration", destroyComponents);
            SourceGeneratorRegistrationOwners.AppendDestroy(result, destroyComponents, editor);
            result.Append("config-mask-schema\t0\t").Append(Encode("v2")).Append('\n');
            var maskOrdinal = 0;
            result.Append("config-collection-count-schema\t0\t").Append(Encode("v2")).Append('\n');
            result.Append("config-collection-callback-schema\t0\t").Append(Encode("v2")).Append('\n');
            var collectionCountOrdinal = 0;
            var configCollections = Aspects.EntityConfigCodeGenerator.GetCollectionComponents(editor, assemblies);
            var configMasks = Aspects.EntityConfigCodeGenerator.GetMaskComponents(editor, assemblies);
            foreach (var component in configCollections) {
                var ordinal = (collectionCountOrdinal++).ToString(CultureInfo.InvariantCulture);
                result.Append("config-collection-callback\t").Append(ordinal)
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\n');
            }
            foreach (var component in configMasks) {
                result.Append("config-mask-registration\t").Append((maskOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\n');
            }
            SourceGeneratorRegistrationOwners.AppendConfigs(result, configMasks, configCollections, editor);
            var groupOrdinal = 0;
            foreach (var component in used.componentsGroup) {
                if (!EditorUtils.IsValidTypeForAssembly(editor, component, assemblies, true)) continue;
                result.Append("group-registration\t").Append((groupOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\n');
            }
            SourceGeneratorRegistrationOwners.AppendTypes(result,
                used.components.Where(type => type.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true)).ToArray(),
                used.componentsGroup.Where(type => EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true)).ToArray(), editor);
            CodeGeneratorTimings.Stage("Select entities and aspects", 0.18f);
            yield return null;
            Append(result, "job", used.jobTypes);
            Append(result, "entity", used.entityTypes);
            var entitySelector = new EntityTypeCodeGenerator {
                editorAssembly = editor, entityTypes = used.entityTypes, asms = assemblies,
            };
            var registrations = EntityTypeCodeGenerator.GetAllTypes(entitySelector, out _);
            var selectedEntities = new List<Type>(registrations.Length);
            foreach (var registration in registrations) selectedEntities.Add(registration.Item1);
            Append(result, "entity-registration", selectedEntities);
            SourceGeneratorRegistrationOwners.AppendEntities(result, selectedEntities, editor);
            Append(result, "aspect", used.aspects);
            var selectedAspects = new List<Type>(used.aspects);
            selectedAspects.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true));
            SourceGeneratorRegistrationOwners.AppendAspects(result, selectedAspects, editor);
            Append(result, "aspect-registration", selectedAspects);
            Append(result, "aspect-construction-auto", selectedAspects);
            CodeGeneratorTimings.Stage("Graph injection inputs", 0.22f);
            yield return null;
            if (!editor) {
                // Transport graph/job ownership only; the compiler selects all injected
                // fields, callback kinds, target slots and the final apply sequence.
                result.Append("graph-injection-schema\t0\t").Append(Encode("v1")).Append('\n');
                var graphOrdinal = 0;
                var slotOrdinal = 0;
                var analyzedGraphSystems = new Dictionary<Type, string>();
                var graphJobOrdinal = 0;
                var topologyOrdinal = 0;
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph")) {
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<ME.BECS.FeaturesGraph.SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (graph.isInnerGraph) continue;
                    var id = graph.GetId();
                    if (id == int.MinValue) throw new InvalidOperationException("Graph ID cannot be represented in callback names: " + guid);
                    var layout = GetGraphSystems(graph);
                    result.Append("graph-registration\t").Append((graphOrdinal++).ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Encode("ME.BECS.GraphGraph" + EditorUtils.GetCodeName(graph.name)))
                        .Append('\t').Append(id.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(layout.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
                    result.Append("graph-topology\t").Append((topologyOrdinal++).ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Encode("topology")).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Encode(SourceGeneratorGraphTopology.Serialize(graph))).Append('\n');
                    for (var slot = 0; slot < layout.Count; ++slot) {
                        var item = layout[slot];
                        uint sourceId = 0;
                        if (!item.useDefault) {
                            if (registerGraphReferences) {
                                ObjectReferenceRegistry.LoadForced();
                                sourceId = ObjectReferenceRegistry.data.Add(item.graph, out _);
                            } else {
                                foreach (var registered in ObjectReferenceRegistry.data.objects)
                                    if (registered != null && registered.data.Is(item.graph)) { sourceId = registered.data.sourceId; break; }
                                if (sourceId == 0) throw new InvalidOperationException("Graph is not registered; run codegen before exporting a read-only snapshot: " + item.graph.name);
                            }
                        }
                        result.Append("graph-system\t").Append((slotOrdinal++).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(item.type.AssemblyQualifiedName)).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(slot.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(item.useDefault ? "1" : "0")
                            .Append('\t').Append(sourceId.ToString(CultureInfo.InvariantCulture)).Append('\t')
                            .Append((item.useDefault ? 0 : item.nodeIndex).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    }
                    var owners = new HashSet<Type>();
                    foreach (var item in layout) {
                        if (!owners.Add(item.type)) continue;
                        if (!analyzedGraphSystems.TryGetValue(item.type, out var selection)) {
                            var jobs = new HashSet<Type>();
                            SourceGeneratorScheduledJobs.Collect(item.type, jobs);
                            // Behavioral selection is a fresh compiled IL snapshot.
                            // Roslyn owns injection bodies, not a second job inventory.
                            selection = "v2\nil\n" + string.Join("\n", jobs.OrderBy(job => job.FullName, StringComparer.Ordinal)
                                .ThenBy(job => job.Assembly.FullName, StringComparer.Ordinal).Select(job => job.AssemblyQualifiedName));
                            analyzedGraphSystems.Add(item.type, selection);
                        }
                        result.Append("graph-job-selection\t").Append((graphJobOrdinal++).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(item.type.AssemblyQualifiedName)).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(selection)).Append('\n');
                    }
                }
            }
            var feederOrdinal = 0;
            foreach (var feeder in feeders) {
                CodeGeneratorTimings.Stage(feeder.GetType().Name, 0.35f + 0.55f * feederOrdinal / System.Math.Max(1, feeders.Length));
                yield return null;
                result.Append("bootstrap-feeder\t").Append((feederOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(feeder.GetType().AssemblyQualifiedName))
                    .Append('\t').Append(feeder.SourceInitializationKind)
                    .Append('\t').Append(feeder.SourceRegistrationKind).Append('\n');
                feeder.editorAssembly = editor;
                feeder.asms = assemblies;
                feeder.systems = new List<Type>(selectedSystems);
                feeder.jobTypes = new List<Type>(used.jobTypes);
                feeder.entityTypes = new List<Type>(used.entityTypes);
                feeder.aspects = new List<Type>(used.aspects);
                var feederSteps = SourceGeneratorFeederCache.AppendSteps(feeder, result);
                while (feederSteps.MoveNext()) yield return null;
            }
            CodeGeneratorTimings.Stage("Select network publication owners", 0.90f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendNetwork(result, editor);
            CodeGeneratorTimings.Stage("Select job setup publication owners", 0.91f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendJobSetup(result, editor);
            CodeGeneratorTimings.Stage("Select debug job publication owners", 0.92f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendJobDebug(result, editor);
            CodeGeneratorTimings.Stage("Select graph publication owners", 0.93f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendGraphs(result, editor);
            CodeGeneratorTimings.Stage("Select view and dependency publication owners", 0.94f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendViewSelection(result, editor);
            SourceGeneratorRegistrationOwners.AppendSystemDependencies(result, editor);
            SourceGeneratorRegistrationOwners.AppendThemeMenus(result, editor);
            CodeGeneratorTimings.Stage("Prepare bootstrap and input catalog", 0.95f);
            yield return null;
            SourceGeneratorRegistrationOwners.AppendBootstrap(result, editor);
            SourceGeneratorRegistrationOwners.AppendInputCatalog(result, editor);
            CodeGeneratorTimings.Stage("Validate and seal input snapshot", 0.96f);
            yield return null;
            if (graphSnapshot != SourceGeneratorGraphSnapshot.GetCurrent())
                throw new InvalidOperationException("Graphs or loaded script assemblies changed during input preparation. Retry after imports/compilation settle.");
            var payload = result.ToString();
            var recordCount = 0;
            foreach (var character in payload) if (character == '\n') ++recordCount;
            output.value = payload + "end\t" + (recordCount - 1).ToString(CultureInfo.InvariantCulture) + "\t" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(payload) + "\n";
        }

        public static bool TryGetPartialDeltaTimeMethod(System.Reflection.FieldInfo field, out string call) {
            call = null;
            var owner = field.DeclaringType;
            if (owner == null || !owner.IsVisible || owner.ContainsGenericParameters || field.IsStatic || field.IsInitOnly ||
                !Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute)) ||
                (field.FieldType != typeof(uint) && field.FieldType != typeof(float) && field.FieldType != typeof(sfloat))) return false;
            var name = "__BecsInjectDelta_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(field.Name);
            var method = owner.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { owner.MakeByRefType(), typeof(ushort) }, null);
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters ||
                !Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))) return false;
            call = GetClosedTypeName(owner) + "." + name;
            return true;
        }

        // Optional addon transport: only Type identities and compilation owners,
        // never executable code or a core-to-Views assembly reference.
        public static void AppendViewPublicationOwners(StringBuilder manifest, Type[] components, (Type type, bool module)[] trackers,
                                                       Type[] views, Type addonContract, bool editor) =>
            SourceGeneratorRegistrationOwners.AppendViews(manifest, components, trackers, views, addonContract, editor);

        public static bool TryGetPrivateSystemInjectionMethod(System.Reflection.FieldInfo field, out string call) =>
            TryGetPartialInjectionMethod(field, out call, allowPublic: false);

        public static bool TryGetPartialInjectionMethod(System.Reflection.FieldInfo field, out string call, bool allowPublic = true) {
            call = null;
            var owner = field.DeclaringType;
            if (owner == null || !owner.IsVisible || owner.ContainsGenericParameters ||
                (!allowPublic && field.IsPublic) || field.IsStatic || field.IsInitOnly || !field.FieldType.IsGenericType ||
                field.FieldType.GetGenericTypeDefinition() != typeof(InjectSystem<>)) return false;
            var name = "__BecsInject_" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(field.Name);
            var method = owner.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { owner.MakeByRefType(), typeof(void).MakePointerType() }, null);
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters ||
                !Attribute.IsDefined(method, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))) return false;
            call = GetClosedTypeName(owner) + "." + name;
            return true;
        }

        public static string GetClosedTypeName(Type type) {
            if (type.IsArray) return GetClosedTypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsPointer) return GetClosedTypeName(type.GetElementType()) + "*";
            if (type.ContainsGenericParameters || type.IsByRef) throw new ArgumentException("Expected a closed type.", nameof(type));
            var owners = new Stack<Type>();
            for (var owner = type; owner != null; owner = owner.DeclaringType) owners.Push(owner);
            var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
            var argumentIndex = 0;
            var result = new StringBuilder("global::");
            if (!string.IsNullOrEmpty(type.Namespace)) {
                foreach (var segment in type.Namespace.Split('.')) result.Append('@').Append(segment).Append('.');
            }
            while (owners.Count != 0) {
                var owner = owners.Pop();
                var tick = owner.Name.IndexOf('`');
                result.Append('@').Append(tick < 0 ? owner.Name : owner.Name.Substring(0, tick));
                if (tick >= 0) {
                    var arity = int.Parse(owner.Name.Substring(tick + 1), CultureInfo.InvariantCulture);
                    result.Append('<');
                    for (var index = 0; index < arity; ++index) {
                        if (index != 0) result.Append(", ");
                        result.Append(GetClosedTypeName(arguments[argumentIndex++]));
                    }
                    result.Append('>');
                }
                if (owners.Count != 0) result.Append('.');
            }
            if (argumentIndex != arguments.Length) throw new ArgumentException("Inconsistent nested generic type.", nameof(type));
            return result.ToString();
        }

        // Shared by the core exporter and Features.Editor without a reverse assembly dependency.
        public readonly struct GraphSystemInput {
            public readonly Type type;
            public readonly ME.BECS.FeaturesGraph.SystemsGraph graph;
            public readonly int nodeIndex;
            public readonly bool useDefault;
            public GraphSystemInput(Type type, ME.BECS.FeaturesGraph.SystemsGraph graph, int nodeIndex, bool useDefault) {
                this.type = type; this.graph = graph; this.nodeIndex = nodeIndex; this.useDefault = useDefault;
            }
        }

        public static int GetSystemsCount(ME.BECS.FeaturesGraph.SystemsGraph graph) =>
            GetGraphSystems(graph).Count;

        public static List<GraphSystemInput> GetGraphSystems(ME.BECS.FeaturesGraph.SystemsGraph graph) {
            var result = new List<GraphSystemInput>();
            CollectGraphSystems(graph, new HashSet<ME.BECS.FeaturesGraph.SystemsGraph>(), result);
            return result;
        }

        private static void CollectGraphSystems(ME.BECS.FeaturesGraph.SystemsGraph graph,
                                               HashSet<ME.BECS.FeaturesGraph.SystemsGraph> active, List<GraphSystemInput> result) {
            if (graph == null) throw new InvalidOperationException("Missing nested systems graph.");
            if (!active.Add(graph)) throw new InvalidOperationException("Recursive systems graph: " + graph.name);
            try {
                for (var index = 0; index < graph.nodes.Count; ++index) {
                    var node = graph.nodes[index];
                    if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null) {
                        var type = systemNode.system.GetType();
                        if (type.IsGenericType) {
                            var constraint = EditorUtils.GetFirstInterfaceConstraintType(type.GetGenericTypeDefinition());
                            if (constraint != null) foreach (var component in EditorUtils.GetTypesDerivedFrom(constraint, type))
                                result.Add(new GraphSystemInput(type.GetGenericTypeDefinition().MakeGenericType(component), graph, index, true));
                        } else result.Add(new GraphSystemInput(type, graph, index, false));
                    } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        CollectGraphSystems(graphNode.graphValue, active, result);
                    }
                }
            } finally { active.Remove(graph); }
        }

        private static void Append(StringBuilder output, string kind, List<Type> types) {
            if (types == null) throw new InvalidOperationException("Missing discovery list: " + kind);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < types.Count; ++i) {
                var identity = types[i]?.AssemblyQualifiedName;
                if (string.IsNullOrEmpty(identity) || !seen.Add(identity)) throw new InvalidOperationException("Invalid/duplicate " + kind + " at ordinal " + i);
                // Preserve supplied order, including open generic definitions; specialization
                // selection is a separate stage and must not silently change these ordinals.
                output.Append(kind).Append('\t').Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Encode(identity)).Append('\n');
            }
        }

        private static void Append(StringBuilder output, string kind, Type[] types) {
            if (types == null) throw new InvalidOperationException("Missing discovery list: " + kind);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < types.Length; ++i) {
                var identity = types[i]?.AssemblyQualifiedName;
                if (string.IsNullOrEmpty(identity) || !seen.Add(identity)) throw new InvalidOperationException("Invalid/duplicate " + kind + " at ordinal " + i);
                // Preserve supplied order, including open generic definitions; specialization
                // selection is a separate stage and must not silently change these ordinals.
                output.Append(kind).Append('\t').Append(i.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Encode(identity)).Append('\n');
            }
        }

        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}
