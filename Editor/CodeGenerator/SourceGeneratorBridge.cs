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
            internal readonly Dictionary<Assembly, string[][]> inputRecords = new Dictionary<Assembly, string[][]>();
            internal readonly Dictionary<Type, (bool success, KeyValuePair<int, string>[] calls, string reason)> earlyInitPlans =
                new Dictionary<Type, (bool, KeyValuePair<int, string>[], string)>();
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
                this.inputRecords.Clear();
                this.earlyInitPlans.Clear();
                this.disposed = true;
            }
        }

        internal static LookupScope BeginLookupScope() => new LookupScope();

        internal static bool TryGetConfigCollectionsRegistration(Type component, bool countOnly, bool editor, out string call, out string reason) {
            if (countOnly) return TryGetConfigCollectionCount(component, editor, out call, out reason);
            call = null;
            reason = "manifest collection callback unavailable";
            if (component.ContainsGenericParameters) return false;
            var profile = editor ? "editor" : "runtime";
            var assemblyName = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name == assemblyName).ToArray();
            if (assemblies.Length != 1) return false;
            var catalog = assemblies[0].GetType("ME.BECS.SourceGenerated.ConfigCollectionsInputs", false);
            if (catalog == null) return false;
            var records = GetInputRecords(assemblies[0]);
            if (records.Count(row => row.Length == 4 && row[0] == profile && row[1] == "config-collection-callback-schema" && row[2] == "0" && row[3] == "djE=") != 1) return false;
            var identity = Convert.ToBase64String(Encoding.UTF8.GetBytes(component.AssemblyQualifiedName));
            var selected = records.Where(row => row.Length == 5 && row[0] == profile && row[1] == "config-collection-callback" && row[3] == identity).ToArray();
            if (selected.Length != 1 || !int.TryParse(selected[0][2], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ||
                selected[0][2] != ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            var initialize = FindMethod(catalog, "Initialize", Type.EmptyTypes);
            var callback = FindMethod(catalog, "Apply_" + selected[0][2],
                new[] { typeof(UnsafeEntityConfig).MakeByRefType(), typeof(void).MakePointerType(), typeof(Ent).MakeByRefType() });
            if (initialize == null || initialize.ReturnType != typeof(void) || callback == null || callback.IsGenericMethod || callback.ReturnType != typeof(void)) return false;
            string[] actual;
            try { actual = Encoding.UTF8.GetString(Convert.FromBase64String(selected[0][4])).Split(','); }
            catch (FormatException) { reason = "invalid collection field encoding"; return false; }
            var expected = Aspects.EntityConfigCodeGenerator.GetCollectionFields(component).Select(field => field.Name);
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal)) { reason = "collection field order mismatch"; return false; }
            call = "global::ME.BECS.SourceGenerated.ConfigCollectionsInputs.Initialize();";
            reason = null;
            return true;
        }

        private static bool TryGetConfigCollectionCount(Type component, bool editor, out string call, out string reason) {
            call = null;
            reason = "manifest collection count unavailable";
            if (component.ContainsGenericParameters) return false;
            var profile = editor ? "editor" : "runtime";
            var assemblyName = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name == assemblyName).ToArray();
            if (assemblies.Length != 1) return false;
            var catalog = assemblies[0].GetType("ME.BECS.SourceGenerated.ConfigCollectionCounts", false);
            if (catalog == null) return false;
            var records = GetInputRecords(assemblies[0]);
            if (records.Count(r => r.Length == 4 && r[0] == profile && r[1] == "config-collection-count-schema" && r[2] == "0" && r[3] == "djE=") != 1) return false;
            var identity = Convert.ToBase64String(Encoding.UTF8.GetBytes(component.AssemblyQualifiedName));
            var selected = records.Where(r => r.Length == 5 && r[0] == profile && r[1] == "config-collection-count" && r[3] == identity).ToArray();
            if (selected.Length != 1 || !uint.TryParse(selected[0][4], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var count) || count == 0 ||
                selected[0][4] != count.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            if (count != Aspects.EntityConfigCodeGenerator.GetCollectionsCount(component)) { reason = "collection count mismatch"; return false; }
            var initialize = FindMethod(catalog, "Initialize", Type.EmptyTypes);
            if (initialize == null || initialize.ReturnType != typeof(void)) return false;
            call = "global::ME.BECS.SourceGenerated.ConfigCollectionCounts.Initialize();";
            reason = null;
            return true;
        }

        private static string[][] GetInputRecords(Assembly assembly) {
            if (lookup != null && lookup.inputRecords.TryGetValue(assembly, out var cached)) return cached;
            var records = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(a => a.Key == "ME.BECS.TypeInput.v1" && a.Value != null).Select(a => a.Value.Split('\t')).ToArray();
            if (lookup != null) lookup.inputRecords.Add(assembly, records);
            return records;
        }

        internal static bool TryGetConfigMaskRegistration(Type component, bool editor, out string call, out string reason) {
            call = null;
            reason = "generated callback unavailable";
            if (component.ContainsGenericParameters || !typeof(IConfigComponent).IsAssignableFrom(component)) return false;
            var profile = editor ? "editor" : "runtime";
            var assemblyName = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name == assemblyName).ToArray();
            if (assemblies.Length != 1) return false;
            var catalog = assemblies[0].GetType("ME.BECS.SourceGenerated.ConfigMaskInputs", false);
            if (catalog == null) return false;
            var records = GetInputRecords(assemblies[0]);
            if (records.Count(r => r.Length == 4 && r[0] == profile && r[1] == "config-mask-schema" && r[2] == "0" && r[3] == "djE=") != 1) return false;
            var identity = Convert.ToBase64String(Encoding.UTF8.GetBytes(component.AssemblyQualifiedName));
            var selected = records.Where(r => r.Length == 5 && r[0] == profile && r[1] == "config-mask-registration" && r[3] == identity).ToArray();
            if (selected.Length != 1 || !int.TryParse(selected[0][2], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ||
                selected[0][2] != ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            var register = FindMethod(catalog, "Initialize", Type.EmptyTypes);
            var pointer = typeof(void).MakePointerType();
            var callback = FindMethod(catalog, "Apply_" + selected[0][2],
                new[] { typeof(UnsafeEntityConfig).MakeByRefType(), pointer, pointer, pointer, typeof(Ent).MakeByRefType() });
            if (register == null || register.ReturnType != typeof(void) || callback == null || callback.IsGenericMethod || callback.ReturnType != typeof(void)) return false;
            var expected = component.GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).ToArray();
            string[] actual;
            try { actual = Encoding.UTF8.GetString(Convert.FromBase64String(selected[0][4])).Split(','); }
            catch (FormatException) { reason = "invalid field encoding"; return false; }
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal)) {
                reason = "field order mismatch";
                return false;
            }
            call = "global::ME.BECS.SourceGenerated.ConfigMaskInputs.Initialize();";
            reason = null;
            return true;
        }

        internal static bool TryGetDestroyRegistration(Type component, bool editor, out string call) {
            call = null;
            if (component.ContainsGenericParameters || !typeof(IComponentDestroy).IsAssignableFrom(component)) return false;
            var profile = editor ? "editor" : "runtime";
            var assemblyName = "ME.BECS.Gen." + (editor ? "Editor" : "Runtime");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.GetName().Name == assemblyName).ToArray();
            if (assemblies.Length != 1) return false;
            var catalog = assemblies[0].GetType("ME.BECS.SourceGenerated.DestroyInputs", false);
            if (catalog == null) return false;
            var identity = Convert.ToBase64String(Encoding.UTF8.GetBytes(component.AssemblyQualifiedName));
            var records = GetInputRecords(assemblies[0]);
            if (records.Count(r => r.Length == 4 && r[0] == profile && r[1] == "destroy-schema" && r[2] == "0" && r[3] == "djE=") != 1) return false;
            var selected = records.Where(r => r.Length == 4 && r[0] == profile && r[1] == "destroy-registration" && r[3] == identity).ToArray();
            if (selected.Length != 1 || !int.TryParse(selected[0][2], System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ||
                selected[0][2] != ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            var initialize = FindMethod(catalog, "Initialize", Type.EmptyTypes);
            var callback = catalog.GetMethod("Destroy_" + selected[0][2], BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (initialize == null || initialize.ReturnType != typeof(void) || callback == null || callback.IsGenericMethod || callback.ReturnType != typeof(void)) return false;
            var parameters = callback.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(Ent).MakeByRefType() ||
                !parameters[1].ParameterType.IsPointer || parameters[1].ParameterType.GetElementType() != typeof(byte)) return false;
            call = "global::ME.BECS.SourceGenerated.DestroyInputs.Initialize();";
            return true;
        }

        internal static Type[] GetDerivedTypesSnapshot(Type contract) {
            if (lookup != null && lookup.derivedTypes.TryGetValue(contract, out var snapshot)) return (Type[])snapshot.Clone();
            var types = UnityEditor.TypeCache.GetTypesDerivedFrom(contract).ToArray();
            if (lookup != null) lookup.derivedTypes[contract] = (Type[])types.Clone();
            return types;
        }

        // Independent source selection for migration diagnostics. Only Type[] metadata getters
        // are invoked; job initialization and its wrapper are never executed here.
        internal static bool TryGetJobEarlyInitSelection(Type job, out string[] calls, out string reason) {
            calls = null;
            if (!TryGetJobEarlyInitPlan(job, out var plan, out reason)) return false;
            calls = plan.Select(entry => entry.Value).OrderBy(call => call, StringComparer.Ordinal).ToArray();
            return true;
        }

        internal static bool TryGetJobEarlyInitPlan(Type job, out KeyValuePair<int, string>[] calls, out string reason) {
            if (lookup != null && lookup.earlyInitPlans.TryGetValue(job, out var cached)) {
                calls = cached.calls == null ? null : (KeyValuePair<int, string>[])cached.calls.Clone();
                reason = cached.reason;
                return cached.success;
            }
            var success = ReadJobEarlyInitPlan(job, out calls, out reason);
            if (lookup != null) lookup.earlyInitPlans.Add(job,
                (success, calls == null ? null : (KeyValuePair<int, string>[])calls.Clone(), reason));
            return success;
        }

        private static bool ReadJobEarlyInitPlan(Type job, out KeyValuePair<int, string>[] calls, out string reason) {
            calls = null;
            reason = "source selection catalog unavailable";
            if (!job.IsVisible || job.ContainsGenericParameters) return false;
            var catalog = job.Assembly.GetType("ME.BECS.SourceGenerated.JobEarlyInit_" + Encode(job.Assembly.GetName().Name), false);
            if (catalog == null || !catalog.IsVisible) return false;
            var definition = job.IsGenericType ? job.GetGenericTypeDefinition() : job;
            var arguments = job.IsGenericType ? job.GetGenericArguments() : Type.EmptyTypes;
            var selected = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var field in catalog.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                if (!field.IsLiteral || field.FieldType != typeof(string) || !field.Name.StartsWith("Selection_", StringComparison.Ordinal)) continue;
                var row = (field.GetRawConstantValue() as string)?.Split('\t');
                if (row == null || row.Length != 7 || row[0] != "v2") { reason = "invalid source selection schema (requires v2)"; return false; }
                if (row[1] != definition.FullName) continue;
                if (!int.TryParse(row[6], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var phase) ||
                    phase < 0 || phase > 6) { reason = "unsupported source selection phase"; return false; }
                if (row[5] != arguments.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    !row[2].StartsWith("Do", StringComparison.Ordinal)) { reason = "invalid source selection arity/method"; return false; }
                var wrapper = FindMethod(catalog, row[3], Type.EmptyTypes);
                var metadata = FindMethod(catalog, row[4], Type.EmptyTypes);
                if (wrapper == null || metadata == null || wrapper.ReturnType != typeof(void) || metadata.ReturnType != typeof(Type[]) ||
                    wrapper.GetGenericArguments().Length != arguments.Length || metadata.GetGenericArguments().Length != arguments.Length) {
                    reason = "source selection wrapper/arguments signature mismatch"; return false;
                }
                Type[] actual;
                try {
                    if (arguments.Length != 0) {
                        wrapper.MakeGenericMethod(arguments);
                        metadata = metadata.MakeGenericMethod(arguments);
                    }
                    actual = (Type[])metadata.Invoke(null, null);
                } catch (ArgumentException exception) { reason = "source selection constraint mismatch: " + exception.Message; return false; }
                  catch (TargetInvocationException exception) { reason = "source selection metadata failed: " + exception.GetBaseException().Message; return false; }
                if (actual == null || actual.Length == 0 || actual[0] != job || actual.Any(type => type == null || type.ContainsGenericParameters)) {
                    reason = "source selection contains invalid/unbound argument types"; return false;
                }
                var call = "EarlyInit." + row[2] + "<" + EditorUtils.GetTypeName(job) +
                    (actual.Length > 1 ? ", " + string.Join(", ", actual.Skip(1).Select(type => EditorUtils.GetDataTypeName(type))) : "") + ">();";
                if (selected.ContainsKey(call)) { reason = "duplicate source selection: " + call; return false; }
                selected.Add(call, phase);
            }
            if (selected.Count == 0) { reason = "source selection absent for job (recompile its catalog)"; return false; }
            calls = selected.OrderBy(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new KeyValuePair<int, string>(entry.Value, entry.Key)).ToArray();
            reason = null;
            return true;
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
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters || !catalog.IsVisible) return legacyCall;
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
