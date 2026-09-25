namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    public static class SourceGeneratorInputManifest {
        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Editor Type Inputs")]
        private static void ExportEditor() => Export(true);

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Export Runtime Type Inputs")]
        private static void ExportRuntime() => Export(false);

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

        internal static string Serialize(string targetAssembly, bool editor, Systems.SystemDependenciesCodeGenerator.UsedObjects used, bool registerGraphReferences = false) {
            if (string.IsNullOrWhiteSpace(targetAssembly)) throw new ArgumentException("Target assembly is required.", nameof(targetAssembly));
            var result = new StringBuilder("ME.BECS.TypeInputs.v3\t").Append(Encode(targetAssembly))
                .Append('\t').Append(editor ? "editor" : "runtime").Append('\n');
            Append(result, "system", used.systems);
            var selectedSystems = new List<Type>(used.systems);
            CodeGenerator.PatchSystemsList(selectedSystems);
            var assemblies = EditorUtils.GetAssembliesInfo();
            selectedSystems.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true));
            Append(result, "system-registration", selectedSystems);
            var injectionOrdinal = 0;
            foreach (var system in selectedSystems) {
                foreach (var field in system.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)) {
                    if (field.IsInitOnly || !field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(InjectSystem<>)) continue;
                    if (!field.FieldType.GenericTypeArguments[0].IsVisible) continue;
                    result.Append("system-injection\t").Append((injectionOrdinal++).ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Encode(system.AssemblyQualifiedName)).Append('\t').Append(Encode(field.Name)).Append('\n');
                }
            }
            Append(result, "component", used.components);
            var componentOrdinal = 0;
            foreach (var component in used.components) {
                if (!component.IsValueType || !EditorUtils.IsValidTypeForAssembly(editor, component, assemblies, true)) continue;
                var tag = CodeGenerator.IsTagType(component);
                var shared = typeof(IComponentShared).IsAssignableFrom(component);
                var flags = (tag ? 1 : 0) | (CodeGenerator.IsStaticType(component) ? 2 : 0) |
                    (!tag && component.GetProperty("Default", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public) != null ? 4 : 0) |
                    (shared ? 8 : 0) | (shared && CodeGenerator.HasComponentCustomSharedHash(component) ? 16 : 0) |
                    (typeof(IConfigInitialize).IsAssignableFrom(component) ? 32 : 0);
                result.Append("component-registration\t").Append((componentOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\t')
                    .Append(flags.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            Append(result, "component-group", used.componentsGroup);
            result.Append("destroy-schema\t0\t").Append(Encode("v1")).Append('\n');
            Append(result, "destroy-registration", ComponentDestroyCodeGenerator.GetSelectedComponents(editor, assemblies));
            result.Append("config-mask-schema\t0\t").Append(Encode("v1")).Append('\n');
            var maskOrdinal = 0;
            result.Append("config-collection-count-schema\t0\t").Append(Encode("v1")).Append('\n');
            result.Append("config-collection-callback-schema\t0\t").Append(Encode("v1")).Append('\n');
            var collectionCountOrdinal = 0;
            foreach (var component in Aspects.EntityConfigCodeGenerator.GetCollectionComponents(editor, assemblies)) {
                var ordinal = (collectionCountOrdinal++).ToString(CultureInfo.InvariantCulture);
                result.Append("config-collection-count\t").Append(ordinal)
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\t')
                    .Append(Aspects.EntityConfigCodeGenerator.GetCollectionsCount(component).ToString(CultureInfo.InvariantCulture)).Append('\n');
                var collectionFields = Aspects.EntityConfigCodeGenerator.GetCollectionFields(component);
                result.Append("config-collection-callback\t").Append(ordinal)
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\t')
                    .Append(Encode(string.Join(",", System.Array.ConvertAll(collectionFields, field => field.Name)))).Append('\n');
            }
            foreach (var component in Aspects.EntityConfigCodeGenerator.GetMaskComponents(editor, assemblies)) {
                var fields = component.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                result.Append("config-mask-registration\t").Append((maskOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\t')
                    .Append(Encode(string.Join(",", System.Array.ConvertAll(fields, field => field.Name)))).Append('\n');
            }
            var groupOrdinal = 0;
            foreach (var component in used.componentsGroup) {
                if (!EditorUtils.IsValidTypeForAssembly(editor, component, assemblies, true)) continue;
                var attribute = (ComponentGroupAttribute)Attribute.GetCustomAttribute(component, typeof(ComponentGroupAttribute));
                if (attribute?.groupType == null) throw new InvalidOperationException("Missing component group: " + component.AssemblyQualifiedName);
                result.Append("group-registration\t").Append((groupOrdinal++).ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Encode(component.AssemblyQualifiedName)).Append('\t')
                    .Append(Encode(attribute.groupType.AssemblyQualifiedName)).Append('\n');
            }
            Append(result, "job", used.jobTypes);
            Append(result, "entity", used.entityTypes);
            var entitySelector = new EntityTypeCodeGenerator {
                editorAssembly = editor, entityTypes = used.entityTypes, asms = assemblies,
            };
            var registrations = EntityTypeCodeGenerator.GetAllTypes(entitySelector, out _);
            var selectedEntities = new List<Type>(registrations.Length);
            foreach (var registration in registrations) selectedEntities.Add(registration.Item1);
            Append(result, "entity-registration", selectedEntities);
            Append(result, "aspect", used.aspects);
            var selectedAspects = new List<Type>(used.aspects);
            selectedAspects.RemoveAll(type => !type.IsValueType || !type.IsVisible ||
                !EditorUtils.IsValidTypeForAssembly(editor, type, assemblies, true));
            var constructedAspects = new List<Type>();
            foreach (var aspect in selectedAspects) {
                var hasQuery = false;
                var hasConstruction = false;
                foreach (var field in aspect.GetFields(System.Reflection.BindingFlags.Instance |
                             System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) {
                    if (!typeof(IAspectData).IsAssignableFrom(field.FieldType)) continue;
                    hasConstruction = true;
                    if (Attribute.IsDefined(field, typeof(QueryWithAttribute))) hasQuery = true;
                }
                // The bridge compares the generated metadata getter against reflection field order;
                // it does not invoke initialization or access runtime component IDs.
                if (hasQuery && !SourceGeneratorBridge.TryGetAspectQuery(aspect, out _, out _))
                    throw new InvalidOperationException("Aspect query catalog is unavailable or differs in field order: " + aspect.AssemblyQualifiedName);
                if (hasConstruction) {
                    if (!SourceGeneratorBridge.TryGetAspectConstruction(aspect, out _, out _, out var reason))
                        throw new InvalidOperationException("Aspect construction catalog is incompatible: " + aspect.AssemblyQualifiedName + "; " + reason);
                    constructedAspects.Add(aspect);
                }
            }
            Append(result, "aspect-registration", selectedAspects);
            Append(result, "aspect-construction", constructedAspects);
            if (!editor) {
                var graphOrdinal = 0;
                var slotOrdinal = 0;
                var deltaJobs = new HashSet<Type>();
                var analyzedGraphSystems = new Dictionary<Type, HashSet<Type>>();
                var graphJobOrdinal = 0;
                var graphInjectionOrdinal = 0;
                var graphApplyOrdinal = 0;
                var topologyOrdinal = 0;
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph")) {
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<ME.BECS.FeaturesGraph.SystemsGraph>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (graph.isInnerGraph) continue;
                    var id = graph.GetId();
                    if (id == int.MinValue) throw new InvalidOperationException("Graph ID cannot be represented in callback names: " + guid);
                    var layout = GetGraphSystems(graph);
                    var scheduledJobs = new HashSet<Type>();
                    foreach (var item in layout) {
                        if (!analyzedGraphSystems.TryGetValue(item.type, out var jobs)) {
                            jobs = new HashSet<Type>();
                            SourceGeneratorScheduledJobs.Collect(item.type, jobs);
                            analyzedGraphSystems.Add(item.type, jobs);
                        }
                        scheduledJobs.UnionWith(jobs);
                    }
                    var orderedJobs = new List<Type>(scheduledJobs);
                    orderedJobs.Sort((left, right) => StringComparer.Ordinal.Compare(left.AssemblyQualifiedName, right.AssemblyQualifiedName));
                    foreach (var job in orderedJobs) {
                        if (!TryGetDeltaTimeFields(job, out var deltaFields) || !deltaJobs.Add(job)) continue;
                        result.Append("job-delta-registration\t").Append((deltaJobs.Count - 1).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(job.AssemblyQualifiedName)).Append('\t').Append(Encode(string.Join(",", deltaFields))).Append('\n');
                    }
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
                    foreach (var job in orderedJobs) {
                        if (!TryGetGraphJobFields(job, layout, out var patchFields)) continue;
                        result.Append("graph-job\t").Append((graphJobOrdinal++).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(job.AssemblyQualifiedName)).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(string.Join(",", patchFields))).Append('\n');
                    }
                    var injectionOwners = new HashSet<Type>();
                    foreach (var item in layout) {
                        if (!injectionOwners.Add(item.type) || !TryGetGraphSystemFields(item.type, layout, out var patchFields)) continue;
                        result.Append("graph-system-injection\t").Append((graphInjectionOrdinal++).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(item.type.AssemblyQualifiedName)).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(string.Join(",", patchFields))).Append('\n');
                    }
                    if (TryGetGraphApplyPlan(graph, out var actions, layout, analyzedGraphSystems))
                        result.Append("graph-apply\t").Append((graphApplyOrdinal++).ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode("apply")).Append('\t').Append(id.ToString(CultureInfo.InvariantCulture))
                            .Append('\t').Append(Encode(string.Join(",", actions))).Append('\n');
                }
            }
            var payload = result.ToString();
            var recordCount = 0;
            foreach (var character in payload) if (character == '\n') ++recordCount;
            return payload + "end\t" + (recordCount - 1).ToString(CultureInfo.InvariantCulture) + "\t" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(payload) + "\n";
        }

        public static bool TryGetGraphApplyPlan(ME.BECS.FeaturesGraph.SystemsGraph graph, out string[] actions,
            List<GraphSystemInput> layout = null, Dictionary<Type, HashSet<Type>> discovered = null) {
            actions = null;
            layout ??= GetGraphSystems(graph);
            discovered ??= new Dictionary<Type, HashSet<Type>>();
            var result = new List<string>();
            var seen = new HashSet<Type>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var item in layout) {
                if (!seen.Add(item.type)) continue;
                var hasInjection = false;
                foreach (var field in item.type.GetFields(flags)) hasInjection |= typeof(IInject).IsAssignableFrom(field.FieldType);
                if (hasInjection) {
                    if (!TryGetGraphSystemFields(item.type, layout, out _)) return false;
                    result.Add("s:" + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(item.type.AssemblyQualifiedName));
                }
                if (!discovered.TryGetValue(item.type, out var jobs)) {
                    jobs = new HashSet<Type>();
                    SourceGeneratorScheduledJobs.Collect(item.type, jobs);
                    discovered.Add(item.type, jobs);
                }
                var ordered = new List<Type>(jobs);
                ordered.Sort((a, b) => { var order = StringComparer.Ordinal.Compare(a.FullName, b.FullName); return order != 0 ? order : StringComparer.Ordinal.Compare(a.Assembly.FullName, b.Assembly.FullName); });
                foreach (var job in ordered) {
                    if (!job.IsVisible) continue;
                    hasInjection = false;
                    foreach (var field in job.GetFields(flags)) hasInjection |= typeof(IInject).IsAssignableFrom(field.FieldType) || Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute));
                    if (!hasInjection) continue;
                    var kind = TryGetGraphJobFields(job, layout, out _) ? "j:" : TryGetDeltaTimeFields(job, out _) ? "d:" : null;
                    if (kind == null) return false;
                    result.Add(kind + ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(job.AssemblyQualifiedName));
                }
            }
            actions = result.ToArray();
            return true;
        }

        public static bool TryGetGraphSystemFields(Type system, List<GraphSystemInput> layout, out string[] plan) {
            plan = null;
            if (!typeof(ISystem).IsAssignableFrom(system)) return false;
            foreach (var field in system.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute))) return false;
            return TryGetGraphJobFields(system, layout, out plan);
        }

        public static bool TryGetGraphJobFields(Type job, List<GraphSystemInput> layout, out string[] plan) {
            plan = null;
            if (!job.IsVisible || job.ContainsGenericParameters) return false;
            var fields = new List<string>();
            var hasSystem = false;
            foreach (var field in job.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) {
                if (field.FieldType == typeof(bool)) return false;
                var injected = typeof(IInject).IsAssignableFrom(field.FieldType);
                var delta = Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute));
                if (!injected && !delta) continue;
                if (field.IsInitOnly || (injected && delta)) return false;
                if (!field.IsPublic && (injected ? !TryGetPartialInjectionMethod(field, out _) : !TryGetPartialDeltaTimeMethod(field, out _))) return false;
                if (injected) {
                    if (!field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(InjectSystem<>)) return false;
                    var target = field.FieldType.GenericTypeArguments[0];
                    var index = layout.FindIndex(item => item.type == target);
                    if (index < 0) return false;
                    fields.Add(field.Name + ":" + index.ToString(CultureInfo.InvariantCulture));
                    hasSystem = true;
                } else {
                    if (field.FieldType != typeof(uint) && field.FieldType != typeof(float) && field.FieldType != typeof(sfloat)) return false;
                    fields.Add(field.Name + ":d");
                }
            }
            if (!hasSystem) return false;
            plan = fields.ToArray();
            return true;
        }

        public static bool TryGetDeltaTimeFields(Type job, out string[] names) {
            names = null;
            if (!job.IsVisible || job.ContainsGenericParameters) return false;
            var fields = new List<string>();
            foreach (var field in job.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) {
                if (field.FieldType == typeof(bool) || typeof(IInject).IsAssignableFrom(field.FieldType)) return false;
                if (!Attribute.IsDefined(field, typeof(InjectDeltaTimeAttribute))) continue;
                if ((!field.IsPublic && !TryGetPartialDeltaTimeMethod(field, out _)) || field.IsInitOnly ||
                    (field.FieldType != typeof(uint) && field.FieldType != typeof(float) && field.FieldType != typeof(sfloat))) return false;
                fields.Add(field.Name);
            }
            if (fields.Count == 0) return false;
            names = fields.ToArray();
            return true;
        }

        public static bool TryGetJobDeltaTimeRegistration(Type job, out string call) {
            call = null;
            if (!TryGetDeltaTimeFields(job, out _)) return false;
            call = "global::ME.BECS.SourceGenerated.GenericJobDeltaInputs.Register_" +
                ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(job.AssemblyQualifiedName) + "();";
            return true;
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
