namespace ME.BECS.Editor {

    using System;
    using System.Reflection;
    using System.Text;
    using System.Collections.Generic;
    using System.Linq;

    // Discovery only: never invoke registration while generating the bootstrap.
    internal static class SourceGeneratorBridge {

        [ThreadStatic] private static LookupScope lookup;

        internal sealed class LookupScope : IDisposable {
            private readonly LookupScope previous;
            private bool disposed;
            internal readonly Dictionary<(Assembly, bool), Type> catalogs = new Dictionary<(Assembly, bool), Type>();
            internal readonly Dictionary<string, string> encoded = new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly Dictionary<(Type, string), MethodInfo> methods = new Dictionary<(Type, string), MethodInfo>();
            internal readonly Dictionary<(Type, Type), Type[]> genericComponents = new Dictionary<(Type, Type), Type[]>();
            internal readonly Dictionary<Type, Type[]> derivedTypes = new Dictionary<Type, Type[]>();
            internal LookupScope() { this.previous = lookup; lookup = this; }
            public void Dispose() {
                if (this.disposed) return;
                if (lookup != this) throw new InvalidOperationException("Source generator lookup scopes must be disposed on their owning thread in reverse order.");
                lookup = this.previous;
                this.catalogs.Clear();
                this.encoded.Clear();
                this.methods.Clear();
                this.genericComponents.Clear();
                this.derivedTypes.Clear();
                this.disposed = true;
            }
        }

        internal static LookupScope BeginLookupScope() => new LookupScope();

        internal static bool TryGetConfigCollectionsRegistration(Type component, bool countOnly, out string call, out string reason) {
            call = null;
            reason = "generated method unavailable";
            if (!component.IsVisible || component.IsGenericType) return false;
            var catalog = component.Assembly.GetType("ME.BECS.SourceGenerated.ConfigCollections_" + Encode(component.Assembly.GetName().Name), false);
            if (catalog == null) return false;
            var key = Encode("global::" + component.FullName.Replace('+', '.'));
            var methodName = (countOnly ? "RegisterCount_" : "Register_") + key;
            var register = FindMethod(catalog, methodName, Type.EmptyTypes);
            if (register == null || register.ReturnType != typeof(void)) return false;
            var fields = component.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => typeof(IUnmanagedList).IsAssignableFrom(f.FieldType)).ToArray();
            if (countOnly) {
                var metadata = FindMethod(catalog, "Count_" + key, Type.EmptyTypes);
                if (metadata == null || metadata.ReturnType != typeof(uint) || (uint)metadata.Invoke(null, null) != (uint)fields.Length) {
                    reason = "collection count mismatch";
                    return false;
                }
            } else {
                var metadata = FindMethod(catalog, "Fields_" + key, Type.EmptyTypes);
                if (metadata == null || metadata.ReturnType != typeof(string[])) return false;
                var expected = fields.OrderBy(f => f.FieldType.FullName)
                    .Where(f => f.FieldType.GenericTypeArguments.Length > 0 && f.FieldType.GenericTypeArguments[0].IsVisible)
                    .Select(f => f.Name).ToArray();
                var actual = metadata.Invoke(null, null) as string[];
                if (actual == null || !expected.SequenceEqual(actual, StringComparer.Ordinal)) {
                    reason = "collection field order mismatch";
                    return false;
                }
            }
            call = "global::" + catalog.FullName + "." + methodName + "();";
            reason = null;
            return true;
        }

        internal static bool TryGetConfigMaskRegistration(Type component, out string call, out string reason) {
            call = null;
            reason = "generated callback unavailable";
            if (!component.IsVisible || component.IsGenericType || !typeof(IConfigComponent).IsAssignableFrom(component)) return false;
            var catalog = component.Assembly.GetType("ME.BECS.SourceGenerated.ConfigMask_" + Encode(component.Assembly.GetName().Name), false);
            if (catalog == null) return false;
            var key = Encode("global::" + component.FullName.Replace('+', '.'));
            var register = FindMethod(catalog, "Register_" + key, Type.EmptyTypes);
            var metadata = FindMethod(catalog, "Fields_" + key, Type.EmptyTypes);
            if (register == null || register.ReturnType != typeof(void) || metadata == null || metadata.ReturnType != typeof(string[])) return false;
            // Only pure field metadata is invoked, never registration or the Burst callback.
            var expected = component.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).ToArray();
            var actual = metadata.Invoke(null, null) as string[];
            if (actual == null || !expected.SequenceEqual(actual, StringComparer.Ordinal)) {
                reason = "field order mismatch";
                return false;
            }
            call = "global::" + catalog.FullName + ".Register_" + key + "();";
            reason = null;
            return true;
        }

        internal static bool TryGetDestroyRegistration(Type component, out string call) {
            call = null;
            if (!component.IsVisible || component.IsGenericType || !typeof(IComponentDestroy).IsAssignableFrom(component)) return false;
            var catalog = component.Assembly.GetType("ME.BECS.SourceGenerated.ComponentDestroy_" + Encode(component.Assembly.GetName().Name), false);
            if (catalog == null) return false;
            var name = "Register_" + Encode("global::" + component.FullName.Replace('+', '.'));
            var method = FindMethod(catalog, name, Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(void)) return false;
            call = "global::" + catalog.FullName + "." + name + "();";
            return true;
        }

        internal static Type[] GetDerivedTypesSnapshot(Type contract) {
            if (lookup != null && lookup.derivedTypes.TryGetValue(contract, out var snapshot)) return (Type[])snapshot.Clone();
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom(contract).ToArray();
            if (lookup != null) lookup.derivedTypes[contract] = (Type[])types.Clone();
            return types;
        }

        internal static string ResolveJobEarlyInit(Type job, string legacyCall) {
            return ResolveJobEarlyInit(job, legacyCall, out _);
        }

        internal static string ResolveJobEarlyInit(Type job, string legacyCall, out string reason) {
            reason = "not an EarlyInit call, non-public job, or open generic job";
            if (!legacyCall.StartsWith("EarlyInit.", StringComparison.Ordinal) || !job.IsVisible || job.ContainsGenericParameters) return legacyCall;
            var catalog = job.Assembly.GetType("ME.BECS.SourceGenerated.JobEarlyInit_" + Encode(job.Assembly.GetName().Name), false);
            if (catalog == null) { reason = "job EarlyInit catalog unavailable"; return legacyCall; }
            if (job.IsGenericType) return ResolveGenericJobEarlyInit(job, catalog, legacyCall, out reason);
            var name = "Init_" + HashEarlyInitCall(legacyCall);
            var method = FindMethod(catalog, name, Type.EmptyTypes);
            reason = method == null ? "wrapper for exact legacy call missing (unsupported signature or spelling mismatch)" : "wrapper return type differs";
            if (method == null || method.ReturnType != typeof(void)) return legacyCall;
            reason = null;
            return "global::" + catalog.FullName + "." + name + "();";
        }

        private static string ResolveGenericJobEarlyInit(Type job, Type catalog, string legacyCall, out string reason) {
            reason = "invalid legacy EarlyInit call";
            var end = legacyCall.IndexOf('<');
            if (end < 0) return legacyCall;
            var earlyMethod = legacyCall.Substring("EarlyInit.".Length, end - "EarlyInit.".Length);
            var key = HashEarlyInitCall(job.GetGenericTypeDefinition().FullName + "|" + earlyMethod);
            var init = FindMethod(catalog, "InitGeneric_" + key, Type.EmptyTypes);
            var metadata = FindMethod(catalog, "ArgsGeneric_" + key, Type.EmptyTypes);
            var arguments = job.GetGenericArguments();
            reason = "generic wrapper/metadata absent or signature incompatible (unsupported constraints, parameter count, or ambiguous overload)";
            if (init == null || metadata == null || !init.IsGenericMethodDefinition || !metadata.IsGenericMethodDefinition ||
                init.ReturnType != typeof(void) || metadata.ReturnType != typeof(Type[]) ||
                init.GetGenericArguments().Length != arguments.Length || metadata.GetGenericArguments().Length != arguments.Length) return legacyCall;
            Type[] actual;
            try {
                init.MakeGenericMethod(arguments);
                actual = (Type[])metadata.MakeGenericMethod(arguments).Invoke(null, null);
            } catch (ArgumentException exception) { reason = "generic constraint mismatch: " + exception.Message; return legacyCall; }
              catch (TargetInvocationException exception) { reason = "metadata failed: " + exception.GetBaseException().Message; return legacyCall; }
            if (actual == null || actual.Length == 0 || actual[0] != job) { reason = "metadata job type mismatch"; return legacyCall; }
            var expected = "EarlyInit." + earlyMethod + "<" + EditorUtils.GetTypeName(job) +
                (actual.Length > 1 ? ", " + string.Join(", ", actual.Skip(1).Select(t => EditorUtils.GetDataTypeName(t))) : "") + ">();";
            if (expected != legacyCall) { reason = "component/aspect arguments differ: expected " + expected + ", legacy " + legacyCall; return legacyCall; }
            reason = null;
            return "global::" + catalog.FullName + ".InitGeneric_" + key + "<" +
                string.Join(", ", arguments.Select(t => EditorUtils.GetTypeName(t))) + ">();";
        }

        internal static bool TryGetGenericComponents(Type definition, Type constraint, out Type[] components) {
            components = null;
            if (lookup == null || !lookup.genericComponents.TryGetValue((definition, constraint), out var snapshot)) return false;
            // Public EditorUtils API returns arrays: callers must not be able to mutate the snapshot.
            components = (Type[])snapshot.Clone();
            return true;
        }

        internal static void StoreGenericComponents(Type definition, Type constraint, Type[] components) {
            if (lookup != null) lookup.genericComponents[(definition, constraint)] = (Type[])components.Clone();
        }

        private static MethodInfo FindMethod(Type catalog, string name, Type[] parameters = null) {
            var key = (catalog, name);
            if (lookup == null || !lookup.methods.TryGetValue(key, out var method)) {
                method = catalog.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                if (lookup != null) lookup.methods[key] = method;
            }
            if (method == null || parameters == null) return method;
            var actual = method.GetParameters();
            if (actual.Length != parameters.Length) return null;
            for (var i = 0; i < actual.Length; ++i) if (actual[i].ParameterType != parameters[i]) return null;
            return method;
        }

        internal static void AddEditorTypes(HashSet<Type> components, HashSet<Type> aspects) {
            // Run-local snapshots: no stale state across compilation or assembly reload.
            var catalogComponents = new HashSet<Type>();
            var catalogAspects = new HashSet<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.IsDynamic) continue;
                try {
                    var catalog = GetCatalog(assembly);
                    if (catalog == null) continue;
                    ReadEditorTypes(catalog, "GetComponents", typeof(IComponentBase), catalogComponents);
                    ReadEditorTypes(catalog, "GetAspects", typeof(IAspect), catalogAspects);
                } catch (Exception exception) {
                    UnityEngine.Debug.LogWarning("[ME.BECS] Source catalog unavailable for " + assembly.GetName().Name +
                        "; using legacy discovery. " + exception.GetBaseException().Message);
                }
            }

            // Retain the exact TypeCache insertion order, including equal FullName sort keys.
            // During migration TypeCache also guards against unexpected catalog-only entries.
            AddWithFallback(UnityEditor.TypeCache.GetTypesDerivedFrom<IComponentBase>(), catalogComponents, components);
            AddWithFallback(UnityEditor.TypeCache.GetTypesDerivedFrom<IAspect>(), catalogAspects, aspects);
        }

        private static void ReadEditorTypes(Type catalog, string methodName, Type contract, HashSet<Type> target) {
            var method = FindMethod(catalog, methodName, Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(Type[])) return;
            var entries = (Type[])method.Invoke(null, null);
            if (entries == null) return;
            foreach (var type in entries) {
                if (type != null && type.Assembly == catalog.Assembly && contract.IsAssignableFrom(type) &&
                    type.IsValueType && type.IsVisible && !type.ContainsGenericParameters) target.Add(type);
            }
        }

        private static void AddWithFallback(IEnumerable<Type> legacy, HashSet<Type> catalog, HashSet<Type> target) {
            foreach (var type in legacy) {
                if (catalog.Contains(type) || (type.IsValueType && type.IsVisible && !type.ContainsGenericParameters)) {
                    target.Add(type);
                }
            }
        }

        internal static bool TryGetRegistration(Type component, bool isTag, bool isStatic, out string call) {
            call = null;
            if (!component.IsVisible || component.IsGenericType || isStatic ||
                typeof(IComponentShared).IsAssignableFrom(component)) return false;

            // Require agreement with the semantic generator before replacing legacy emission.
            var fields = component.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var semanticTag = fields.Length == 0 && (component.StructLayoutAttribute?.Size ?? 0) <= 1;
            if (semanticTag != isTag) return false;
            if (!isTag) {
                var defaults = component.GetProperty("Default", BindingFlags.Static | BindingFlags.Public);
                if (defaults != null && (defaults.PropertyType != component || defaults.GetGetMethod() == null)) return false;
            }

            var catalog = GetCatalog(component.Assembly);
            if (catalog == null) return false;
            // Keyword-escaped names not matched here simply keep the legacy path.
            var methodName = "Register_" + Encode("global::" + component.FullName.Replace('+', '.'));
            var method = FindMethod(catalog, methodName, Type.EmptyTypes);
            if (method == null || method.ReturnType != typeof(void)) return false;
            call = "global::" + catalog.FullName + "." + methodName + "();";
            return true;
        }

        internal static Type GetCatalog(Assembly assembly) => GetCatalog(assembly, false);

        private static Type GetCatalog(Assembly assembly, bool systems) {
            var key = (assembly, systems);
            if (lookup != null && lookup.catalogs.TryGetValue(key, out var cached)) return cached;
            var catalog = assembly.GetType("ME.BECS.SourceGenerated." + (systems ? "GenericSystems_" : "Catalog_") + Encode(assembly.GetName().Name), false);
            if (lookup != null) lookup.catalogs[key] = catalog;
            return catalog;
        }

        internal static bool TryGetSystemRegistration(Type system, out string call) {
            call = null;
            if (system.IsGenericType) {
                if (!system.IsVisible || system.ContainsGenericParameters || !typeof(ISystem).IsAssignableFrom(system)) return false;
                var definition = system.GetGenericTypeDefinition();
                var catalog = GetCatalog(system.Assembly, true);
                if (catalog == null) return false;
                var name = "Register_" + Encode(definition.FullName);
                var genericMethod = FindMethod(catalog, name);
                var arguments = system.GetGenericArguments();
                if (genericMethod == null || !genericMethod.IsGenericMethodDefinition || genericMethod.ReturnType != typeof(void) ||
                    genericMethod.GetParameters().Length != 0 || genericMethod.GetGenericArguments().Length != arguments.Length) return false;
                // Validate constraints only. Never invoke registration during code generation.
                try { genericMethod.MakeGenericMethod(arguments); }
                catch (ArgumentException) { return false; }
                call = "global::" + catalog.FullName + "." + name + "<" +
                    string.Join(", ", arguments.Select(argument => EditorUtils.GetTypeName(argument))) + ">();";
                return true;
            }
            if (!typeof(ISystem).IsAssignableFrom(system) ||
                !TryGetMethod(system, "RegisterSystem_", Type.EmptyTypes, out var method)) return false;
            call = method + "();";
            return true;
        }

        internal static bool TryGetConfigRegistration(Type component, bool isTag, bool isStatic, out string call) {
            call = null;
            if (!typeof(IConfigInitialize).IsAssignableFrom(component) ||
                !TryGetMethod(component, "RegisterConfig_", new[] { typeof(bool), typeof(bool) }, out var method)) return false;
            call = method + "(" + (isTag ? "true" : "false") + ", " + (isStatic ? "true" : "false") + ");";
            return true;
        }

        internal static bool TryGetSystemLifecycleAot(Type system, string phase, out string call) {
            return TryGetSystemAotMethod(system, phase, true, out call);
        }

        internal static bool TryGetSystemPointerAot(Type system, string phase, string kind, out string call) {
            return TryGetSystemAotMethod(system, kind + phase, false, out call);
        }

        private static bool TryGetSystemAotMethod(Type system, string phase, bool withContext, out string call) {
            call = null;
            if (!system.IsVisible || system.ContainsGenericParameters || !typeof(ISystem).IsAssignableFrom(system)) return false;
            var definition = system.IsGenericType ? system.GetGenericTypeDefinition() : system;
            var catalog = GetCatalog(system.Assembly, true);
            if (catalog == null) return false;
            var name = "Aot" + phase + "_" + Encode(definition.FullName);
            var method = FindMethod(catalog, name);
            if (method == null || method.ReturnType != typeof(void)) return false;
            var arguments = system.IsGenericType ? system.GetGenericArguments() : Type.EmptyTypes;
            if (arguments.Length > 0) {
                if (!method.IsGenericMethodDefinition || method.GetGenericArguments().Length != arguments.Length) return false;
                try { method = method.MakeGenericMethod(arguments); }
                catch (ArgumentException) { return false; }
            } else if (method.IsGenericMethod) return false;
            var parameters = method.GetParameters();
            if (withContext ? parameters.Length != 1 || parameters[0].ParameterType != typeof(SystemContext).MakeByRefType() : parameters.Length != 0) return false;
            var suffix = arguments.Length == 0 ? "" : "<" + string.Join(", ", arguments.Select(a => EditorUtils.GetTypeName(a))) + ">";
            call = "global::" + catalog.FullName + "." + name + suffix + (withContext ? "(ref nullContext);" : "();");
            return true;
        }

        internal static bool TryGetAot(Type component, string phase, out string call) {
            call = null;
            var contract = phase == "Component" ? typeof(IComponentBase) : phase == "Shared" ? typeof(IComponentShared) :
                phase == "Static" ? typeof(IConfigComponentStatic) : phase == "Config" ? typeof(IConfigInitialize) : null;
            if (contract == null || !contract.IsAssignableFrom(component) ||
                !TryGetMethod(component, "Aot" + phase + "_", Type.EmptyTypes, out var method)) return false;
            call = method + "();";
            return true;
        }

        internal static bool TryGetGroupRegistration(Type component, Type group, out string call) {
            call = null;
            if (!typeof(IComponentBase).IsAssignableFrom(component)) return false;
            if (!TryGetMethod(component, "RegisterGroup_", new[] { typeof(Type) }, out var method)) return false;
            call = method + "(typeof(" + EditorUtils.GetTypeName(group) + "));";
            return true;
        }

        internal static bool TryGetEntityTypeRegistration(Type type, uint id, out string call) {
            call = null;
            if (id > ushort.MaxValue || !typeof(IEntityType).IsAssignableFrom(type) ||
                !TryGetMethod(type, "RegisterEntityType_", new[] { typeof(ushort) }, out var method)) return false;
            call = method + "(" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ");";
            return true;
        }

        internal static bool TryGetAspectRegistration(Type aspect, out string call) {
            call = null;
            if (!typeof(IAspect).IsAssignableFrom(aspect)) return false;
            if (!TryGetMethod(aspect, "RegisterAspect_", Type.EmptyTypes, out var method)) return false;
            call = method + "();";
            return true;
        }

        internal static bool TryGetAspectQuery(Type aspect, out string call, out Type[] components) {
            call = null;
            components = null;
            if (!typeof(IAspect).IsAssignableFrom(aspect) ||
                !TryGetMethod(aspect, "InitializeAspectQuery_", Type.EmptyTypes, out var initializer)) return false;
            var catalog = GetCatalog(aspect.Assembly);
            var metadata = FindMethod(catalog, "GetAspectQuery_" + Encode("global::" + aspect.FullName.Replace('+', '.')), Type.EmptyTypes);
            if (metadata == null || metadata.ReturnType != typeof(Type[])) return false;
            var expected = new List<Type>();
            foreach (var field in aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (!typeof(IAspectData).IsAssignableFrom(field.FieldType) || field.GetCustomAttribute<QueryWithAttribute>() == null) continue;
                var arguments = field.FieldType.GenericTypeArguments;
                if (arguments.Length == 0 || !arguments[0].IsVisible) return false;
                expected.Add(arguments[0]);
            }
            Type[] actual;
            try { actual = (Type[])metadata.Invoke(null, null); }
            catch (Exception) { return false; }
            if (actual == null || actual.Length == 0 || actual.Length != expected.Count) return false;
            for (var i = 0; i < actual.Length; ++i) if (actual[i] != expected[i]) return false;
            components = actual;
            call = initializer + "();";
            return true;
        }

        internal static bool TryGetAspectConstruction(Type aspect, out string call, out Type[] components) {
            return TryGetAspectConstruction(aspect, out call, out components, out _);
        }

        internal static bool TryGetAspectConstruction(Type aspect, out string call, out Type[] components, out string reason) {
            call = null;
            components = null;
            reason = "ConstructAspect method unavailable (unsupported declaration or analyzer output not refreshed)";
            if (!typeof(IAspect).IsAssignableFrom(aspect) || !TryGetMethod(aspect, "ConstructAspect_",
                new[] { typeof(World).MakeByRefType() }, out var constructor)) return false;
            var metadata = FindMethod(GetCatalog(aspect.Assembly), "GetAspectConstruction_" + Encode("global::" + aspect.FullName.Replace('+', '.')), Type.EmptyTypes);
            if (metadata == null || metadata.ReturnType != typeof(string[])) {
                reason = "GetAspectConstruction metadata missing or signature differs";
                return false;
            }
            string[] names;
            try { names = (string[])metadata.Invoke(null, null); }
            catch (Exception exception) { reason = "Construction metadata failed: " + exception.GetBaseException().Message; return false; }
            var fields = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .OrderBy(f => f.FieldType.FullName).Where(f => typeof(IAspectData).IsAssignableFrom(f.FieldType)).ToArray();
            if (names == null || names.Length == 0 || names.Length != fields.Length) {
                reason = "Field count mismatch: legacy=" + fields.Length + ", generated=" + (names == null ? "null" : names.Length.ToString());
                return false;
            }
            var types = new Type[fields.Length];
            for (var i = 0; i < fields.Length; ++i) {
                var field = fields[i];
                if (names[i] != field.Name) {
                    reason = "Field order mismatch at " + i + ": legacy=[" + string.Join(", ", fields.Select(f => f.Name)) +
                        "], generated=[" + string.Join(", ", names) + "]";
                    return false;
                }
                if (field.IsInitOnly || !field.FieldType.IsGenericType || field.FieldType.GetGenericTypeDefinition() != typeof(AspectDataPtr<>)) {
                    reason = "Unsupported field: " + field.Name + " (" + field.FieldType.FullName + "), readonly=" + field.IsInitOnly;
                    return false;
                }
                types[i] = field.FieldType.GenericTypeArguments[0];
                if (!types[i].IsVisible) { reason = "Component not public: " + types[i].FullName; return false; }
            }
            components = types;
            call = constructor + "(ref world);";
            reason = null;
            return true;
        }

        private static bool TryGetMethod(Type type, string prefix, Type[] parameters, out string qualifiedName) {
            qualifiedName = null;
            if (!type.IsVisible || type.IsGenericType) return false;
            var catalog = GetCatalog(type.Assembly);
            if (catalog == null) return false;
            var name = prefix + Encode("global::" + type.FullName.Replace('+', '.'));
            var method = FindMethod(catalog, name, parameters);
            if (method == null || method.ReturnType != typeof(void)) return false;
            qualifiedName = "global::" + catalog.FullName + "." + name;
            return true;
        }

        internal static bool TryGetSpecialRegistration(Type component, bool shared, bool isTag, bool hasCustomHash, out string call) {
            call = null;
            if (!component.IsVisible || component.IsGenericType) return false;
            if (!(shared ? typeof(IComponentShared) : typeof(IConfigComponentStatic)).IsAssignableFrom(component)) return false;
            var catalog = GetCatalog(component.Assembly);
            if (catalog == null) return false;
            var name = (shared ? "RegisterShared_" : "RegisterStatic_") + Encode("global::" + component.FullName.Replace('+', '.'));
            var parameters = shared ? new[] { typeof(bool), typeof(bool) } : new[] { typeof(bool) };
            var method = FindMethod(catalog, name, parameters);
            if (method == null || method.ReturnType != typeof(void)) return false;
            call = "global::" + catalog.FullName + "." + name + "(" + (isTag ? "true" : "false") +
                (shared ? ", " + (hasCustomHash ? "true" : "false") : string.Empty) + ");";
            return true;
        }

        private static string HashEarlyInitCall(string value) {
            return ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(value);
        }

        private static string Encode(string value) {
            if (lookup != null && lookup.encoded.TryGetValue(value, out var cached)) return cached;
            var encoded = ME.BECS.CodeGeneration.SourceGeneratorNames.Encode(value);
            if (lookup != null) lookup.encoded[value] = encoded;
            return encoded;
        }

    }

}
