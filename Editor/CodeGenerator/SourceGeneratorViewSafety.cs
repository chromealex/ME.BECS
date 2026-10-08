namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using ME.BECS.Views;
    using TypeInfo = Jobs.JobsEarlyInitCodeGenerator.TypeInfo;
    using SourceStatus = SourceGeneratorJobSafety.SourceStatus;

    // One export pass. Compiled IL selects production dependencies; source
    // catalogs remain a comparison oracle. Neither path invokes view callbacks.
    /// <summary>
    /// Provides view safety for BECS source-generator publication.
    /// </summary>
    public sealed class SourceGeneratorViewSafety {
        private readonly Dictionary<Assembly, Dictionary<string, string[]>> catalogs = new Dictionary<Assembly, Dictionary<string, string[]>>();
        private readonly Dictionary<(Type Owner, string Phase), HashSet<TypeInfo>> selected = new Dictionary<(Type, string), HashSet<TypeInfo>>();
        private readonly Dictionary<(Type Owner, string Phase), HashSet<TypeInfo>> exportedIL = new Dictionary<(Type, string), HashSet<TypeInfo>>();
        private readonly Func<Type, string, HashSet<TypeInfo>> legacy;
        private readonly object selectionLock = new object();

        /// <summary>
        /// Initializes <c>SourceGeneratorViewSafety</c> from the supplied defaults.
        /// </summary>
        public SourceGeneratorViewSafety() : this((owner, phase) => {
            var method = GetCallback(owner, phase);
            return method == null ? new HashSet<TypeInfo>() : Jobs.JobsEarlyInitCodeGenerator.GetMethodTypesInfo(method, useAnalyzer: false);
        }) { }

        internal SourceGeneratorViewSafety(Func<Type, string, HashSet<TypeInfo>> legacy) {
            this.legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        // Source-first comparison/testing path, isolated from production caches.
        /// <summary>
        /// Selects the component types accessed by the specified view owner and phase.
        /// </summary>
        public HashSet<TypeInfo> Select(Type owner, string phase) {
            lock (this.selectionLock) {
                if (this.selected.TryGetValue((owner, phase), out var cached)) return new HashSet<TypeInfo>(cached);
                var status = this.ReadSourceLocked(owner, phase, out _, out var source, out var reason);
                if (status == SourceStatus.Invalid) throw new InvalidOperationException("Invalid source view safety catalog for " + owner?.AssemblyQualifiedName +
                    " :: " + phase + ": " + reason + ". Recompile its source catalog and export Compare View Safety.");
                var result = status == SourceStatus.Complete ? source : this.legacy(owner, phase);
                this.selected.Add((owner, phase), new HashSet<TypeInfo>(result));
                return new HashSet<TypeInfo>(result);
            }
        }

        /// <summary>
        /// Selects for export.
        /// </summary>
        public HashSet<TypeInfo> SelectForExport(Type owner, string phase, bool module, out bool sourceSelected) {
            if (module) phase = ModulePhase(owner, phase);
            lock (this.selectionLock) {
                sourceSelected = false;
                if (!this.exportedIL.TryGetValue((owner, phase), out var dependencies)) {
                    var analyzed = this.legacy(owner, phase) ?? throw new InvalidOperationException("IL view safety analysis returned no result for " + owner + " :: " + phase);
                    dependencies = new HashSet<TypeInfo>(analyzed);
                    Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(dependencies);
                    this.exportedIL.Add((owner, phase), dependencies);
                }
                return new HashSet<TypeInfo>(dependencies);
            }
        }

        /// <summary>
        /// Selects module.
        /// </summary>
        public HashSet<TypeInfo> SelectModule(Type owner, string phase) => this.Select(owner, ModulePhase(owner, phase));

        private static string ModulePhase(Type owner, string phase) => typeof(EntityView).IsAssignableFrom(owner) ? "module:" + phase : phase;

        internal SourceStatus ReadSource(Type owner, string phase, out string[] rows, out HashSet<TypeInfo> source, out string reason) {
            lock (this.selectionLock) {
                var status = this.ReadSourceLocked(owner, phase, out rows, out source, out reason);
                if (rows != null) rows = (string[])rows.Clone();
                return status;
            }
        }

        private SourceStatus ReadSourceLocked(Type owner, string phase, out string[] rows, out HashSet<TypeInfo> source, out string reason) {
            rows = null;
            source = null;
            reason = "Expected a closed view/module type and ApplyState or ApplyStateParallel phase";
            var callbackPhase = phase != null && phase.StartsWith("module:", StringComparison.Ordinal) ? phase.Substring(7) : phase;
            if (owner == null || owner.ContainsGenericParameters || owner.IsAbstract ||
                (!typeof(EntityView).IsAssignableFrom(owner) && !typeof(IViewModule).IsAssignableFrom(owner)) ||
                (callbackPhase != "ApplyState" && callbackPhase != "ApplyStateParallel")) return SourceStatus.Invalid;
            try {
                if (!this.catalogs.TryGetValue(owner.Assembly, out var catalog)) {
                    catalog = new Dictionary<string, string[]>(StringComparer.Ordinal);
                    foreach (AssemblyMetadataAttribute attribute in owner.Assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)) {
                        if (attribute.Key != "ME.BECS.ViewSafety.v2" || attribute.Value == null) continue;
                        var entry = attribute.Value.Split('\n');
                        // Retain identifiable truncated records as invalid instead of hiding them as missing.
                        if (entry.Length < 2 || string.IsNullOrEmpty(entry[0]) || string.IsNullOrEmpty(entry[1])) continue;
                        var key = entry[0] + "\n" + entry[1];
                        if (catalog.ContainsKey(key)) catalog[key] = null;
                        else catalog.Add(key, entry.Skip(1).ToArray());
                    }
                    this.catalogs.Add(owner.Assembly, catalog);
                }
                var identity = owner.IsGenericType ? owner.AssemblyQualifiedName : owner.FullName;
                if (!catalog.TryGetValue(phase + "\n" + identity, out rows)) {
                    reason = "Missing source view callback catalog";
                    return SourceStatus.Missing;
                }
                var status = SourceGeneratorJobSafety.ClassifyRows(owner, rows, out reason);
                if (status != SourceStatus.Complete) return status;
                if (SourceGeneratorJobSafety.TryParse(owner, rows, out source)) return SourceStatus.Complete;
                reason = "Complete view catalog has invalid typed dependencies, identity or records";
            } catch (Exception exception) {
                reason = "Source view safety catalog inspection failed: " + exception.GetBaseException().Message;
            }
            source = null;
            return SourceStatus.Invalid;
        }

        // Resolve the callback actually dispatched by EntityView, including explicit
        // module implementations. Same-named overloads and hidden new slots are not callbacks.
        /// <summary>
        /// Returns callback.
        /// </summary>
        public static MethodInfo GetCallback(Type owner, string phase) {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var moduleRole = phase != null && phase.StartsWith("module:", StringComparison.Ordinal);
            if (moduleRole) phase = phase.Substring(7);
            if (phase != "ApplyState" && phase != "ApplyStateParallel") throw new ArgumentException("Unknown view callback phase", nameof(phase));
            if (!moduleRole && typeof(EntityView).IsAssignableFrom(owner)) {
                var slot = typeof(EntityView).GetMethod(phase, BindingFlags.Instance | BindingFlags.NonPublic);
                if (slot == null) throw new InvalidOperationException("View callback slot is unavailable: " + owner.AssemblyQualifiedName + " :: " + phase);
                return MostDerived(owner, slot);
            }
            var contract = phase == "ApplyState" ? typeof(IViewApplyState) : typeof(IViewApplyStateParallel);
            if (!contract.IsAssignableFrom(owner)) return null;
            var map = owner.GetInterfaceMap(contract);
            var index = Array.FindIndex(map.InterfaceMethods, method => method.Name == phase);
            if (index < 0) throw new InvalidOperationException("View module callback contract is unavailable: " + owner.AssemblyQualifiedName + " :: " + phase);
            return MostDerived(owner, map.TargetMethods[index]);
        }

        /// <summary>
        /// Returns module callback.
        /// </summary>
        public static MethodInfo GetModuleCallback(Type owner, string phase) => GetCallback(owner, ModulePhase(owner, phase));

        private static MethodInfo MostDerived(Type owner, MethodInfo slot) {
            if (!slot.IsVirtual) return slot;
            var definition = slot.GetBaseDefinition();
            for (var type = owner; type != null; type = type.BaseType) {
                var callback = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .SingleOrDefault(method => method.IsVirtual && method.GetBaseDefinition() == definition);
                if (callback != null) return callback;
            }
            return slot;
        }

        // Explicit diagnostic oracle only; differences never veto production IL selection.
        /// <summary>
        /// Compares the supplied values for ordering.
        /// </summary>
        public int Compare(Type owner, string phase, HashSet<TypeInfo> legacy, out string detail) {
            var coverage = this.ReadSource(owner, phase, out var summary, out _, out var reason);
            if (coverage != SourceStatus.Complete) {
                detail = coverage + ": " + reason;
                return -1;
            }
            var status = SourceGeneratorJobSafety.Validate(owner, summary, legacy, out _);
            detail = status == 1 ? "complete: dependencies equal" : status == 0 ? "complete: dependencies differ" : "complete metadata but typed catalog unavailable/invalid";
            if (status == 0) {
                var normalized = new HashSet<TypeInfo>(legacy);
                Jobs.JobsEarlyInitCodeGenerator.UpdateDeps(normalized);
                detail += "\n    source: " + string.Join("; ", summary.Skip(3).Where(row => row.StartsWith("D\t", StringComparison.Ordinal))) +
                    "\n    legacy: " + string.Join("; ", normalized.OrderBy(item => item.type.AssemblyQualifiedName, StringComparer.Ordinal)
                        .Select(item => item.type.AssemblyQualifiedName + " / " + item.op + " / argument=" + item.isArg));
            }
            return status;
        }

        /// <summary>
        /// Compares module.
        /// </summary>
        public int CompareModule(Type owner, string phase, HashSet<TypeInfo> legacy, out string detail) =>
            this.Compare(owner, ModulePhase(owner, phase), legacy, out detail);
    }
}
