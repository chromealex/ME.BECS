using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Globalization;
using ME.BECS.Views;
using ME.BECS.Editor.Jobs;
using scg = System.Collections.Generic;

namespace ME.BECS.Editor.Aspects {

    // Asset/type discovery and compiled IL supply snapshots; Roslyn emits the tracker.
    /// <summary>
    /// Exports entity view registration data for generated code.
    /// </summary>
    public class EntityViewCodeGenerator : CustomCodeGenerator {
        /// <summary>
        /// Source initialization kind used by <c>EntityViewCodeGenerator</c>.
        /// </summary>
        public override string SourceInitializationKind => this.GetType() == typeof(EntityViewCodeGenerator) ? "views" : base.SourceInitializationKind;
        /// <summary>
        /// Whether cache compiled inputs behavior or state is selected.
        /// </summary>
        public override bool CacheCompiledInputs => this.GetType() == typeof(EntityViewCodeGenerator);
        private Plan collected;

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare View Safety")]
        private static void CompareSafety() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before checking view safety.");
                return;
            }
            var comparison = new SourceGeneratorViewSafety();
            var details = new StringBuilder();
            var equal = 0;
            var different = 0;
            var unavailable = 0;
            var owners = UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>().Select(type => (type, module: false))
                .Concat(UnityEditor.TypeCache.GetTypesDerivedFrom<IViewModule>().Select(type => (type, module: true)))
                .Where(entry => !entry.type.IsAbstract && !entry.type.ContainsGenericParameters).Distinct()
                .OrderBy(entry => entry.type.AssemblyQualifiedName, StringComparer.Ordinal).ThenBy(entry => entry.module);
            foreach (var selection in owners) {
                var owner = selection.type;
                foreach (var phase in new[] { nameof(EntityView.ApplyState), nameof(EntityView.ApplyStateParallel) }) {
                    try {
                        var method = selection.module ? SourceGeneratorViewSafety.GetModuleCallback(owner, phase) : SourceGeneratorViewSafety.GetCallback(owner, phase);
                        if (method == null) continue;
                        var legacy = JobsEarlyInitCodeGenerator.GetMethodTypesInfo(method, useAnalyzer: false);
                        string detail;
                        var status = selection.module ? comparison.CompareModule(owner, phase, legacy, out detail) : comparison.Compare(owner, phase, legacy, out detail);
                        if (status == 1) ++equal; else if (status == 0) ++different; else ++unavailable;
                        details.AppendLine(owner.AssemblyQualifiedName + " :: " + (selection.module ? "module:" : "view:") + phase + " — " + detail);
                    } catch (System.Exception exception) {
                        ++unavailable;
                        details.AppendLine(owner.AssemblyQualifiedName + " :: " + phase + " — " + exception);
                    }
                }
            }
            SourceGeneratorReport.Publish("ViewSafety", "View callback safety: complete equal=" + equal + ", complete differences=" + different +
                ", incomplete/unavailable/invalid=" + unavailable + ". Raw callback dependencies before view tracking filters. " +
                "Fresh compiled IL vs source summaries; callbacks and initialization NOT invoked. Production uses IL snapshots; " +
                "source catalogs are diagnostics only. Comparison is not a selection gate.", details.ToString());
        }

        [UnityEditor.MenuItem("ME.BECS/Source Generator/Compare View Tracker Inputs")]
        private static void CompareInputs() {
            if (UnityEditor.EditorApplication.isCompiling) {
                UnityEngine.Debug.LogWarning("[ME.BECS] Wait for compilation before checking view tracker inputs.");
                return;
            }
            var details = new StringBuilder();
            details.AppendLine("Snapshot UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            var equal = 0;
            var differences = 0;
            var unavailable = 0;
            foreach (var editor in new[] { false, true }) {
                var name = editor ? "Editor" : "Runtime";
                try {
                    var owner = SourceGeneratorViewSelectionCatalog.GetAssembly(editor);
                    var feeder = new EntityViewCodeGenerator { editorAssembly = editor, asms = EditorUtils.GetAssembliesInfo() };
                    var expectedText = new StringBuilder();
                    feeder.AppendSourceGeneratorInputs(expectedText);
                    var expected = expectedText.ToString().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(row => row.StartsWith("view-tracker", StringComparison.Ordinal)).ToArray();
                    var actual = owner.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false).Cast<AssemblyMetadataAttribute>()
                        .Where(attribute => attribute.Key == "ME.BECS.ViewTrackerInputs.v1").Select(attribute => attribute.Value).ToArray();
                    var same = expected.SequenceEqual(actual, StringComparer.Ordinal);
                    if (same) ++equal; else ++differences;
                    var plan = feeder.Collect();
                    details.AppendLine(name + ": views/modules=" + plan.entries.Count + ", tracked components=" + plan.tracked.Length +
                        ", expected records=" + expected.Length + ", compiled records=" + actual.Length + ", equal=" + same +
                        ", owner=" + owner.GetName().Name + ", MVID=" + owner.ManifestModule.ModuleVersionId);
                } catch (System.Exception exception) {
                    ++unavailable;
                    details.AppendLine(name + ": " + exception);
                }
            }
            SourceGeneratorReport.Publish("ViewTracker", "View tracker inputs: equal=" + equal + ", differences=" + differences +
                ", unavailable=" + unavailable + ". Fresh compiled IL snapshots vs compiled source input metadata; initialization NOT invoked. " +
                "This checks selection and order, not Burst/player execution.", details.ToString());
        }
        private sealed class Entry {
            public Type type;
            public bool module;
            public bool ignored;
            public readonly scg::List<Type> components = new scg::List<Type>();
            public readonly scg::List<Phase> phases = new scg::List<Phase>();
        }

        private sealed class Phase {
            public string name;
            public string origin;
            public Type[] components = Array.Empty<Type>();
        }

        private sealed class Plan {
            public int capacity;
            public Type[] tracked;
            public readonly scg::List<Entry> entries = new scg::List<Entry>();
        }

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(scg::List<Type> references) {
            var plan = this.Collect();
            references.AddRange(plan.entries.Select(entry => entry.type));
            references.AddRange(plan.tracked);
        }

        /// <summary>
        /// Returns retired source files.
        /// </summary>
        public override scg::IEnumerable<string> GetRetiredSourceFiles() => new[] { "EntityView" };

        /// <summary>
        /// Adds this feature's registration inputs to the source-generator export.
        /// </summary>
        public override void AppendSourceGeneratorInputs(StringBuilder manifest) {
            var plan = this.Collect();
            void Append(string kind, int ordinal, string payload) => manifest.Append(kind).Append('\t')
                .Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))).Append('\n');
            Append("view-tracker", 0, "v3\n" + plan.capacity.ToString(CultureInfo.InvariantCulture));
            var views = 0;
            var modules = 0;
            foreach (var entry in plan.entries) {
                var payload = new StringBuilder(entry.type.AssemblyQualifiedName);
                if (entry.ignored) payload.Append("\nignored");
                else foreach (var phase in entry.phases) {
                    payload.Append("\nS\t").Append(phase.name).Append('\t').Append(phase.origin);
                    foreach (var component in phase.components)
                        payload.Append("\nC\t").Append(phase.name).Append('\t').Append(component.AssemblyQualifiedName);
                }
                Append(entry.module ? "view-tracker-module" : "view-tracker-view", entry.module ? modules++ : views++,
                    payload.ToString());
            }
            SourceGeneratorInputManifest.AppendViewPublicationOwners(manifest, plan.tracked,
                plan.entries.GroupBy(entry => entry.type).Select(group => (group.Key, group.First().module)).ToArray(),
                plan.entries.Where(entry => !entry.module).Select(entry => entry.type).OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray(),
                typeof(ViewsTracker), this.editorAssembly);
        }

        private Plan Collect() {
            if (this.collected != null) return this.collected;
            var views = UnityEditor.TypeCache.GetTypesDerivedFrom<EntityView>().OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, StringComparer.Ordinal).ToArray();
            var modules = UnityEditor.TypeCache.GetTypesDerivedFrom<IViewModule>().OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ThenBy(type => type.Assembly.FullName, StringComparer.Ordinal).ToArray();
            var plan = new Plan { capacity = views.Length + modules.Length };
            var safety = new SourceGeneratorViewSafety();
            var types = new scg::HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            foreach (var selection in new[] { (module: false, values: views), (module: true, values: modules) }) {
                foreach (var viewType in selection.values) {
                    if (viewType.IsAbstract || viewType.ContainsGenericParameters || !this.IsValidTypeForAssembly(viewType)) continue;
                    var entry = new Entry { type = viewType, module = selection.module };
                    plan.entries.Add(entry);
                    if (typeof(IViewIgnoreTracker).IsAssignableFrom(viewType)) { entry.ignored = true; continue; }
                    var ignored = new scg::HashSet<Type>(GetTrackingArguments(viewType, typeof(IViewTrackIgnore<>)));
                    void Add(JobsEarlyInitCodeGenerator.TypeInfo component) {
                        entry.components.Add(component.type);
                        types.Add(component);
                    }
                    foreach (var methodName in new[] { nameof(EntityView.ApplyState), nameof(EntityView.ApplyStateParallel) }) {
                        var method = selection.module ? SourceGeneratorViewSafety.GetModuleCallback(viewType, methodName) : SourceGeneratorViewSafety.GetCallback(viewType, methodName);
                        // Even an absent callback is an explicit empty IL snapshot,
                        // not permission for a source catalog to supply dependencies.
                        var phase = new Phase { name = methodName, origin = "il" };
                        entry.phases.Add(phase);
                        if (method == null) continue;
                        var dependencies = safety.SelectForExport(viewType, methodName, selection.module, out _);
                        phase.components = OrderTrackingTypes(dependencies.Select(item => item.type));
                        foreach (var component in dependencies) {
                            if (component.op != RefOp.ReadOnly)
                                UnityEngine.Debug.LogWarning($"EntityView {viewType.FullName} writes to {component.type.FullName} in method {methodName}, be sure this view has been used in Visual mode world only");
                            if (!ignored.Contains(component.type)) Add(component);
                        }
                    }
                    foreach (var type in GetTrackingArguments(viewType, typeof(IViewTrack<>))) {
                        if (typeof(IAspect).IsAssignableFrom(type)) {
                            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                                if (typeof(IAspectData).IsAssignableFrom(field.FieldType) && field.GetCustomAttribute(typeof(QueryWithAttribute)) != null)
                                    Add(new JobsEarlyInitCodeGenerator.TypeInfo { type = field.FieldType.GenericTypeArguments[0], op = RefOp.ReadOnly });
                        } else Add(new JobsEarlyInitCodeGenerator.TypeInfo { type = type, op = RefOp.ReadOnly });
                    }
                }
            }
            // Tracker IDs must not depend on reflection/hash enumeration or process culture.
            // Access modes affect diagnostics, not the unique set of tracked component types.
            plan.tracked = OrderTrackingTypes(types.Select(type => type.type));
            return this.collected = plan;
        }

        internal static Type[] GetTrackingArguments(Type owner, Type contract) {
            if (contract != typeof(IViewTrack<>) && contract != typeof(IViewTrackIgnore<>))
                throw new ArgumentException("Expected a view tracking interface definition.", nameof(contract));
            // Open generic IsAssignableFrom does not match constructed interfaces.
            // GetInterfaces includes inherited contracts; identity also excludes unrelated
            // user interfaces with the same short name. Explicit opt-in order is canonical.
            return OrderTrackingTypes(owner.GetInterfaces().Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == contract)
                .Select(type => type.GetGenericArguments()[0]));
        }

        internal static Type[] OrderTrackingTypes(scg::IEnumerable<Type> types) => types.Distinct()
            .OrderBy(type => type.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();
    }
}
