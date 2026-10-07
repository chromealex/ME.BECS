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
            internal readonly Dictionary<(Assembly, bool), Dictionary<string, (Type Catalog, MethodInfo Register, string Ordinal)>> typePublications =
                new Dictionary<(Assembly, bool), Dictionary<string, (Type, MethodInfo, string)>>();
            internal readonly Dictionary<(Assembly, bool), Dictionary<string, (Type Catalog, Type Bodies, string Ordinal)>> configPublications =
                new Dictionary<(Assembly, bool), Dictionary<string, (Type, Type, string)>>();
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
                this.typePublications.Clear();
                this.configPublications.Clear();
                this.earlyInitPlans.Clear();
                this.disposed = true;
            }
        }

        internal static LookupScope BeginLookupScope() => new LookupScope();

        internal static bool HasConfigCollectionsRegistration(Type component, bool countOnly, bool editor, out string reason) =>
            HasConfigRegistration(component, countOnly ? "Counts" : "Collections", editor, out reason);

        internal static bool HasConfigMaskRegistration(Type component, bool editor, out string reason) =>
            HasConfigRegistration(component, "Masks", editor, out reason);

        private static string[][] GetInputRecords(Assembly assembly) {
            if (lookup != null && lookup.inputRecords.TryGetValue(assembly, out var cached)) return cached;
            // Catalog assemblies no longer embed their rows; read the verified snapshot rows.
            var records = SourceGeneratorInputCatalog.Records(assembly);
            if (lookup != null) lookup.inputRecords.Add(assembly, records);
            return records;
        }

        // Availability diagnostics inspect the exact owner publication. They do
        // not emit replacement calls or require an aggregate Initialize facade.
        private static bool HasConfigRegistration(Type component, string phase, bool editor, out string reason) {
            reason = "Owner-local config publication unavailable";
            if (component == null || component.ContainsGenericParameters) return false;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).ToArray();
            if (!SourceGeneratorInputCatalog.TryGet(editor, assemblies, out var selection, out reason)) return false;
            var rows = selection.Rows;
            if (!rows.Contains("config-publication-schema\t0\tdjE=")) { reason = "Config publication selection unavailable"; return false; }
            reason = "Owner-local config publication unavailable";
            try {
                var entryValue = CodeGeneration.SourceGeneratorConfigFragmentFormat.EntryValue(phase, component.AssemblyQualifiedName);
                var publications = GetConfigPublications(selection.Assembly, editor, rows, assemblies);
                if (publications == null || !publications.TryGetValue(entryValue, out var publication)) return false;
                var ordinal = publication.Ordinal;
                var catalog = publication.Catalog;
                var register = FindMethod(catalog, "Register_" + ordinal, Type.EmptyTypes);
                if (register == null || register.ContainsGenericParameters || register.ReturnType != typeof(void)) return false;
                if (phase == "Counts" || phase == "Collections") {
                    var count = FindMethod(catalog, "GetCount_" + ordinal, Type.EmptyTypes);
                    if (count == null || count.ReturnType != typeof(uint) || (uint)count.Invoke(null, null) != Aspects.EntityConfigCodeGenerator.GetCollectionsCount(component)) return false;
                }
                if (phase != "Counts") {
                    var fields = FindMethod(catalog, "GetFields_" + ordinal, Type.EmptyTypes);
                    var expectedFields = phase == "Masks" ? component.GetFields(BindingFlags.Public | BindingFlags.Instance) : Aspects.EntityConfigCodeGenerator.GetCollectionFields(component);
                    if (fields == null || fields.ReturnType != typeof(string[]) ||
                        !expectedFields.Select(field => field.Name).SequenceEqual((string[])fields.Invoke(null, null), StringComparer.Ordinal)) return false;
                    var pointer = typeof(void).MakePointerType();
                    var parameters = phase == "Masks" ? new[] { typeof(UnsafeEntityConfig).MakeByRefType(), pointer, pointer, pointer, typeof(Ent).MakeByRefType() } :
                        new[] { typeof(UnsafeEntityConfig).MakeByRefType(), pointer, typeof(Ent).MakeByRefType() };
                    var callback = FindMethod(publication.Bodies, "Apply_" + ordinal, parameters);
                    if (callback == null || callback.IsGenericMethod || callback.ReturnType != typeof(void)) return false;
                }
                reason = null;
                return true;
            } catch (FormatException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        private static Dictionary<string, (Type Catalog, Type Bodies, string Ordinal)> GetConfigPublications(
            Assembly selection, bool editor, string[] rows, Assembly[] assemblies) {
            if (lookup != null && lookup.configPublications.TryGetValue((selection, editor), out var cached)) return cached;
            var profile = editor ? "Editor" : "Runtime";
            Dictionary<string, (Type Catalog, Type Bodies, string Ordinal)> Build() {
                var result = new Dictionary<string, (Type, Type, string)>(StringComparer.Ordinal);
                foreach (var document in CodeGeneration.SourceGeneratorConfigFragmentFormat.Documents(rows, editor)) {
                    var owners = assemblies.Where(assembly => assembly.GetName().Name == document.Owner).ToArray();
                    if (owners.Length != 1) return null;
                    var expected = CodeGeneration.SourceGeneratorSystemFragmentFormat.Metadata(document, CodeGeneration.SourceGeneratorConfigFragmentFormat.Serialize(document));
                    if (owners[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Count(attribute => attribute.Key == CodeGeneration.SourceGeneratorConfigFragmentFormat.MetadataKey && attribute.Value == expected) != 1) return null;
                    var catalog = owners[0].GetType("ME.BECS.SourceGenerated.ConfigFragment_" + profile, false);
                    var bodies = owners[0].GetType("ME.BECS.SourceGenerated.ConfigCallbacks_" + profile, false);
                    if (catalog == null || bodies == null) return null;
                    foreach (var entry in document.Entries)
                        result.Add(entry.Value, (catalog, bodies, entry.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }
                return result;
            }
            var publications = Build();
            // A lookup scope is confined to one synchronous comparison/export.
            // Nothing survives reloads or a later compilation/input selection.
            if (lookup != null) lookup.configPublications.Add((selection, editor), publications);
            return publications;
        }

        internal static bool HasDestroyRegistration(Type component, bool editor) {
            if (component == null || component.ContainsGenericParameters || !typeof(IComponentDestroy).IsAssignableFrom(component)) return false;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).ToArray();
            if (!SourceGeneratorInputCatalog.TryGet(editor, assemblies, out var selection, out _)) return false;
            if (!selection.Rows.Contains("destroy-publication-schema\t0\tdjE=")) return false;
            try {
                var matches = CodeGeneration.SourceGeneratorDestroyFragmentFormat.Documents(selection.Rows, editor)
                    .Where(document => document.Entries.Any(entry => entry.Value == component.AssemblyQualifiedName)).ToArray();
                if (matches.Length != 1) return false;
                var document = matches[0];
                var owners = assemblies.Where(assembly => assembly.GetName().Name == document.Owner).ToArray();
                if (owners.Length != 1) return false;
                var expected = CodeGeneration.SourceGeneratorSystemFragmentFormat.Metadata(document,
                    CodeGeneration.SourceGeneratorDestroyFragmentFormat.Serialize(document));
                if (owners[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                    .Count(attribute => attribute.Key == CodeGeneration.SourceGeneratorDestroyFragmentFormat.MetadataKey && attribute.Value == expected) != 1) return false;
                var profile = editor ? "Editor" : "Runtime";
                var publisher = owners[0].GetType("ME.BECS.SourceGenerated.DestroyFragment_" + profile, false);
                var catalog = owners[0].GetType("ME.BECS.SourceGenerated.DestroyCallbacks_" + profile, false);
                if (publisher == null || catalog == null) return false;
                var ordinal = document.Entries.Single(entry => entry.Value == component.AssemblyQualifiedName).Key.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var register = FindMethod(publisher, "Register_" + ordinal, Type.EmptyTypes);
                var callback = FindMethod(catalog, "Destroy_" + ordinal, new[] { typeof(Ent).MakeByRefType(), typeof(byte).MakePointerType() });
                return register != null && !register.ContainsGenericParameters && register.ReturnType == typeof(void) &&
                    callback != null && !callback.ContainsGenericParameters && callback.ReturnType == typeof(void);
            } catch (FormatException) { return false; }
            catch (InvalidOperationException) { return false; }
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
                    (actual.Length > 1 ? ", " + string.Join(", ", actual.Skip(1).Select(type => EditorUtils.GetTypeName(type))) : "") + ">();";
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
            var method = ResolveJobEarlyInitMethod(job, legacyCall, out reason);
            if (method == null) return legacyCall;
            return "global::" + method.DeclaringType.FullName + "." + method.Name +
                (method.IsGenericMethod ? "<" + string.Join(", ", method.GetGenericArguments().Select(type => EditorUtils.GetTypeName(type))) + ">" : "") + "();";
        }

        internal static MethodInfo ResolveJobEarlyInitMethod(Type job, string legacyCall, out string reason) {
            reason = "not an EarlyInit call, non-public job, or open generic job";
            if (!legacyCall.StartsWith("EarlyInit.", StringComparison.Ordinal) || !job.IsVisible || job.ContainsGenericParameters) return null;
            var catalog = job.Assembly.GetType("ME.BECS.SourceGenerated.JobEarlyInit_" + Encode(job.Assembly.GetName().Name), false);
            if (catalog == null) { reason = "job EarlyInit catalog unavailable"; return null; }
            if (job.IsGenericType) return ResolveGenericJobEarlyInit(job, catalog, legacyCall, out reason);
            var name = "Init_" + HashEarlyInitCall(legacyCall);
            var method = FindMethod(catalog, name, Type.EmptyTypes);
            reason = method == null ? "wrapper for exact legacy call missing (unsupported signature or spelling mismatch)" : "wrapper return type differs";
            if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters || !catalog.IsVisible) return null;
            reason = null;
            return method;
        }

        private static MethodInfo ResolveGenericJobEarlyInit(Type job, Type catalog, string legacyCall, out string reason) {
            reason = "invalid legacy EarlyInit call";
            var end = legacyCall.IndexOf('<');
            if (end < 0) return null;
            var earlyMethod = legacyCall.Substring("EarlyInit.".Length, end - "EarlyInit.".Length);
            var key = HashEarlyInitCall(job.GetGenericTypeDefinition().FullName + "|" + earlyMethod);
            var init = FindMethod(catalog, "InitGeneric_" + key, Type.EmptyTypes);
            var metadata = FindMethod(catalog, "ArgsGeneric_" + key, Type.EmptyTypes);
            var arguments = job.GetGenericArguments();
            reason = "generic wrapper/metadata absent or signature incompatible (unsupported constraints, parameter count, or ambiguous overload)";
            if (init == null || metadata == null || !init.IsGenericMethodDefinition || !metadata.IsGenericMethodDefinition ||
                init.ReturnType != typeof(void) || metadata.ReturnType != typeof(Type[]) ||
                init.GetGenericArguments().Length != arguments.Length || metadata.GetGenericArguments().Length != arguments.Length) return null;
            Type[] actual;
            try {
                init.MakeGenericMethod(arguments);
                actual = (Type[])metadata.MakeGenericMethod(arguments).Invoke(null, null);
            } catch (ArgumentException exception) { reason = "generic constraint mismatch: " + exception.Message; return null; }
              catch (TargetInvocationException exception) { reason = "metadata failed: " + exception.GetBaseException().Message; return null; }
            if (actual == null || actual.Length == 0 || actual[0] != job) { reason = "metadata job type mismatch"; return null; }
            var expected = "EarlyInit." + earlyMethod + "<" + EditorUtils.GetTypeName(job) +
                (actual.Length > 1 ? ", " + string.Join(", ", actual.Skip(1).Select(t => EditorUtils.GetTypeName(t))) : "") + ">();";
            if (expected != legacyCall) { reason = "component/aspect arguments differ: expected " + expected + ", legacy " + legacyCall; return null; }
            reason = null;
            return init.MakeGenericMethod(arguments);
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

        // Diagnostic availability only. Component registration is compiler-owned;
        // never generate a replacement call or invoke registration/Default here.
        internal static bool TryGetComponentRegistration(Type component, bool editor, out int flags, out string reason) {
            flags = 0;
            reason = "compiled component bootstrap unavailable";
            if (component == null || !component.IsValueType || component.ContainsGenericParameters || !typeof(IComponentBase).IsAssignableFrom(component)) return false;
            if (!SourceGeneratorInputCatalog.TryGet(editor, out var selection, out reason)) return false;
            return TryReadComponentRegistration(component, selection.Assembly, editor, out flags, out reason);
        }

        internal static bool TryReadComponentRegistration(Type component, Assembly assembly, bool editor, out int flags, out string reason) {
            flags = 0;
            reason = "component selection/flags unavailable or invalid";
            var profile = editor ? "editor" : "runtime";
            var identity = component.AssemblyQualifiedName;
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(identity));
            var selected = GetInputRecords(assembly).Where(row => (row.Length == 4 || row.Length == 5) &&
                row[0] == profile && row[1] == "component-registration" && row[3] == encoded).ToArray();
            if (selected.Length != 1 || !uint.TryParse(selected[0][2], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var ordinal) ||
                selected[0][2] != ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
            if (!GetInputRecords(assembly).Any(row => row.Length > 1 && row[0] == profile && row[1] == "type-publication-schema")) return false;
            var publications = GetTypePublications(assembly, editor);
            if (publications == null) { reason = "component publication is missing or stale"; return false; }
            var registerValue = CodeGeneration.SourceGeneratorTypeFragmentFormat.EntryValue("Register", identity, null);
            if (!publications.TryGetValue(registerValue, out var registration)) return false;
            var field = registration.Catalog.GetField("Flags_" + registration.Ordinal, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(int)) return false;
            var parsed = (int)field.GetRawConstantValue();
            if (parsed < 0 || parsed > 63 || (parsed & 5) == 5 || ((parsed & 16) != 0 && (parsed & 8) == 0)) return false;
            foreach (var phase in new[] { (bit: 0, suffix: ""), (bit: 8, suffix: "Shared"), (bit: 2, suffix: "Static"), (bit: 32, suffix: "Config") }) {
                var expected = phase.bit == 0 || (parsed & phase.bit) != 0;
                var value = CodeGeneration.SourceGeneratorTypeFragmentFormat.EntryValue("Register" + phase.suffix, identity, null);
                if (publications.TryGetValue(value, out var publication) != expected) {
                    reason = "component publication phase differs: " + phase.suffix; return false;
                }
                if (!expected) continue;
                var aot = FindMethod(publication.Catalog, "Aot_" + publication.Ordinal, Type.EmptyTypes);
                if (!IsPreservationRoot(publication.Catalog) || aot == null || aot.ContainsGenericParameters || aot.ReturnType != typeof(void) ||
                    phase.bit == 0 && FindMethod(publication.Catalog, "Size_" + publication.Ordinal, Type.EmptyTypes)?.ReturnType != typeof(uint)) {
                    reason = "component publication AOT/size method unavailable: " + value; return false;
                }
            }
            flags = parsed;
            reason = null;
            return true;
        }

        internal static bool IsPreservationRoot(Type catalog) {
            if (catalog == null || !Attribute.IsDefined(catalog, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))) return false;
            var root = FindMethod(catalog, "PreserveReferences", Type.EmptyTypes);
            return root != null && root.ReturnType == typeof(void) && !root.ContainsGenericParameters &&
                Attribute.IsDefined(root, typeof(UnityEngine.Scripting.PreserveAttribute)) &&
                !Attribute.IsDefined(root, typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute)) &&
                !Attribute.IsDefined(root, typeof(UnityEditor.InitializeOnLoadMethodAttribute));
        }

        // Only inspect compiler-owned delegate arrays; never execute their
        // registration, Default or AOT targets. Cache per comparison/export,
        // not across compilation or reload boundaries.
        private static Dictionary<string, (Type Catalog, MethodInfo Register, string Ordinal)> GetTypePublications(Assembly selection, bool editor) {
            if (lookup != null && lookup.typePublications.TryGetValue((selection, editor), out var cached)) return cached;
            Dictionary<string, (Type, MethodInfo, string)> Build() {
                var profile = editor ? "Editor" : "Runtime";
                var rows = GetInputRecords(selection).Where(row => row[0] == profile.ToLowerInvariant()).Select(row => string.Join("\t", row.Skip(1)));
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).ToArray();
                var result = new Dictionary<string, (Type, MethodInfo, string)>(StringComparer.Ordinal);
                foreach (var document in CodeGeneration.SourceGeneratorTypeFragmentFormat.Documents(rows, editor)) {
                    var owners = assemblies.Where(assembly => assembly.GetName().Name == document.Owner).ToArray();
                    if (owners.Length != 1) return null;
                    var expected = CodeGeneration.SourceGeneratorSystemFragmentFormat.Metadata(document, CodeGeneration.SourceGeneratorTypeFragmentFormat.Serialize(document));
                    if (owners[0].GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Count(attribute => attribute.Key == CodeGeneration.SourceGeneratorTypeFragmentFormat.MetadataKey && attribute.Value == expected) != 1) return null;
                    var catalog = owners[0].GetType("ME.BECS.SourceGenerated.TypeFragment_" + profile, false);
                    if (catalog == null) return null;
                    var ordinals = catalog.GetField("Ordinals", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as int[];
                    var callbacks = catalog.GetField("Callbacks", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Action[];
                    if (ordinals == null || callbacks == null || callbacks.Length != document.Entries.Length ||
                        !ordinals.SequenceEqual(document.Entries.Select(entry => entry.Key))) return null;
                    for (var i = 0; i < document.Entries.Length; ++i) {
                        var callback = callbacks[i]?.Method;
                        if (callback == null || !callback.IsStatic || callback.ContainsGenericParameters || callback.ReturnType != typeof(void) || callback.GetParameters().Length != 0) return null;
                        result.Add(document.Entries[i].Value, (catalog, callback, ordinals[i].ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
                return result;
            }
            Dictionary<string, (Type, MethodInfo, string)> publications;
            try { publications = Build(); }
            catch (FormatException) { publications = null; }
            catch (InvalidOperationException) { publications = null; }
            if (lookup != null) lookup.typePublications.Add((selection, editor), publications);
            return publications;
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
            if (!system.IsVisible || system.ContainsGenericParameters || !typeof(ISystem).IsAssignableFrom(system)) return false;
            var definition = system.IsGenericType ? system.GetGenericTypeDefinition() : system;
            var catalog = GetCatalog(system.Assembly, true);
            if (catalog == null) return false;
            var name = "Register_" + Encode(definition.FullName);
            var method = FindMethod(catalog, name);
            var arguments = system.IsGenericType ? system.GetGenericArguments() : Type.EmptyTypes;
            if (method == null || method.ReturnType != typeof(void) || method.GetParameters().Length != 0 ||
                method.GetGenericArguments().Length != arguments.Length || method.IsGenericMethodDefinition != system.IsGenericType) return false;
            if (arguments.Length != 0) {
                // Validate constraints only. Never invoke registration during code generation.
                try { method.MakeGenericMethod(arguments); }
                catch (ArgumentException) { return false; }
            }
            call = "global::" + catalog.FullName + "." + name + (arguments.Length == 0 ? "" : "<" +
                string.Join(", ", arguments.Select(argument => EditorUtils.GetTypeName(argument))) + ">") + "();";
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
