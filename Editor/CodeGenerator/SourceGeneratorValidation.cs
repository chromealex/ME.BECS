namespace ME.BECS.Editor {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;

    internal static class SourceGeneratorValidation {

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Editor Catalogs")]
        private static void CompareEditor() => Compare(true);

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare Runtime Usage")]
        private static void CompareRuntime() => Compare(false);

        private static void Compare(bool editor) {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before comparing catalogs.");
                return;
            }
            try {
                using var sourceGeneratorLookup = SourceGeneratorBridge.BeginLookupScope();
                // Keep an independent legacy baseline, not the catalog-assisted discovery under test.
                Systems.SystemDependenciesCodeGenerator.GetUsedObjects(editor, out var used, useSourceCatalogs: false);
                var report = new StringBuilder("[ME.BECS] Source generator comparison: ")
                    .AppendLine(editor ? "editor discovery" : "runtime usage (loaded Editor assemblies, NOT a player build)");
                var issues = new List<string>();
                var unsupported = new List<string>();
                var summaryAssemblies = 0;
                var summaryMethods = 0;
                var summaryOperations = 0;
                var summaryUnresolved = 0;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (assembly.IsDynamic) continue;
                    var methodIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (System.Reflection.AssemblyMetadataAttribute attribute in assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.MethodSummary.v2") continue;
                        var payload = attribute.Value;
                        var rows = payload?.Split('\n');
                        if (rows == null || rows.Length < 5 || string.IsNullOrEmpty(rows[0])) {
                            issues.Add("Invalid method summary in " + assembly.FullName);
                            continue;
                        }
                        if (!methodIds.Add(rows[0])) issues.Add("Duplicate method summary in " + assembly.FullName + ": " + rows[0]);
                        ++summaryMethods;
                        if (rows[2].Length != 0) ++summaryUnresolved;
                        for (var i = 4; i < rows.Length; ++i) if (rows[i].Length != 0) ++summaryOperations;
                    }
                    if (methodIds.Count != 0) ++summaryAssemblies;
                }
                report.AppendLine($"Semantic method summaries: assemblies={summaryAssemblies}, methods={summaryMethods}, operations={summaryOperations}, unresolved={summaryUnresolved} (raw metadata only; IL analysis NOT replaced)");
                report.Append(SourceGeneratorGraphValidation.Describe(false));
                var destroyAssemblies = EditorUtils.GetAssembliesInfo();
                var destroyTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentDestroy>()
                    .Where(t => t.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, t, destroyAssemblies, true)).ToArray();
                var destroyGenerated = 0;
                foreach (var type in destroyTypes) {
                    if (SourceGeneratorBridge.TryGetDestroyRegistration(type, editor, out _)) ++destroyGenerated;
                    else issues.Add("Manifest destroy callback unavailable: " + Name(type));
                }
                report.AppendLine($"Component destroy callbacks: generated={destroyGenerated}, total={destroyTypes.Length} (callbacks/registration NOT invoked)");
                var maskTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>()
                    .Where(t => t.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, t, destroyAssemblies, true) &&
                                t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Length > 1).ToArray();
                var maskGenerated = 0;
                foreach (var type in maskTypes) {
                    if (SourceGeneratorBridge.TryGetConfigMaskRegistration(type, editor, out _, out var reason)) ++maskGenerated;
                    else issues.Add("Manifest config mask callback unavailable: " + Name(type) + " — " + reason);
                }
                report.AppendLine($"Config mask callbacks: generated={maskGenerated}, total={maskTypes.Length} (field order checked; callbacks/registration NOT invoked)");
                var collectionTypes = UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponent>()
                    .Concat(UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentStatic>())
                    .Concat(UnityEditor.TypeCache.GetTypesDerivedFrom<IConfigComponentShared>())
                    .Where(t => t.IsValueType && EditorUtils.IsValidTypeForAssembly(editor, t, destroyAssemblies, true) &&
                        t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                            .Any(f => typeof(IUnmanagedList).IsAssignableFrom(f.FieldType))).ToArray();
                foreach (var countOnly in new[] { true, false }) {
                    var generated = 0;
                    var label = countOnly ? "Config collection counts" : "Config collection callbacks";
                    foreach (var type in collectionTypes) {
                        if (SourceGeneratorBridge.TryGetConfigCollectionsRegistration(type, countOnly, editor, out _, out var reason)) ++generated;
                        else issues.Add(label + " manifest unavailable: " + Name(type) + " — " + reason);
                    }
                    report.AppendLine($"{label}: generated={generated}, total={collectionTypes.Length} (metadata checked; callbacks/registration NOT invoked)");
                }
                try {
                    report.Append(Jobs.JobsEarlyInitCodeGenerator.CompareEarlyInit(used.jobTypes, editor, out var unavailableEarlyInit));
                    if (unavailableEarlyInit != 0) issues.Add("Source-generated EarlyInit coverage/selection issues: " + unavailableEarlyInit + "; legacy fallback is disabled. See EarlyInit diagnostics.");
                } catch (Exception exception) {
                    issues.Add("Job EarlyInit comparison incomplete: " + exception.GetBaseException().Message);
                }
                if (editor) {
                    Systems.SystemDependenciesCodeGenerator.GetUsedObjects(true, out var assisted, useSourceCatalogs: true);
                    if (!used.components.SequenceEqual(assisted.components)) issues.Add("Catalog-assisted component discovery differs in content or order from legacy.");
                    if (!used.aspects.SequenceEqual(assisted.aspects)) issues.Add("Catalog-assisted aspect discovery differs in content or order from legacy.");
                    if (!used.componentsGroup.SequenceEqual(assisted.componentsGroup)) issues.Add("Catalog-assisted component groups differ in content or order from legacy.");
                }
                var components = new HashSet<Type>();
                var aspects = new HashSet<Type>();
                var systems = new HashSet<Type>();
                var entityTypes = new HashSet<Type>();
                var catalogs = new Dictionary<Assembly, Type>();
                // Include catalogs with types absent from legacy discovery as well.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal)) {
                    var catalog = SourceGeneratorBridge.GetCatalog(assembly);
                    if (catalog == null) continue;
                    catalogs.Add(assembly, catalog);
                    ReadCatalog(catalog, "GetComponents", typeof(IComponentBase), components, issues);
                    ReadCatalog(catalog, "GetAspects", typeof(IAspect), aspects, issues);
                    ReadCatalog(catalog, "GetSystems", typeof(ISystem), systems, issues);
                    ReadCatalog(catalog, "GetEntityTypes", typeof(IEntityType), entityTypes, issues);
                }
                CompareTypes("component", used.components, components, catalogs, issues, unsupported);
                CompareTypes("aspect", used.aspects, aspects, catalogs, issues, unsupported);
                CompareTypes("system", used.systems, systems, catalogs, issues, unsupported);
                CompareTypes("entity type", used.entityTypes, entityTypes, catalogs, issues, unsupported);
                var entityGenerator = new EntityTypeCodeGenerator { entityTypes = used.entityTypes, editorAssembly = editor, asms = EditorUtils.GetAssembliesInfo() };
                var selectedEntityTypes = EntityTypeCodeGenerator.GetAllTypes(entityGenerator, out var entityCount);
                var generatedEntityTypes = 0;
                foreach (var item in selectedEntityTypes) {
                    if (SourceGeneratorBridge.TryGetEntityTypeRegistration(item.Item1, item.Item2, out _)) ++generatedEntityTypes;
                    else unsupported.Add("Entity type registration uses legacy: " + Name(item.Item1));
                }
                report.AppendLine($"Entity type registrations: generated={generatedEntityTypes}, total={entityCount}; catalog={entityTypes.Count} (registration NOT invoked)");
                var openSystems = used.systems.Where(t => t.ContainsGenericParameters).Select(Name).ToList();
                Append(report, "Open generic system definitions (specializations checked separately)", openSystems);
                foreach (var definition in used.systems.Where(t => t.IsGenericTypeDefinition)) {
                    var constraint = EditorUtils.GetFirstInterfaceConstraintType(definition);
                    if (constraint == null) continue;
                    var variants = EditorUtils.GetTypesDerivedFrom(constraint, definition);
                    var mode = definition.GetCustomAttribute<SystemGenericParallelModeAttribute>() != null ? "parallel" : "sequential";
                    report.AppendLine($"Generic variants: {Name(definition)}, components={variants.Length}, mode={mode}");
                }
                // Check actual closed specializations selected by the same bootstrap expansion.
                var expandedSystems = new List<Type>(used.systems);
                try {
                    CodeGenerator.PatchSystemsList(expandedSystems);
                    var closedGenericSystems = expandedSystems.Where(t => t.IsGenericType && !t.ContainsGenericParameters).ToArray();
                    var supportedGenericSystems = 0;
                    foreach (var type in closedGenericSystems) {
                        if (SourceGeneratorBridge.TryGetSystemRegistration(type, out _)) ++supportedGenericSystems;
                        else unsupported.Add("Generic system registration uses legacy: " + Name(type));
                    }
                    report.AppendLine($"Closed generic system registrations: generated={supportedGenericSystems}, total={closedGenericSystems.Length}");
                    var phases = new[] { "Awake", "Start", "Update", "Destroy", "DrawGizmos" };
                    var contracts = new[] { typeof(IAwake), typeof(IStart), typeof(IUpdate), typeof(IDestroy), typeof(IDrawGizmos) };
                    var lifecycleTotal = 0;
                    var lifecycleGenerated = 0;
                    var pointerTotal = 0;
                    var pointerGenerated = 0;
                    foreach (var type in expandedSystems.Where(t => t.IsVisible && !t.ContainsGenericParameters)) {
                        for (var phase = 0; phase < phases.Length; ++phase) {
                            if (!contracts[phase].IsAssignableFrom(type)) continue;
                            ++lifecycleTotal;
                            if (SourceGeneratorBridge.TryGetSystemLifecycleAot(type, phases[phase], out _)) ++lifecycleGenerated;
                            else unsupported.Add("Lifecycle AOT uses legacy: " + Name(type) + ".On" + phases[phase]);
                            foreach (var kind in new[] { "Burst", "NoBurst", "Factory" }) {
                                ++pointerTotal;
                                if (SourceGeneratorBridge.TryGetSystemPointerAot(type, phases[phase], kind, out _)) ++pointerGenerated;
                                else unsupported.Add("Pointer AOT wrapper unavailable: " + Name(type) + "." + kind + phases[phase]);
                            }
                        }
                    }
                    report.AppendLine($"Direct lifecycle AOT calls: generated={lifecycleGenerated}, total={lifecycleTotal} (methods NOT invoked)");
                    report.AppendLine($"Pointer AOT wrappers available: generated={pointerGenerated}, total={pointerTotal} (availability only, NOT Burst execution/stripping validation)");
                } catch (Exception exception) {
                    issues.Add("Legacy generic system expansion failed: " + exception.GetBaseException().Message);
                }
                foreach (var type in used.systems.Where(systems.Contains)) {
                    if (!SourceGeneratorBridge.TryGetSystemRegistration(type, out _)) issues.Add("System registration bridge falls back: " + Name(type));
                }
                foreach (var type in used.aspects.Where(aspects.Contains)) {
                    if (!SourceGeneratorBridge.TryGetAspectRegistration(type, out _)) issues.Add("Aspect registration bridge falls back: " + Name(type));
                    var hasQuery = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(f => typeof(IAspectData).IsAssignableFrom(f.FieldType) && f.GetCustomAttribute<QueryWithAttribute>() != null);
                    if (hasQuery && !SourceGeneratorBridge.TryGetAspectQuery(type, out _, out _)) {
                        issues.Add("Aspect QueryWith falls back (unsupported, unavailable, or field order mismatch): " + Name(type));
                    }
                    var hasData = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(f => typeof(IAspectData).IsAssignableFrom(f.FieldType));
                    if (hasData && !SourceGeneratorBridge.TryGetAspectConstruction(type, out _, out _, out var constructionReason)) {
                        unsupported.Add("Aspect construction uses legacy: " + Name(type) + " — " + constructionReason);
                    }
                }
                foreach (var type in used.componentsGroup.Where(components.Contains)) {
                    var group = type.GetCustomAttribute<ComponentGroupAttribute>();
                    if (group != null && !SourceGeneratorBridge.TryGetGroupRegistration(type, group.groupType, out _)) {
                        issues.Add("Group registration bridge falls back: " + Name(type));
                    }
                }
                foreach (var type in used.components.Where(components.Contains)) {
                    try {
                        if (!SourceGeneratorBridge.TryGetAot(type, "Component", out _)) issues.Add("Component AOT bridge falls back: " + Name(type));
                        if (typeof(IComponentShared).IsAssignableFrom(type) && !SourceGeneratorBridge.TryGetAot(type, "Shared", out _)) issues.Add("Shared AOT bridge falls back: " + Name(type));
                        if (typeof(IConfigComponentStatic).IsAssignableFrom(type) && !SourceGeneratorBridge.TryGetAot(type, "Static", out _)) issues.Add("Static AOT bridge falls back: " + Name(type));
                        if (typeof(IConfigInitialize).IsAssignableFrom(type)) {
                            if (!SourceGeneratorBridge.TryGetConfigRegistration(type, false, false, out _)) issues.Add("Config registration bridge falls back: " + Name(type));
                            if (!SourceGeneratorBridge.TryGetAot(type, "Config", out _)) issues.Add("Config AOT bridge falls back: " + Name(type));
                        }
                        var catalog = catalogs[type.Assembly];
                        var metadata = catalog.GetMethod("GetRegistrationFlags", BindingFlags.Public | BindingFlags.Static,
                            null, new[] { typeof(Type) }, null);
                        if (metadata == null || metadata.ReturnType != typeof(int)) {
                            issues.Add("Metadata unavailable (rebuild/reimport analyzer): " + Name(type));
                            continue;
                        }
                        var actual = (int)metadata.Invoke(null, new object[] { type });
                        var tag = Marshal.SizeOf(type) <= 1 && type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;
                        var hasDefault = !tag && type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static) != null;
                        var special = typeof(IComponentShared).IsAssignableFrom(type) || typeof(IConfigComponentStatic).IsAssignableFrom(type);
                        var ordinary = !special && typeof(IComponent).IsAssignableFrom(type);
                        var expected = (ordinary ? 1 : 0) | (tag ? 2 : 0) | (hasDefault ? 4 : 0);
                        if (actual != expected) issues.Add($"Registration flags: {Name(type)} legacy={expected}, generated={actual} (1=registration, 2=tag, 4=default)");
                        if (ordinary && !SourceGeneratorBridge.TryGetRegistration(type, tag, false, out _)) {
                            issues.Add("Registration bridge falls back: " + Name(type));
                        }
                        if (typeof(IComponentShared).IsAssignableFrom(type) && !SourceGeneratorBridge.TryGetSpecialRegistration(type, true, tag, false, out _)) issues.Add("Shared registration bridge falls back: " + Name(type));
                        if (typeof(IConfigComponentStatic).IsAssignableFrom(type) && !SourceGeneratorBridge.TryGetSpecialRegistration(type, false, tag, false, out _)) issues.Add("Static registration bridge falls back: " + Name(type));
                    } catch (Exception exception) {
                        issues.Add("Cannot compare registration: " + Name(type) + " — " + exception.GetBaseException().Message);
                    }
                }
                var extraComponents = components.Except(used.components).Select(Name).ToArray();
                var extraAspects = aspects.Except(used.aspects).Select(Name).ToArray();
                var extraSystems = systems.Except(used.systems).Select(Name).ToArray();
                var extraEntityTypes = entityTypes.Except(used.entityTypes).Select(Name).ToArray();
                report.AppendLine($"Systems: legacy={used.systems.Count}, catalog={systems.Count}");
                report.AppendLine($"Catalogs={catalogs.Count}; components: legacy={used.components.Count}, catalog={components.Count}; aspects: legacy={used.aspects.Count}, catalog={aspects.Count}");
                if (editor) {
                    issues.AddRange(extraComponents.Select(t => "Catalog-only component: " + t));
                    issues.AddRange(extraAspects.Select(t => "Catalog-only aspect: " + t));
                    issues.AddRange(extraSystems.Select(t => "Catalog-only system: " + t));
                    issues.AddRange(extraEntityTypes.Select(t => "Catalog-only entity type: " + t));
                } else {
                    report.AppendLine($"Outside runtime usage (expected, not errors): components={extraComponents.Length}, aspects={extraAspects.Length}");
                    report.AppendLine($"Systems outside runtime usage (expected): {extraSystems.Length}");
                    report.AppendLine($"Entity types outside runtime usage (expected): {extraEntityTypes.Length}");
                }
                Append(report, "Unsupported by current generator (legacy fallback)", unsupported);
                Append(report, "Differences / unavailable catalogs", issues);
                report.AppendLine("Registration methods and Default getters were NOT invoked. Discovery and ID ordering were NOT changed.");
                if (issues.Count > 0) UnityEngine.Debug.LogWarning(report.ToString());
                else UnityEngine.Debug.Log(report.ToString());
            } catch (Exception exception) {
                UnityEngine.Debug.LogError("[ME.BECS] Catalog comparison failed; no successful comparison result: " + exception);
            }
        }

        private static void ReadCatalog(Type catalog, string methodName, Type contract, HashSet<Type> target, List<string> issues) {
            try {
                var method = catalog.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(Type[])) throw new InvalidOperationException("Missing Type[] " + methodName);
                var entries = (Type[])method.Invoke(null, null);
                if (entries == null) throw new InvalidOperationException("Null catalog");
                foreach (var entry in entries) {
                    if (entry == null || entry.Assembly != catalog.Assembly || !contract.IsAssignableFrom(entry)) {
                        issues.Add("Invalid " + methodName + " entry in " + catalog.Assembly.GetName().Name + ": " + (entry == null ? "null" : Name(entry)));
                    } else if (!target.Add(entry)) issues.Add("Duplicate " + methodName + " entry: " + Name(entry));
                }
            } catch (Exception exception) {
                issues.Add(catalog.Assembly.GetName().Name + "." + methodName + ": " + exception.GetBaseException().Message);
            }
        }

        private static void CompareTypes(string kind, IEnumerable<Type> legacy, HashSet<Type> generated,
            Dictionary<Assembly, Type> catalogs, List<string> issues, List<string> unsupported) {
            foreach (var type in legacy) {
                if (kind == "system" && type.ContainsGenericParameters) continue;
                var reason = UnsupportedReason(type, kind == "component");
                if (reason != null) unsupported.Add(kind + " " + Name(type) + " — " + reason);
                else if (!generated.Contains(type)) issues.Add("Missing " + kind + ": " + Name(type) +
                    (catalogs.ContainsKey(type.Assembly) ? " (catalog present)" : " (assembly catalog unavailable)"));
            }
        }

        private static string UnsupportedReason(Type type, bool component) {
            if (!type.IsValueType || type.IsEnum || !type.IsVisible || type.IsByRefLike) return "not a public ordinary struct";
            for (var owner = type; owner != null; owner = owner.DeclaringType) {
                if (owner.IsGenericType) return "generic type or declaring type";
            }
            if (!IsUnmanaged(type, new HashSet<Type>())) return "managed fields";
            return null;
        }

        private static bool IsUnmanaged(Type type, HashSet<Type> visited) {
            if (type.IsPointer || type.IsPrimitive || type.IsEnum) return true;
            if (!type.IsValueType) return false;
            if (!visited.Add(type)) return true;
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(field => IsUnmanaged(field.FieldType, visited));
        }

        private static string Name(Type type) => type.FullName + " [" + type.Assembly.GetName().Name + "]";

        private static void Append(StringBuilder report, string title, List<string> entries) {
            report.AppendLine(title + ": " + entries.Count);
            foreach (var entry in entries.OrderBy(t => t, StringComparer.Ordinal)) report.Append("  ").AppendLine(entry);
        }

    }
}
