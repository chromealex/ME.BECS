namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    // Explicit comparison oracle, not production runtime discovery. Only generated
    // typeof/MethodInfo getters may run here; never lifecycle, Execute, constructor
    // or registration bodies. Production registration reachability is compiled IL.
    internal sealed class SourceGeneratorRuntimeUsage {
        internal sealed class Types {
            internal Type[] components, aspects, entityTypes, jobs;
            internal Types Copy() => new Types {
                components = (Type[])this.components.Clone(), aspects = (Type[])this.aspects.Clone(),
                entityTypes = (Type[])this.entityTypes.Clone(), jobs = (Type[])this.jobs.Clone(),
            };
        }
        private readonly Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray();
        private readonly Dictionary<Assembly, string[][]> metadata = new Dictionary<Assembly, string[][]>();
        private readonly Dictionary<(Type Owner, MethodInfo Root), Types> cache = new Dictionary<(Type, MethodInfo), Types>();

        internal bool TryRead(MethodInfo root, out Types types) => this.TryReadForOwner(root?.DeclaringType, root, out types);

        internal bool TryReadForOwner(Type owner, MethodInfo root, out Types types) {
            types = null;
            if (owner == null || owner.ContainsGenericParameters || root == null || root.ContainsGenericParameters || !IsRoot(owner, root)) return false;
            var key = (owner, root);
            if (!this.cache.TryGetValue(key, out var selected)) {
                selected = this.Read(owner, root);
                this.cache.Add(key, selected);
            }
            if (selected == null) return false;
            types = selected.Copy();
            return true;
        }

        private Types Read(Type owner, MethodInfo root) {
            var identity = owner.IsGenericType ? owner.AssemblyQualifiedName : owner.FullName;
            Types selected = null;
            var found = false;
            foreach (var assembly in owner.IsGenericType ? this.assemblies : new[] { owner.Assembly }) {
                if (!this.metadata.TryGetValue(assembly, out var rows)) {
                    rows = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == "ME.BECS.RuntimeTypeUsage.v1" && attribute.Value != null)
                        .Select(attribute => attribute.Value.Split('\n')).ToArray();
                    this.metadata.Add(assembly, rows);
                }
                foreach (var entry in rows.Where(row => row.Length > 0 && row[0] == identity)) {
                    if (entry.Length < 3 || !uint.TryParse(entry[2], NumberStyles.None, CultureInfo.InvariantCulture, out var gaps) ||
                        entry[2] != gaps.ToString(CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Invalid source runtime type usage header: " + root);
                    if (!TryRoot(assembly, entry, out var declaredOwner, out var bound) || declaredOwner != owner) {
                        if (gaps == 0) throw new InvalidOperationException("Invalid complete source runtime type usage binding: " + root);
                        return null;
                    }
                    if (bound != root) continue;
                    if (found) throw new InvalidOperationException("Duplicate source runtime type usage: " + root);
                    found = true;
                    if (gaps != 0) continue; // Never merge a partial source set into discovery.
                    if (!TryParse(assembly, entry, out var parsed, out var reason))
                        throw new InvalidOperationException("Invalid complete source runtime type usage for " + root + ": " + reason);
                    selected = parsed;
                }
            }
            return selected;
        }

        internal static bool TryParse(Assembly publisher, string[] rows, out Types types, out string reason) {
            types = null;
            reason = "Malformed or incomplete runtime usage catalog";
            if (publisher == null || rows == null || rows.Length < 3 || rows[2] != "0") return false;
            try {
                if (!TryRoot(publisher, rows, out _, out _)) return false;
                var lists = new Dictionary<string, List<string>>(StringComparer.Ordinal) {
                    ["C"] = new List<string>(), ["A"] = new List<string>(), ["E"] = new List<string>(), ["J"] = new List<string>(),
                };
                string[] plan = null;
                foreach (var row in rows.Skip(3)) {
                    var parts = row.Split('\t');
                    if (parts[0] == "R" || parts[0] == "U") continue; // TryRoot validates the unique binding.
                    if (parts[0] == "P") { if (plan != null || parts.Length != 4) return false; plan = parts; continue; }
                    if (parts.Length != 2 || !lists.TryGetValue(parts[0], out var list) || parts[1].Any(char.IsControl)) return false;
                    list.Add(parts[1]);
                }
                var name = "ME.BECS.SourceGenerated.RuntimeTypeUsage_" + CodeGeneration.SourceGeneratorNames.Hash(publisher.FullName + "\n" + rows[0] + "\n" + rows[1]);
                if (plan == null || plan[1] != publisher.FullName || plan[2] != name || plan[3] != "v1") return false;
                var holder = publisher.GetType(name, false);
                if (holder == null || !holder.IsVisible) return false;
                var result = new Dictionary<string, Type[]>(StringComparer.Ordinal);
                foreach (var entry in new[] { (kind: "C", getter: "GetComponents", contract: typeof(IComponentBase)),
                             (kind: "A", getter: "GetAspects", contract: typeof(IAspect)),
                             (kind: "E", getter: "GetEntityTypes", contract: typeof(IEntityType)),
                             (kind: "J", getter: "GetJobs", contract: (Type)null) }) {
                    var list = lists[entry.kind];
                    if (list.Distinct(StringComparer.Ordinal).Count() != list.Count || !list.SequenceEqual(list.OrderBy(value => value, StringComparer.Ordinal))) return false;
                    var getters = holder.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.Name == entry.getter).ToArray();
                    if (getters.Length != 1 || getters[0].ContainsGenericParameters || getters[0].ReturnType != typeof(Type[]) || getters[0].GetParameters().Length != 0) return false;
                    var actual = getters[0].Invoke(null, null) as Type[];
                    if (actual == null || actual.Length != list.Count || actual.Any(type => type == null || !type.IsValueType || !type.IsVisible || type.ContainsGenericParameters || type.IsByRefLike ||
                            entry.contract != null && !entry.contract.IsAssignableFrom(type)) || !actual.Select(type => type.AssemblyQualifiedName).SequenceEqual(list)) return false;
                    if (entry.kind == "J" && actual.Any(type => !type.GetInterfaces().Any(contract => contract.Name.StartsWith("IJob", StringComparison.Ordinal) &&
                            (contract.Namespace == "ME.BECS.Jobs" || contract.Namespace == "Unity.Jobs")))) return false;
                    result.Add(entry.kind, actual);
                }
                types = new Types { components = result["C"], aspects = result["A"], entityTypes = result["E"], jobs = result["J"] };
                reason = null;
                return true;
            } catch (Exception exception) { reason = exception.GetBaseException().Message; return false; }
        }

        private static readonly string[] CallbackPhases = { "OnInitialize", "Destroy", "OnAwake", "OnStart", "OnUpdate", "DoDestroy" };

        private static bool IsRoot(Type owner, MethodInfo root) =>
            typeof(ISystem).IsAssignableFrom(owner) && SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(owner).Contains(root) ||
            CallbackPhases.Any(phase => GetCallback(owner, phase) == root);

        internal static MethodInfo GetCallback(Type owner, string phase) {
            if (owner == null || owner.ContainsGenericParameters) return null;
            var contract = phase == nameof(IConfigInitialize.OnInitialize) ? typeof(IConfigInitialize) :
                phase == nameof(IComponentDestroy.Destroy) ? typeof(IComponentDestroy) : null;
            if (contract != null) {
                if (!owner.IsValueType || !contract.IsAssignableFrom(owner)) return null;
                var map = owner.GetInterfaceMap(contract);
                var indices = Enumerable.Range(0, map.InterfaceMethods.Length).Where(index => map.InterfaceMethods[index].Name == phase).ToArray();
                return indices.Length == 1 ? map.TargetMethods[indices[0]] : null;
            }
            if (!typeof(global::ME.BECS.Module).IsAssignableFrom(owner) || !new[] { "OnAwake", "OnStart", "OnUpdate", "DoDestroy" }.Contains(phase)) return null;
            var slots = typeof(global::ME.BECS.Module).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == phase && method.IsVirtual).ToArray();
            if (slots.Length != 1) return null;
            var slot = slots[0];
            for (var current = owner; current != null && typeof(global::ME.BECS.Module).IsAssignableFrom(current); current = current.BaseType) {
                foreach (var candidate in current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                    var definition = candidate.GetBaseDefinition();
                    if (definition.Module == slot.Module && definition.MetadataToken == slot.MetadataToken)
                        return candidate.IsAbstract ? null : candidate;
                }
            }
            return null;
        }

        private static bool TryRoot(Assembly publisher, string[] rows, out Type owner, out MethodInfo root) {
            owner = null;
            root = null;
            if (publisher == null || rows == null || rows.Length < 3) return false;
            var bindings = rows.Skip(3).Where(row => row.StartsWith("R\t", StringComparison.Ordinal) || row.StartsWith("U\t", StringComparison.Ordinal)).ToArray();
            if (bindings.Length != 1) return false;
            if (bindings[0].StartsWith("R\t", StringComparison.Ordinal)) {
                if (!SourceGeneratorSystemDependencies.TryRoot(publisher, rows, out root)) return false;
                owner = root.DeclaringType;
                return true;
            }
            var fields = bindings[0].Split('\t');
            var name = "ME.BECS.SourceGenerated.RuntimeUsageRoot_" + CodeGeneration.SourceGeneratorNames.Hash(publisher.FullName + "\n" + rows[0] + "\n" + rows[1]);
            if (fields.Length != 7 || fields[1] != publisher.FullName || fields[2] != name || fields[3] != "GetRoot" ||
                fields[4] != "GetOwner" || !CallbackPhases.Contains(fields[5]) || fields[6] != "v1" || !rows[1].StartsWith("M:", StringComparison.Ordinal)) return false;
            var holder = publisher.GetType(name, false);
            if (holder == null || !holder.IsVisible) return false;
            var methods = holder.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var ownerGetters = methods.Where(method => method.Name == fields[4]).ToArray();
            var rootGetters = methods.Where(method => method.Name == fields[3]).ToArray();
            if (ownerGetters.Length != 1 || rootGetters.Length != 1 || ownerGetters[0].ContainsGenericParameters || rootGetters[0].ContainsGenericParameters ||
                ownerGetters[0].ReturnType != typeof(Type) || rootGetters[0].ReturnType != typeof(MethodInfo) ||
                ownerGetters[0].GetParameters().Length != 0 || rootGetters[0].GetParameters().Length != 0) return false;
            owner = ownerGetters[0].Invoke(null, null) as Type;
            root = rootGetters[0].Invoke(null, null) as MethodInfo;
            return owner != null && owner.IsVisible && !owner.IsAbstract && !owner.ContainsGenericParameters && root != null &&
                rows[0] == (owner.IsGenericType ? owner.AssemblyQualifiedName : owner.FullName) && root == GetCallback(owner, fields[5]);
        }
    }
}
