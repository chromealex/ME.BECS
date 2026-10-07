namespace ME.BECS.Editor {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    // The input manifest is ~20 MB and only ever appended to while owners are
    // selected. Each owner step used to copy and split the whole builder again
    // (about ten times per profile); split only the newly appended tail instead.
    internal static class ManifestRows {
        [ThreadStatic] private static StringBuilder owner;
        [ThreadStatic] private static int length;
        [ThreadStatic] private static List<string> complete;
        [ThreadStatic] private static string tail;

        // Type.GetType(assembly-qualified name) parses and binds the name every time;
        // owner selection resolves the same few thousand names repeatedly.
        // Assembly.GetName() builds a new AssemblyName each call (Mono parses it).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Reflection.Assembly, string> assemblyNames =
            new System.Collections.Concurrent.ConcurrentDictionary<System.Reflection.Assembly, string>();
        internal static string NameOf(System.Reflection.Assembly assembly) => assemblyNames.GetOrAdd(assembly, item => item.GetName().Name);

        internal static Type ResolveType(string name) =>
            ILAnalysisSession.Get((typeof(ManifestRows), "type", name), () => Type.GetType(name, true));

        internal static string[] Of(StringBuilder manifest) {
            if (!ReferenceEquals(owner, manifest) || manifest.Length < length || complete == null ||
                (length > 0 && tail.Length > 0 && manifest[length - 1] != tail[tail.Length - 1])) {
                owner = manifest; length = 0; complete = new List<string>(); tail = "";
            }
            if (manifest.Length > length) {
                var added = tail + manifest.ToString(length, manifest.Length - length);
                var parts = added.Split('\n');
                for (var i = 0; i < parts.Length - 1; ++i) complete.Add(parts[i]);
                tail = parts[parts.Length - 1];
                length = manifest.Length;
            }
            var rows = new string[complete.Count + 1];
            complete.CopyTo(rows);
            rows[rows.Length - 1] = tail;
            return rows;
        }
    }

    // Ownership is a compilation property, not just Type.Assembly. A closed
    // System<Component> can require references to two otherwise unrelated asmdefs.
    internal static class SourceGeneratorRegistrationOwners {
        internal static void AppendThemeMenus(StringBuilder manifest, bool editor) {
            if (!editor) return;
            var entry = CodeGeneration.SourceGeneratorThemeMenuFragmentFormat.EntryValue(ManifestRows.Of(manifest));
            // Themes already owns the Editor API; no bridge or gameplay reference
            // is needed. The additional file remains a project asset.
            manifest.Append("thememenu-publication-schema\t0\tdjE=\nthememenu-registration-owner\t0\t")
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(entry)).Append('\t')
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(ManifestRows.NameOf(typeof(Themes).Assembly))).Append('\n');
        }

        internal static void AppendInputCatalog(StringBuilder manifest, bool editor) {
            // Metadata only: no gameplay type or addon reference is needed.
            var owner = SourceGeneratorPublicationBridges.Select(RequiredAssemblies(typeof(BootstrapRuntime)), editor);
            manifest.Append("inputcatalog-publication-schema\t0\tdjE=\ninputcatalog-registration-owner\t0\tdjE=\t")
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
        }

        internal static void AppendSystemDependencies(StringBuilder manifest, bool editor) {
            if (!editor) return;
            var entry = CodeGeneration.SourceGeneratorSystemDependencyFragmentFormat.EntryValue(ManifestRows.Of(manifest));
            var types = new HashSet<Type> { typeof(ComponentDependencyGraphInfo) };
            foreach (var record in CodeGeneration.SourceGeneratorSystemDependencyFragmentFormat.Rows(entry)) {
                var fields = record.Split('\t');
                if (fields[0] != "system-dependencies") continue;
                var rows = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(fields[2]).Split('\n');
                types.Add(ManifestRows.ResolveType(rows[1]));
                foreach (var row in rows.Skip(2).Select(value => value.Split('\t')))
                    if (row[0] == "M" || row[0] == "D") types.Add(ManifestRows.ResolveType(row[1]));
                    else if (row[0] == "C") types.Add(ManifestRows.ResolveType(row[2]));
            }
            var required = types.SelectMany(type => RequiredAssembliesCore(type, allowOpen: true)).Distinct(StringComparer.Ordinal).ToArray();
            var owner = SourceGeneratorPublicationBridges.Select(required, editor: true);
            manifest.Append("systemdependency-publication-schema\t0\tdjE=\nsystemdependency-registration-owner\t0\t")
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(entry)).Append('\t')
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
        }

        internal static void AppendViewSelection(StringBuilder manifest, bool editor) {
            var rows = ManifestRows.Of(manifest);
            if (!rows.Any(row => row.StartsWith("view-type-schema\t", StringComparison.Ordinal))) return;
            var payload = CodeGeneration.SourceGeneratorViewSelectionFragmentFormat.EntryValue(rows);
            var types = new HashSet<Type> { ManifestRows.ResolveType("ME.BECS.Views.BootstrapViews, ME.BECS.Views") };
            foreach (var row in CodeGeneration.SourceGeneratorViewSelectionFragmentFormat.Rows(payload).Select(row => row.Split('\t'))) {
                if (row[0] == "views-registration-owner") {
                    if (!CodeGeneration.SourceGeneratorViewsFragmentFormat.TryEntry(CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(row[2]), out var entry))
                        throw new InvalidOperationException("Invalid Views registration selection.");
                    types.Add(ManifestRows.ResolveType(entry.Component));
                } else if (row[0] == "view-tracker-view" || row[0] == "view-tracker-module") {
                    var lines = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(row[2]).Split('\n');
                    types.Add(ManifestRows.ResolveType(lines[0]));
                    // Include IL dependencies even when filtered out later. Private
                    // metadata is inspected, never emitted as inaccessible C#.
                    foreach (var dependency in lines.Skip(1).Where(line => line.StartsWith("C\t", StringComparison.Ordinal)))
                        types.Add(ManifestRows.ResolveType(dependency.Split('\t')[2]));
                }
            }
            var required = types.SelectMany(RequiredAssemblies).Concat(new[] {
                ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
            }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            var owner = SourceGeneratorPublicationBridges.Select(required, editor);
            manifest.Append("viewselection-publication-schema\t0\tdjE=\nviewselection-registration-owner\t0\t")
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(payload)).Append('\t')
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
        }

        internal static void AppendBootstrap(StringBuilder manifest, bool editor) {
            var profile = CodeGeneration.SourceGeneratorBootstrapFragmentFormat.Create(ManifestRows.Of(manifest), editor);
            var required = new HashSet<string>(RequiredAssemblies(typeof(BootstrapRuntime)), StringComparer.Ordinal) {
                ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
            };
            if (editor) required.Add(ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly));
            if (profile.Selections.Any(item => item.Kind == "Network")) required.UnionWith(RequiredAssemblies(ManifestRows.ResolveType("ME.BECS.Network.BootstrapNetworkMethods, ME.BECS.Network")));
            if (profile.Initialize.Contains("views")) required.UnionWith(RequiredAssemblies(ManifestRows.ResolveType("ME.BECS.Views.BootstrapViews, ME.BECS.Views")));
            var owner = SourceGeneratorPublicationBridges.Select(required.ToArray(), editor);
            manifest.Append("bootstrap-publication-schema\t0\tdjE=\nbootstrap-registration-owner\t0\t")
                .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(CodeGeneration.SourceGeneratorBootstrapFragmentFormat.EntryValue(profile)))
                .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
        }

        internal sealed class Candidate {
            internal readonly string name;
            internal readonly bool editorOnly;
            internal readonly HashSet<string> references;
            internal readonly HashSet<string> playerReferences;
            internal readonly bool editorUnsafe;
            internal readonly bool playerUnsafe;
            internal Candidate(string name, bool editorOnly, IEnumerable<string> references) : this(name, editorOnly, references, references) { }
            internal Candidate(string name, bool editorOnly, IEnumerable<string> references, IEnumerable<string> playerReferences)
                : this(name, editorOnly, references, playerReferences, true, true) { }
            internal Candidate(string name, bool editorOnly, IEnumerable<string> references, IEnumerable<string> playerReferences, bool editorUnsafe, bool playerUnsafe) {
                this.name = name;
                this.editorOnly = editorOnly;
                this.references = new HashSet<string>(references, StringComparer.Ordinal) { name };
                this.playerReferences = new HashSet<string>(playerReferences, StringComparer.Ordinal) { name };
                this.editorUnsafe = editorUnsafe;
                this.playerUnsafe = playerUnsafe;
            }
        }

        internal static string[] RequiredAssemblies(Type type) => RequiredAssembliesCore(type, false);

        // A concrete type's assembly/constraint closure is immutable for the
        // loaded compilation. Reuse it across publication slots in this analysis
        // session; never retain it across unrelated compilation snapshots.
        private static string[] RequiredAssembliesCore(Type type, bool allowOpen) =>
            ILAnalysisSession.Get((typeof(SourceGeneratorRegistrationOwners), type, allowOpen),
                () => CollectRequiredAssemblies(type, allowOpen));

        private static string[] CollectRequiredAssemblies(Type type, bool allowOpen) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (!allowOpen && type.ContainsGenericParameters) throw new ArgumentException("A registration needs a closed type.", nameof(type));
            var names = new HashSet<string>(StringComparer.Ordinal) { ManifestRows.NameOf(typeof(ISystem).Assembly) };
            var visited = new HashSet<Type>();
            void Add(Type value) {
                if (value.HasElementType) { Add(value.GetElementType()); return; }
                if (value.IsGenericParameter || !visited.Add(value)) return;
                names.Add(ManifestRows.NameOf(value.Assembly));
                if (value.IsGenericType) {
                    foreach (var argument in value.GetGenericArguments()) Add(argument);
                    // Naming a concrete argument is not enough when binding the
                    // generated generic method also needs an external constraint.
                    foreach (var parameter in value.GetGenericTypeDefinition().GetGenericArguments())
                        foreach (var constraint in parameter.GetGenericParameterConstraints()) Add(constraint);
                }
            }
            Add(type);
            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        internal static string Choose(string declaringAssembly, string[] required, bool editor, IEnumerable<Candidate> candidates) {
            var candidate = FindCandidate(declaringAssembly, required, editor, candidates);
            if (candidate == null) throw new InvalidOperationException("No existing " + (editor ? "Editor" : "Runtime") +
                " script assembly can publish a registration requiring [" + string.Join(", ", required) +
                "]. Add an explicit reference in an appropriate owning asmdef; generated aggregate assemblies are not publication owners.");
            return candidate.name;
        }

        private static Candidate FindCandidate(string declaringAssembly, string[] required, bool editor, IEnumerable<Candidate> candidates) {
            // Prefer the definition's own compilation when it can name the full
            // specialization. Otherwise prefer an argument owner, then the smallest
            // existing reference surface. All ties are ordinal and explicit.
            // Single pass (same order as OrderBy/ThenBy/FirstOrDefault): this runs
            // for thousands of registrations against ~200 candidates.
            Candidate best = null;
            int bestRank = 0, bestEditor = 0, bestCount = 0;
            foreach (var item in candidates) {
                if (item.name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal) || !editor && item.editorOnly) continue;
                var references = editor ? item.references : item.playerReferences;
                var eligible = true;
                foreach (var name in required) if (!references.Contains(name)) { eligible = false; break; }
                if (!eligible) continue;
                var rank = item.name == declaringAssembly ? 0 : Array.IndexOf(required, item.name) >= 0 ? 1 : 2;
                var editorRank = item.editorOnly ? 1 : 0;
                var count = references.Count;
                if (best != null) {
                    var order = rank.CompareTo(bestRank);
                    if (order == 0) order = editorRank.CompareTo(bestEditor);
                    if (order == 0) order = count.CompareTo(bestCount);
                    if (order == 0) order = string.CompareOrdinal(item.name, best.name);
                    if (order >= 0) continue;
                }
                best = item; bestRank = rank; bestEditor = editorRank; bestCount = count;
            }
            return best;
        }

        internal static Candidate[] CurrentCandidates() =>
            ILAnalysisSession.Get((typeof(SourceGeneratorRegistrationOwners), "publication-candidates"), CollectCandidates);

        private static Candidate[] CollectCandidates() {
            // EditorAssembly alone does not cover asmdefs excluded from the player
            // by define constraints. A Runtime publisher must exist in that target's
            // actual player compilation inventory as well.
            var player = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player)
                .ToDictionary(assembly => assembly.name, StringComparer.Ordinal);
            IEnumerable<string> References(UnityEditor.Compilation.Assembly assembly) => assembly == null ? Array.Empty<string>() :
                assembly.assemblyReferences.Select(reference => reference.name).Concat(
                    assembly.compiledAssemblyReferences.Select(path => Path.GetFileNameWithoutExtension(path)));
            return UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Where(assembly => !assembly.name.StartsWith("ME.BECS.Gen.", StringComparison.Ordinal))
                .Select(assembly => {
                    player.TryGetValue(assembly.name, out var runtime);
                    return new Candidate(assembly.name, (assembly.flags & UnityEditor.Compilation.AssemblyFlags.EditorAssembly) != 0 || runtime == null,
                        References(assembly), References(runtime), assembly.compilerOptions.AllowUnsafeCode,
                        runtime != null && runtime.compilerOptions.AllowUnsafeCode);
                })
                .ToArray();
        }

        internal static void Append(StringBuilder manifest, IEnumerable<Type> systems, bool editor) {
            var candidates = CurrentCandidates();
            manifest.Append("system-registration-owners-schema\t0\tdjE=\n");
            manifest.Append("system-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            foreach (var system in systems) {
                string owner;
                try { owner = ChoosePublication(ManifestRows.NameOf(system.Assembly), RequiredAssemblies(system), editor, candidates); }
                catch (InvalidOperationException error) { throw new InvalidOperationException(system.AssemblyQualifiedName + ": " + error.Message, error); }
                manifest.Append("system-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(system.AssemblyQualifiedName)))
                    .Append('\t').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(owner))).Append('\n');
            }
        }

        internal static void AppendGraphs(StringBuilder manifest, bool editor) {
            manifest.Append("graph-publication-schema\t0\tdjE=\n");
            if (editor) return;
            var ordinal = 0;
            foreach (var entry in CodeGeneration.SourceGeneratorGraphFragmentFormat.Entries(ManifestRows.Of(manifest))) {
                if (!CodeGeneration.SourceGeneratorGraphFragmentFormat.ValidEntry(entry)) throw new InvalidOperationException("Invalid graph publication snapshot.");
                var types = new HashSet<Type> {
                    typeof(ME.BECS.FeaturesGraph.SystemsGraph), typeof(Unity.Burst.BurstCompileAttribute),
                    typeof(Unity.Collections.NativeArray<>), typeof(Unity.Jobs.JobHandle), typeof(AOT.MonoPInvokeCallbackAttribute),
                    typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute), typeof(UnityEngine.Scripting.PreserveAttribute),
                };
                foreach (var row in CodeGeneration.SourceGeneratorGraphFragmentFormat.Rows(entry).Select(value => value.Split('\t'))) {
                    string Decode(string value) => CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(value);
                    if (row[0] == "graph-system" || row[0] == "graph-job-selection") types.Add(ManifestRows.ResolveType(Decode(row[2])));
                    if (row[0] == "graph-job-selection") {
                        foreach (var job in Decode(row[4]).Split('\n').Skip(2).Where(value => value.Length != 0)) types.Add(ManifestRows.ResolveType(job));
                    } else if (row[0] == "graph-topology") {
                        foreach (var node in Decode(row[4]).Split('\n').Where(value => value.StartsWith("node\t", StringComparison.Ordinal)).Select(value => value.Split('\t'))) {
                            types.Add(ManifestRows.ResolveType(Decode(node[3])));
                            var system = Decode(node[6]);
                            if (system.Length != 0) types.Add(ManifestRows.ResolveType(system));
                        }
                    }
                }
                var required = types.SelectMany(type => RequiredAssembliesCore(type, allowOpen: true)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                // Graphs have no declaring assembly. Their IL-selected jobs and
                // private setters must already be compiled, so use a downstream
                // project bridge keyed by the exact required reference surface.
                var owner = SourceGeneratorPublicationBridges.Select(required, editor: false);
                manifest.Append("graph-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(entry))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendEntities(StringBuilder manifest, IEnumerable<Type> entities, bool editor) {
            var candidates = CurrentCandidates();
            manifest.Append("entity-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            foreach (var entity in entities) {
                var owner = ChoosePublication(ManifestRows.NameOf(entity.Assembly), RequiredAssemblies(entity), editor, candidates);
                manifest.Append("entity-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(entity.AssemblyQualifiedName))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendAspects(StringBuilder manifest, IEnumerable<Type> aspects, bool editor) {
            var candidates = CurrentCandidates();
            manifest.Append("aspect-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            foreach (var aspect in aspects) {
                var owner = ChoosePublication(ManifestRows.NameOf(aspect.Assembly), RequiredAssemblies(aspect), editor, candidates);
                manifest.Append("aspect-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(aspect.AssemblyQualifiedName))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static string ChooseUnsafe(string declaringAssembly, string[] required, bool editor, IEnumerable<Candidate> candidates) =>
            Choose(declaringAssembly, required, editor, candidates.Where(item => editor ? item.editorUnsafe : item.playerUnsafe));

        internal static string ChoosePublication(string declaringAssembly, string[] required, bool editor, IEnumerable<Candidate> candidates, bool unsafeCode = false) {
            // Reuse ordinary owners first. A bridge is keyed by the exact required
            // surface, not whichever older generated bridge happens to sort first.
            var ordinary = candidates.Where(item => !SourceGeneratorPublicationBridges.IsBridge(item.name) &&
                (!unsafeCode || (editor ? item.editorUnsafe : item.playerUnsafe)));
            // Thousands of types share a few (assembly, required) surfaces: memoize the
            // pure ordinary-owner choice for the session inventory. Bridge selection
            // stays uncached because it records the plan in the current planning scope.
            var owner = candidates is Candidate[] inventory
                ? ILAnalysisSession.Get((typeof(SourceGeneratorRegistrationOwners), "ordinary-owner", inventory, declaringAssembly,
                    string.Join("\n", required), editor, unsafeCode), () => FindCandidate(declaringAssembly, required, editor, ordinary)?.name)
                : FindCandidate(declaringAssembly, required, editor, ordinary)?.name;
            return owner ?? SourceGeneratorPublicationBridges.Select(required, editor);
        }

        internal static void AppendDestroy(StringBuilder manifest, IEnumerable<Type> components, bool editor) {
            var candidates = CurrentCandidates();
            manifest.Append("destroy-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            foreach (var component in components) {
                var required = RequiredAssemblies(component).Concat(new[] {
                    ManifestRows.NameOf(typeof(Unity.Burst.BurstCompileAttribute).Assembly),
                    ManifestRows.NameOf(typeof(AOT.MonoPInvokeCallbackAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).ToArray();
                var owner = ChoosePublication(ManifestRows.NameOf(component.Assembly), required, editor, candidates, unsafeCode: true);
                manifest.Append("destroy-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(component.AssemblyQualifiedName))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendTypes(StringBuilder manifest, Type[] components, Type[] groups, bool editor) {
            var candidates = CurrentCandidates();
            manifest.Append("type-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            var owners = new Dictionary<Type, string>();
            void Add(Type component, string phase, Type group = null) {
                if (group != null || !owners.TryGetValue(component, out _)) {
                    var required = RequiredAssemblies(component).AsEnumerable();
                    if (group != null) required = required.Concat(RequiredAssembliesCore(group, true));
                    var owner = ChoosePublication(ManifestRows.NameOf(component.Assembly), required.Distinct(StringComparer.Ordinal).ToArray(), editor, candidates);
                    AppendRow(owner);
                    if (group == null) owners.Add(component, owner);
                } else AppendRow(owners[component]);
                void AppendRow(string owner) {
                    var value = CodeGeneration.SourceGeneratorTypeFragmentFormat.EntryValue(phase, component.AssemblyQualifiedName, group?.AssemblyQualifiedName);
                    manifest.Append("type-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(value))
                        .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
                }
            }
            foreach (var component in groups) {
                var attributes = component.GetCustomAttributesData().Where(item => item.AttributeType == typeof(ComponentGroupAttribute)).ToArray();
                if (attributes.Length != 1 || attributes[0].ConstructorArguments.Count != 1)
                    throw new InvalidOperationException("Expected exactly one component group for " + component.AssemblyQualifiedName);
                var group = attributes[0].ConstructorArguments[0].Value as Type;
                foreach (var argument in attributes[0].NamedArguments) if (argument.MemberName == "groupType") group = argument.TypedValue.Value as Type;
                if (group == null) throw new InvalidOperationException("Invalid component group for " + component.AssemblyQualifiedName);
                Add(component, "Group", group);
            }
            foreach (var phase in new[] { (Name: "Register", Contract: (Type)null), (Name: "RegisterShared", Contract: typeof(IComponentShared)),
                         (Name: "RegisterStatic", Contract: typeof(IConfigComponentStatic)), (Name: "RegisterConfig", Contract: typeof(IConfigInitialize)) })
                foreach (var component in components) if (phase.Contract == null || phase.Contract.IsAssignableFrom(component)) Add(component, phase.Name);
        }

        internal static void AppendNetwork(StringBuilder manifest, bool editor) {
            // Consume the optional feeder's existing ordered selection, without a
            // compile-time reference from core Editor to the Network addon.
            var rows = ManifestRows.Of(manifest);
            if (!rows.Any(row => row.StartsWith("network-method-schema\t", StringComparison.Ordinal))) return;
            var candidates = CurrentCandidates();
            var contract = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType("ME.BECS.Network.NetworkMethodDelegate", false)).Single(type => type != null);
            manifest.Append("network-publication-schema\t0\tdjE=\n");
            foreach (var row in rows.Where(row => row.StartsWith("network-method\t", StringComparison.Ordinal))) {
                var fields = row.Split('\t');
                var identity = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(fields[2]).Split('\n');
                var type = ManifestRows.ResolveType(identity[0]);
                var required = RequiredAssemblies(type).Concat(RequiredAssemblies(contract)).Concat(new[] {
                    ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).ToArray();
                var owner = ChoosePublication(ManifestRows.NameOf(type.Assembly), required, editor, candidates);
                manifest.Append("network-registration-owner\t").Append(fields[1]).Append('\t')
                    .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(CodeGeneration.SourceGeneratorNetworkFragmentFormat.EntryValue(identity[0], identity[1])))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendJobInit(StringBuilder manifest, bool editor) {
            string[] rows;
            using (CodeGeneratorTimings.Measure("JobInit owners: manifest rows"))
                rows = ManifestRows.Of(manifest).Where(row => row.StartsWith("job-early-init\t", StringComparison.Ordinal)).ToArray();
            var candidates = CurrentCandidates();
            manifest.Append("jobinit-publication-schema\t0\tdjE=\n");
            foreach (var row in rows) {
                var fields = row.Split('\t');
                var payload = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(fields[2]);
                var values = payload.Split('\n');
                var job = ManifestRows.ResolveType(values[1]);
                var targets = new System.Collections.Generic.List<Type> { job };
                if (values[2].Length != 0) {
                    targets.Add(ManifestRows.ResolveType(values[2]));
                    targets.AddRange(values.Skip(4).Select(identity => ManifestRows.ResolveType(identity)));
                }
                var requiredScope = CodeGeneratorTimings.Measure("JobInit owners: required assemblies");
                var required = targets.SelectMany(RequiredAssemblies).Concat(new[] {
                    ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).ToArray();
                requiredScope.Dispose();
                string owner;
                using (CodeGeneratorTimings.Measure("JobInit owners: choose"))
                try { owner = ChoosePublication(ManifestRows.NameOf(job.Assembly), required, editor, candidates); }
                catch (InvalidOperationException error) { throw new InvalidOperationException("EarlyInit slot " + fields[1] + " (" + job.AssemblyQualifiedName + "): " + error.Message, error); }
                using (CodeGeneratorTimings.Measure("JobInit owners: append entry"))
                manifest.Append("jobinit-registration-owner\t").Append(fields[1]).Append('\t')
                    .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(CodeGeneration.SourceGeneratorJobInitFragmentFormat.EntryValue(payload)))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendJobDebug(StringBuilder manifest, bool editor) {
            var rows = ManifestRows.Of(manifest).Where(row => row.StartsWith("job-debug\t", StringComparison.Ordinal)).ToArray();
            var candidates = CurrentCandidates();
            manifest.Append("jobdebug-publication-schema\t0\tdjE=\n");
            foreach (var row in rows) {
                var fields = row.Split('\t');
                var payload = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(fields[2]);
                var debug = payload.Split('\n');
                var job = ManifestRows.ResolveType(debug[1]);
                var dependencies = new HashSet<Type> { job, ManifestRows.ResolveType(debug[2]) };
                if (debug[3].Length != 0) dependencies.Add(ManifestRows.ResolveType(debug[3]));
                foreach (var dependency in debug.Skip(5).Select(value => value.Split('\t'))) {
                    if (dependency[0] == "S" && dependency.Length == 2) continue;
                    dependencies.Add(ManifestRows.ResolveType(dependency[dependency.Length - 1]));
                }
                var required = dependencies.SelectMany(RequiredAssemblies).Concat(new[] {
                    ManifestRows.NameOf(typeof(Unity.Burst.BurstCompiler).Assembly),
                    ManifestRows.NameOf(typeof(Unity.Collections.Allocator).Assembly),
                    // Allocator lives in UnityEngine.CoreModule, not the Collections
                    // package. Cuts allocation overloads expose UnsafeList<T> too.
                    ManifestRows.NameOf(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeList<>).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).ToArray();
                var owner = ChoosePublication(ManifestRows.NameOf(job.Assembly), required, editor, candidates, unsafeCode: true);
                manifest.Append("jobdebug-registration-owner\t").Append(fields[1]).Append('\t')
                    .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(CodeGeneration.SourceGeneratorJobDebugFragmentFormat.EntryValue(payload)))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendJobSetup(StringBuilder manifest, bool editor) {
            var rows = ManifestRows.Of(manifest);
            var entities = rows.Where(row => row.StartsWith("entity-registration\t", StringComparison.Ordinal)).Select(row => row.Split('\t'))
                .OrderBy(row => int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var groups = entities.ToDictionary(row => {
                var type = ManifestRows.ResolveType(CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(row[2]));
                return type.Assembly.FullName + "\tT:" + type.FullName.Replace('+', '.');
            }, row => new KeyValuePair<int, string>(int.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture),
                CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(row[2])), StringComparer.Ordinal);
            var entries = CodeGeneration.SourceGeneratorJobSetupFragmentFormat.Selection(rows, groups, entities.Length);
            var candidates = CurrentCandidates();
            manifest.Append("jobsetup-publication-schema\t0\tdjE=\n");
            for (var ordinal = 0; ordinal < entries.Length; ++ordinal) {
                var fields = CodeGeneration.SourceGeneratorJobSetupFragmentFormat.Unpack(entries[ordinal]);
                var job = ManifestRows.ResolveType(fields[1]);
                var dependencies = new HashSet<Type> { job };
                foreach (var row in fields[5].Split('\n')) {
                    var debug = CodeGeneration.SourceGeneratorSystemFragmentFormat.Decode(row.Split('\t')[2]).Split('\n');
                    dependencies.Add(ManifestRows.ResolveType(debug[2]));
                    if (debug[3].Length != 0) dependencies.Add(ManifestRows.ResolveType(debug[3]));
                    foreach (var dependency in debug.Skip(5).Select(value => value.Split('\t'))) {
                        if (dependency[0] == "S" && dependency.Length == 2) continue;
                        dependencies.Add(ManifestRows.ResolveType(dependency[dependency.Length - 1]));
                    }
                }
                foreach (var row in fields[6].Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    dependencies.Add(ManifestRows.ResolveType(row.Split('\t')[1]));
                var required = dependencies.SelectMany(RequiredAssemblies).Concat(new[] {
                    ManifestRows.NameOf(typeof(Unity.Collections.Allocator).Assembly), ManifestRows.NameOf(typeof(Unity.Mathematics.math).Assembly),
                    ManifestRows.NameOf(typeof(Unity.Collections.LowLevel.Unsafe.UnsafeList<>).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                    ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).ToArray();
                var owner = ChoosePublication(ManifestRows.NameOf(job.Assembly), required, editor, candidates, unsafeCode: true);
                manifest.Append("jobsetup-registration-owner\t").Append(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                    .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(entries[ordinal])).Append('\t')
                    .Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
        }

        internal static void AppendViews(StringBuilder manifest, Type[] components, (Type type, bool module)[] trackers,
                                         Type[] views, Type addonContract, bool editor) {
            var candidates = CurrentCandidates();
            var owners = new Dictionary<Type, string>();
            manifest.Append("views-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            void Add(string phase, Type type) {
                if (!owners.TryGetValue(type, out var owner)) {
                    var required = RequiredAssemblies(type).Concat(RequiredAssemblies(addonContract)).Concat(new[] {
                        ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                        ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                    }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                        .Distinct(StringComparer.Ordinal).ToArray();
                    owner = ChoosePublication(ManifestRows.NameOf(type.Assembly), required, editor, candidates);
                    owners.Add(type, owner);
                }
                manifest.Append("views-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(CodeGeneration.SourceGeneratorViewsFragmentFormat.EntryValue(phase, type.AssemblyQualifiedName)))
                    .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
            }
            foreach (var component in components) Add("Component", component);
            foreach (var tracker in trackers) Add(tracker.module ? "ModuleTracker" : "ViewTracker", tracker.type);
            foreach (var view in views) Add("ViewType", view);
        }

        internal static void AppendConfigs(StringBuilder manifest, Type[] masks, Type[] collections, bool editor) {
            var candidates = CurrentCandidates();
            var owners = new Dictionary<Type, string>();
            manifest.Append("config-publication-schema\t0\tdjE=\n");
            var ordinal = 0;
            foreach (var phase in CodeGeneration.SourceGeneratorConfigFragmentFormat.Phases)
                foreach (var component in phase == "Masks" ? masks : collections) {
                    if (!owners.TryGetValue(component, out var owner)) {
                        var required = RequiredAssemblies(component).Concat(component.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .SelectMany(field => RequiredAssemblies(field.FieldType))).Concat(new[] {
                                ManifestRows.NameOf(typeof(Unity.Burst.BurstCompileAttribute).Assembly),
                                ManifestRows.NameOf(typeof(AOT.MonoPInvokeCallbackAttribute).Assembly),
                                ManifestRows.NameOf(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute).Assembly),
                                ManifestRows.NameOf(typeof(UnityEngine.Scripting.PreserveAttribute).Assembly),
                            }).Concat(editor ? new[] { ManifestRows.NameOf(typeof(UnityEditor.InitializeOnLoadMethodAttribute).Assembly) } : Array.Empty<string>())
                            .Distinct(StringComparer.Ordinal).ToArray();
                        owner = ChoosePublication(ManifestRows.NameOf(component.Assembly), required, editor, candidates, unsafeCode: true);
                        owners.Add(component, owner);
                    }
                    manifest.Append("config-registration-owner\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                        .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(
                            CodeGeneration.SourceGeneratorConfigFragmentFormat.EntryValue(phase, component.AssemblyQualifiedName)))
                        .Append('\t').Append(CodeGeneration.SourceGeneratorSystemFragmentFormat.Encode(owner)).Append('\n');
                }
        }
    }
}
