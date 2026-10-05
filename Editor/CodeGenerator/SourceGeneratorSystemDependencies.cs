namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;
    using Dependencies = Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies;

    // Run-local source readers and export selection. Only the supplied analysis
    // delegate traverses IL; catalog readers call typeof/MethodInfo getters only.
    // Never executes lifecycle methods or initializes runtime registrations.
    // Dependency coverage is NOT proof that Complete() dominates direct accesses.
    internal sealed class SourceGeneratorSystemDependencies {
        private sealed class Entry {
            internal HashSet<TypeInfo> operations;
            internal List<Dependencies.Error> errors;
            internal string reason;
        }

        private readonly object gate = new object();
        private Assembly[] assemblies;
        private readonly Dictionary<(Assembly Publisher, bool Synchronization), string[][]> metadata = new Dictionary<(Assembly, bool), string[][]>();
        private readonly Dictionary<(MethodInfo Root, bool Synchronization), Entry> selected = new Dictionary<(MethodInfo, bool), Entry>();

        // Source-first oracle for comparison tests, separate from production export.
        // The two coverage domains remain independent here too.
        internal Dependencies Select(MethodInfo root, Func<MethodInfo, Dependencies> legacy) {
            if (root == null) return default;
            var hasOperations = this.TryRead(root, out var operations, out _);
            var hasSynchronization = this.TryReadSynchronization(root, out var errors, out _);
            if (!hasOperations || !hasSynchronization) {
                var fallback = legacy(root);
                if (!hasOperations) operations = fallback.ops;
                if (!hasSynchronization) errors = fallback.errors;
            }
            return new Dependencies(operations) { errors = new List<Dependencies.Error>(errors) };
        }

        internal Dependencies SelectForExport(MethodInfo root, Func<MethodInfo, Dependencies> legacy, out bool hasOperations, out bool hasSynchronization) {
            hasOperations = hasSynchronization = false;
            if (root == null) return default;
            // Both operations and advisory messages come from the same fresh IL
            // pass. A source catalog can neither replace nor veto this snapshot.
            // The Complete() heuristic is only a hint, never proof of safety.
            var analyzed = legacy(root);
            if (analyzed.ops == null) throw new InvalidOperationException("IL system dependency analysis returned no operations for " + root);
            return new Dependencies(analyzed.ops) { errors = new List<Dependencies.Error>(analyzed.errors) };
        }

        internal bool TryRead(MethodInfo root, out HashSet<TypeInfo> operations, out string reason) {
            operations = null;
            var entry = this.GetEntry(root, synchronization: false);
            reason = entry.reason;
            if (entry.operations == null) return false;
            operations = new HashSet<TypeInfo>(entry.operations);
            return true;
        }

        internal bool TryReadSynchronization(MethodInfo root, out List<Dependencies.Error> errors, out string reason) {
            errors = null;
            var entry = this.GetEntry(root, synchronization: true);
            reason = entry.reason;
            if (entry.errors == null) return false;
            errors = new List<Dependencies.Error>(entry.errors);
            return true;
        }

        private Entry GetEntry(MethodInfo root, bool synchronization) {
            if (root == null || root.ContainsGenericParameters || root.DeclaringType == null)
                return new Entry { reason = "Missing/open lifecycle root" };
            lock (this.gate) {
                var key = (root, synchronization);
                if (!this.selected.TryGetValue(key, out var entry)) {
                    try { entry = this.Read(root, synchronization); }
                    catch (Exception exception) { entry = new Entry { reason = exception.GetBaseException().Message }; }
                    this.selected.Add(key, entry);
                }
                return entry;
            }
        }

        private Entry Read(MethodInfo requested, bool synchronization) {
            var system = requested.DeclaringType;
            var identity = system.IsGenericType ? system.AssemblyQualifiedName : system.FullName;
            var expectedRoots = SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(system);
            if (!expectedRoots.Contains(requested)) return new Entry { reason = "Method is not a system lifecycle implementation" };
            var seen = new HashSet<MethodInfo>();
            Entry result = null;
            // Only explicit source diagnostics need to inventory catalog publishers.
            // Constructing the production selector must not scan loaded assemblies.
            var publishers = system.IsGenericType
                ? this.assemblies ??= AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                    .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal).ToArray()
                : new[] { system.Assembly };
            foreach (var publisher in publishers) {
                var key = (publisher, synchronization);
                if (!this.metadata.TryGetValue(key, out var entries)) {
                    var catalog = synchronization ? "ME.BECS.SystemSynchronization.v3" : "ME.BECS.SystemDependencies.v1";
                    entries = publisher.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == catalog && attribute.Value != null)
                        .Select(attribute => attribute.Value.Split('\n')).ToArray();
                    this.metadata.Add(key, entries);
                }
                foreach (var rows in entries.Where(rows => rows.Length > 0 && rows[0] == identity)) {
                    if (!TryRoot(publisher, rows, out var bound) || !expectedRoots.Contains(bound) || !seen.Add(bound))
                        return new Entry { reason = "Missing, foreign or duplicate lifecycle binding" };
                    if (bound != requested) continue;
                    if (synchronization) {
                        var valid = TryParseSynchronization(publisher, rows, out _, out var errors, out var reason);
                        result = new Entry { errors = valid ? errors : null, reason = reason };
                    } else {
                        var valid = TryParse(publisher, rows, out _, out var operations, out var reason);
                        result = new Entry { operations = valid ? operations : null, reason = reason };
                    }
                }
            }
            return result ?? new Entry { reason = synchronization ? "Missing compiler synchronization plan" : "Missing compiler dependency plan" };
        }

        internal static bool TryParseSynchronization(Assembly publisher, string[] rows, out MethodInfo root,
            out List<Dependencies.Error> errors, out string reason) {
            root = null;
            errors = null;
            reason = "Invalid compiler synchronization plan";
            if (publisher == null || !SourceGeneratorScheduledJobsValidation.ValidateSynchronizationSummary(rows, out var status)) return false;
            if (status == "incomplete") {
                reason = "Incomplete compiler synchronization coverage (gaps=" + rows[2] + "): " +
                    string.Join("; ", rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).Take(4).Select(row => row.Substring(2)));
                return false;
            }
            try {
                if (!TryRoot(publisher, rows, out var bound)) { reason = "Invalid compiler lifecycle binding"; return false; }
                var result = new List<Dependencies.Error>();
                if (status == "unproven") result.Add(new Dependencies.Error {
                    code = Dependencies.Error.Code.MethodCallRequired,
                    callerMethodInfo = bound,
                    message = "Method " + bound.DeclaringType.FullName + "." + bound.Name +
                        " may access component data while work is still pending. Review synchronization at these sites; this advisory analysis may miss existing guarantees. Source sites: " +
                        string.Join("; ", rows.Skip(3).Where(row => row.StartsWith("E\t", StringComparison.Ordinal)).Select(row => row.Substring(2))),
                });
                // Even with no component accesses, Complete can synchronize unrelated
                // side effects. This proof does not justify MethodNotRequired warnings.
                root = bound;
                errors = result;
                reason = null;
                return true;
            } catch (Exception exception) { reason = exception.GetBaseException().Message; return false; }
        }

        private static MethodInfo Getter(Type holder, string name, Type result) {
            if (holder == null || !holder.IsVisible) return null;
            var methods = holder.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.Name == name).ToArray();
            return methods.Length == 1 && !methods[0].ContainsGenericParameters && methods[0].GetParameters().Length == 0 && methods[0].ReturnType == result
                ? methods[0] : null;
        }

        internal static bool TryRoot(Assembly publisher, string[] rows, out MethodInfo root) {
            root = null;
            if (rows == null || rows.Length < 3 || string.IsNullOrEmpty(rows[0]) || !rows[1].StartsWith("M:", StringComparison.Ordinal)) return false;
            var bindings = rows.Skip(3).Where(row => row.StartsWith("R\t", StringComparison.Ordinal)).ToArray();
            if (bindings.Length != 1) return false;
            var fields = bindings[0].Split('\t');
            var name = "ME.BECS.SourceGenerated.SystemDirectRoot_" +
                CodeGeneration.SourceGeneratorNames.Hash(publisher.FullName + "\n" + rows[0] + "\n" + rows[1]);
            if (fields.Length != 5 || fields[1] != publisher.FullName || fields[2] != name || fields[3] != "GetRoot" || fields[4] != "v1") return false;
            var getter = Getter(publisher.GetType(name, false), "GetRoot", typeof(MethodInfo));
            if (getter == null) return false;
            root = getter.Invoke(null, null) as MethodInfo;
            return root != null && !root.ContainsGenericParameters && !root.IsStatic && root.DeclaringType != null &&
                rows[0] == (root.DeclaringType.IsGenericType ? root.DeclaringType.AssemblyQualifiedName : root.DeclaringType.FullName) &&
                SourceGeneratorScheduledJobsValidation.GetLifecycleMethods(root.DeclaringType).Contains(root);
        }

        internal static bool TryParse(Assembly publisher, string[] rows, out MethodInfo root, out HashSet<TypeInfo> operations, out string reason) {
            root = null;
            operations = null;
            reason = "Invalid compiler dependency plan";
            if (publisher == null || rows == null || rows.Length < 3) return false;
            if (rows[2] != "0") {
                reason = "Incomplete compiler dependency coverage (gaps=" + rows[2] + "): " +
                    string.Join("; ", rows.Skip(3).Where(row => row.StartsWith("G\t", StringComparison.Ordinal)).Take(4).Select(row => row.Substring(2)));
                return false;
            }
            try {
                if (!TryRoot(publisher, rows, out var bound)) { reason = "Invalid compiler lifecycle binding"; return false; }
                var componentRows = new List<(string Identity, byte Mode)>();
                var systemRows = new List<string>();
                var componentIdentities = new HashSet<string>(StringComparer.Ordinal);
                var systemIdentities = new HashSet<string>(StringComparer.Ordinal);
                string[] plan = null;
                foreach (var row in rows.Skip(3)) {
                    var fields = row.Split('\t');
                    if (fields[0] == "R") continue; // TryRoot validates the unique binding.
                    if (fields[0] == "P") {
                        if (plan != null || fields.Length != 4) return false;
                        plan = fields;
                    } else if (fields[0] == "C") {
                        if (fields.Length != 3 || !byte.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mode) || mode > 2 ||
                            fields[1] != mode.ToString(CultureInfo.InvariantCulture) || !componentIdentities.Add(fields[2])) return false;
                        componentRows.Add((fields[2], mode));
                    } else if (fields[0] == "Y") {
                        if (fields.Length != 2 || !systemIdentities.Add(fields[1])) return false;
                        systemRows.Add(fields[1]);
                    } else return false; // A complete plan cannot contain gaps or unknown rows.
                }
                var name = "ME.BECS.SourceGenerated.SystemDependencyPlan_" +
                    CodeGeneration.SourceGeneratorNames.Hash(publisher.FullName + "\n" + rows[0] + "\n" + rows[1]);
                if (plan == null || plan[1] != publisher.FullName || plan[2] != name || plan[3] != "v1") { reason = "Missing compiler typed dependency catalog"; return false; }
                var holder = publisher.GetType(name, false);
                var componentsGetter = Getter(holder, "GetComponents", typeof(Type[]));
                var modesGetter = Getter(holder, "GetModes", typeof(byte[]));
                var systemsGetter = Getter(holder, "GetSystems", typeof(Type[]));
                if (componentsGetter == null || modesGetter == null || systemsGetter == null) { reason = "Incomplete typed dependency getters"; return false; }
                var components = componentsGetter.Invoke(null, null) as Type[];
                var modes = modesGetter.Invoke(null, null) as byte[];
                var systems = systemsGetter.Invoke(null, null) as Type[];
                if (components == null || modes == null || systems == null || components.Length != componentRows.Count || modes.Length != components.Length ||
                    systems.Length != systemRows.Count || components.Distinct().Count() != components.Length || systems.Distinct().Count() != systems.Length) return false;
                var result = new HashSet<TypeInfo>();
                for (var index = 0; index < components.Length; ++index) {
                    var component = components[index];
                    if (component == null || !component.IsValueType || component.ContainsGenericParameters || !typeof(IComponentBase).IsAssignableFrom(component) ||
                        component.AssemblyQualifiedName != componentRows[index].Identity || modes[index] != componentRows[index].Mode) return false;
                    result.Add(new TypeInfo { type = component, op = (RefOp)modes[index] });
                }
                for (var index = 0; index < systems.Length; ++index) {
                    var system = systems[index];
                    if (system == null || !system.IsValueType || system.ContainsGenericParameters || !typeof(ISystem).IsAssignableFrom(system) ||
                        system.AssemblyQualifiedName != systemRows[index]) return false;
                    result.Add(new TypeInfo { type = system, op = RefOp.ReadWrite });
                }
                root = bound;
                operations = result;
                reason = null;
                return true;
            } catch (Exception exception) { reason = exception.GetBaseException().Message; return false; }
        }
    }
}
