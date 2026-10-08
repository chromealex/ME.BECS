using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using System.Reflection.Emit;
using ME.BECS.Editor.Systems;

namespace ME.BECS.Editor.Jobs {
    
    /// <summary>
    /// Exports jobs early init registration data for generated code.
    /// </summary>
    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {
        /// <summary>
        /// Source initialization kind used by <c>JobsEarlyInitCodeGenerator</c>.
        /// </summary>
        public override string SourceInitializationKind => this.GetType() == typeof(JobsEarlyInitCodeGenerator) ? "jobs" : base.SourceInitializationKind;
        /// <summary>
        /// Whether cache compiled inputs behavior or state is selected.
        /// </summary>
        public override bool CacheCompiledInputs => this.GetType() == typeof(JobsEarlyInitCodeGenerator);

        private System.Collections.Generic.List<(System.Type job, string call)> earlyInitDiagnostics;
        private MethodInfo[] earlyInitMethods;
        private static readonly System.Type[] EarlyInitContracts = {
            typeof(IJobForComponentsBase), typeof(IJobParallelForComponentsBase), typeof(IJobForComponentsBase),
            typeof(IJobParallelForAspectsBase), typeof(IJobForAspectsBase), typeof(IJobForAspectsComponentsBase),
            typeof(IJobParallelForAspectsComponentsBase),
        };
        private readonly SourceGeneratorJobEarlyInit sourceEarlyInit = new SourceGeneratorJobEarlyInit();
        private readonly SourceGeneratorJobSafety sourceSafety = new SourceGeneratorJobSafety();
        private readonly System.Collections.Generic.Dictionary<System.Type, uint> selectedWeights = new System.Collections.Generic.Dictionary<System.Type, uint>();

        private uint SelectWeight(System.Type jobType) {
            if (this.selectedWeights.TryGetValue(jobType, out var weight)) return weight;
            weight = ILJobWeights.Analyze(jobType);
            this.selectedWeights.Add(jobType, weight);
            return weight;
        }

        internal static string CompareEarlyInit(System.Collections.Generic.List<System.Type> jobs, bool editor, out int unavailable) {
            return CompareEarlyInit(jobs, editor, out unavailable, out _);
        }

        private static string CompareEarlyInit(System.Collections.Generic.List<System.Type> jobs, bool editor, out int unavailable,
            out System.Collections.Generic.List<(System.Type job, MethodInfo method)> initialization) {
            unavailable = 0;
            var generator = new JobsEarlyInitCodeGenerator {
                jobTypes = jobs, editorAssembly = editor, asms = EditorUtils.GetAssembliesInfo(),
                earlyInitDiagnostics = new System.Collections.Generic.List<(System.Type job, string call)>(),
            };
            // Diagnostic oracle shares production discovery/phase order only. Export
            // does not call this comparison or perform legacy method selection.
            generator.CollectLegacyInitialization();
            var report = new System.Text.StringBuilder();
            foreach (var generic in new[] { false, true }) {
                var entries = generator.earlyInitDiagnostics.Where(e => e.job.IsGenericType == generic).Distinct().ToArray();
                var generated = 0;
                var fallback = new System.Collections.Generic.List<string>();
                foreach (var entry in entries) {
                    var resolved = SourceGeneratorBridge.ResolveJobEarlyInit(entry.job, entry.call, out var reason);
                    if (resolved != entry.call) ++generated;
                    else fallback.Add(entry.job.FullName + " [" + entry.job.Assembly.GetName().Name + "] — " + reason);
                }
                unavailable += fallback.Count;
                report.AppendLine($"Job EarlyInit ({(generic ? "generic" : "ordinary")}): generated={generated}, unavailable={fallback.Count}, selected calls={entries.Length} (legacy fallback disabled)");
                foreach (var entry in fallback.OrderBy(s => s, System.StringComparer.Ordinal)) report.Append("  ").AppendLine(entry);
            }
            var selectionsCompared = 0;
            var selectionsDifferent = 0;
            foreach (var group in generator.earlyInitDiagnostics.GroupBy(entry => entry.job).OrderBy(group => group.Key.AssemblyQualifiedName, System.StringComparer.Ordinal)) {
                if (!SourceGeneratorBridge.TryGetJobEarlyInitSelection(group.Key, out var sourceCalls, out var reason)) {
                    ++unavailable;
                    report.AppendLine("EarlyInit source selection unavailable: " + group.Key.AssemblyQualifiedName + " — " + reason);
                    continue;
                }
                ++selectionsCompared;
                var legacyCalls = group.Select(entry => entry.call).Distinct(System.StringComparer.Ordinal).OrderBy(call => call, System.StringComparer.Ordinal).ToArray();
                if (sourceCalls.SequenceEqual(legacyCalls, System.StringComparer.Ordinal)) continue;
                ++selectionsDifferent;
                ++unavailable;
                report.AppendLine("EarlyInit SELECTION DIFFERS: " + group.Key.AssemblyQualifiedName);
                foreach (var call in sourceCalls.Except(legacyCalls, System.StringComparer.Ordinal)) report.AppendLine("  source only: " + call);
                foreach (var call in legacyCalls.Except(sourceCalls, System.StringComparer.Ordinal)) report.AppendLine("  legacy only: " + call);
            }
            report.AppendLine($"EarlyInit independent source selections: compared={selectionsCompared}, different={selectionsDifferent} (distinct call sets; registration order NOT checked)");
            generator.CompareEarlyInitOrder(report, ref unavailable, out initialization);
            return report.AppendLine("EarlyInit diagnostics: fresh selection, no cache files read/written; initialization methods NOT invoked.").ToString();
        }

        private void CompareEarlyInitOrder(System.Text.StringBuilder report, ref int unavailable,
            out System.Collections.Generic.List<(System.Type job, MethodInfo method)> initialization) {
            initialization = new System.Collections.Generic.List<(System.Type job, MethodInfo method)>();
            var contracts = EarlyInitContracts;
            var source = new System.Collections.Generic.List<(System.Type job, string call)>();
            var plans = new System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.KeyValuePair<int, string>[]>();
            var incomplete = 0;
            for (var phase = 0; phase < contracts.Length; ++phase) {
                foreach (var job in this.SelectEarlyInitJobs(contracts[phase])) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job)) continue;
                    if (!plans.TryGetValue(job, out var plan)) {
                        if (!SourceGeneratorBridge.TryGetJobEarlyInitPlan(job, out plan, out var reason)) {
                            ++incomplete;
                            report.AppendLine("EarlyInit ordered plan unavailable: " + job.AssemblyQualifiedName + " — " + reason);
                        }
                        plans.Add(job, plan);
                    }
                    if (plan == null) continue;
                    var candidates = plan.Where(entry => entry.Key == phase).ToArray();
                    if (candidates.Length > 1) {
                        ++incomplete;
                        report.AppendLine("EarlyInit ambiguous source phase " + phase + ": " + job.AssemblyQualifiedName);
                        continue;
                    }
                    MethodInfo generated = null;
                    if (candidates.Length == 1) {
                        var call = candidates[0].Value;
                        source.Add((job, call));
                        generated = SourceGeneratorBridge.ResolveJobEarlyInitMethod(job, call, out var reason);
                        if (generated == null) {
                            ++incomplete;
                            report.AppendLine("EarlyInit ordered wrapper unavailable: " + job.AssemblyQualifiedName + " — " + reason);
                            continue;
                        }
                    }
                    // Include stat-only slots in the validated snapshot, not only EarlyInit calls.
                    initialization.Add((job, generated));
                }
            }
            unavailable += incomplete;
            if (incomplete != 0) {
                report.AppendLine("EarlyInit registration order: INCOMPLETE, unavailable/ambiguous=" + incomplete);
                return;
            }
            var equal = source.SequenceEqual(this.earlyInitDiagnostics);
            report.AppendLine($"EarlyInit registration order: {(equal ? "equal" : "DIFFERS")}, source={source.Count}, legacy={this.earlyInitDiagnostics.Count} (duplicates retained; shared discovery order)");
            if (equal) return;
            ++unavailable;
            var differences = 0;
            for (var index = 0; index < System.Math.Max(source.Count, this.earlyInitDiagnostics.Count) && differences < 16; ++index) {
                var actual = index < source.Count ? source[index] : default;
                var expected = index < this.earlyInitDiagnostics.Count ? this.earlyInitDiagnostics[index] : default;
                if (actual.Equals(expected)) continue;
                ++differences;
                report.AppendLine("  index " + index + ": source=" + (actual.call ?? "<end>") + "; legacy=" + (expected.call ?? "<end>"));
            }
        }

        // Production reads compiler-selected wrapper signatures directly. Keep the
        // phase/job slots (including stat-only slots and repeated jobs) unchanged.
        private System.Collections.Generic.List<(System.Type job, MethodInfo method)> SelectSourceEarlyInit() {
            var initialization = new System.Collections.Generic.List<(System.Type job, MethodInfo method)>();
            for (var phase = 0; phase < EarlyInitContracts.Length; ++phase) {
                foreach (var job in this.SelectEarlyInitJobs(EarlyInitContracts[phase])) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job)) continue;
                    if (!this.sourceEarlyInit.TryGetMethods(job, out var methods, out var reason))
                        throw new System.InvalidOperationException("Source EarlyInit selection failed for " + job.AssemblyQualifiedName + ": " + reason);
                    foreach (var entry in methods) {
                        if (!EarlyInitContracts[entry.Key].IsAssignableFrom(job))
                            throw new System.InvalidOperationException("Source EarlyInit phase does not match job contract: " + job.AssemblyQualifiedName);
                    }
                    initialization.Add((job, methods.Where(entry => entry.Key == phase).Select(entry => entry.Value).SingleOrDefault()));
                }
            }
            return initialization;
        }

        // Export asks for the same contract's jobs up to three times (EarlyInit
        // phases, debug/weight loop, preparation); expanding generic jobs is costly.
        private readonly System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<System.Type>> earlyInitJobs =
            new System.Collections.Generic.Dictionary<System.Type, System.Collections.Generic.List<System.Type>>();
        private System.Collections.Generic.List<System.Type> earlyInitJobsSource;

        private System.Collections.Generic.List<System.Type> SelectEarlyInitJobs(System.Type contract) {
            if (!ReferenceEquals(this.earlyInitJobsSource, this.jobTypes)) { this.earlyInitJobs.Clear(); this.earlyInitJobsSource = this.jobTypes; }
            if (this.earlyInitJobs.TryGetValue(contract, out var cached)) return new System.Collections.Generic.List<System.Type>(cached);
            var jobs = this.GetTypesDerivedFrom(contract).OrderBy(type => type.FullName).ToList();
            jobs = ILAnalysisSession.ReadMetadata(() => {
                CodeGenerator.PatchSystemsList(jobs);
                return jobs;
            });
            this.earlyInitJobs[contract] = jobs;
            return new System.Collections.Generic.List<System.Type>(jobs);
        }

        // Populate only the IL memo for exactly the production debug/weight jobs.
        // No registration, compiler input publication or Unity asset access here.
        internal void PrepareAnalysis() => this.PrepareAnalysis(new System.Collections.Generic.HashSet<System.Type>());

        // Safety/count/weight summaries are keyed by the concrete job, not the
        // publication profile. Share this set only within one analysis session.
        internal System.Action<int, int> analysisProgress;
        internal void PrepareAnalysis(System.Collections.Generic.HashSet<System.Type> visited) {
            var selected = new System.Collections.Generic.List<System.Type>();
            foreach (var contract in EarlyInitContracts.Distinct()) {
                foreach (var job in this.SelectEarlyInitJobs(contract)) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job) || !visited.Add(job)) continue;
                    selected.Add(job);
                }
            }
            var completed = 0;
            this.analysisProgress?.Invoke(0, selected.Count);
            foreach (var job in selected) {
                ILAnalysisSession.Checkpoint();
                GetJobTypesInfo(job);
                ILJobEntityCounts.TryGetExportPayload(job, out _, out _);
                ILJobWeights.Analyze(job);
                this.analysisProgress?.Invoke(++completed, selected.Count);
            }
        }

        /// <summary>
        /// Stores type info for <c>JobsEarlyInitCodeGenerator</c>.
        /// </summary>
        public struct TypeInfo : System.IEquatable<TypeInfo> {

            /// <summary>
            /// Type descriptor used by the associated operation.
            /// </summary>
            public System.Type type;
            /// <summary>
            /// Op used by <c>JobsEarlyInitCodeGenerator.TypeInfo</c>.
            /// </summary>
            public RefOp op;
            /// <summary>
            /// Indicates is arg.
            /// </summary>
            public bool isArg;

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public bool Equals(TypeInfo other) {
                return Equals(this.type, other.type) && this.op == other.op;
            }

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public override bool Equals(object obj) {
                return obj is TypeInfo other && this.Equals(other);
            }

            /// <summary>
            /// Returns a hash code consistent with this type's equality comparison.
            /// </summary>
            public override int GetHashCode() {
                return System.HashCode.Combine(this.type, (int)this.op);
            }

            /// <summary>
            /// Formats this value for display or diagnostics.
            /// </summary>
            public override string ToString() {
                return $"{this.type} {this.op} {this.isArg}";
            }

        }

        // Migration oracle only; production consumes the source selection catalog below.
        private void CollectLegacyEarlyInit<TJobBase, T0, T1>(string method) {

            var jobsComponents = this.SelectEarlyInitJobs(typeof(TJobBase));
            foreach (var jobType in jobsComponents) {

                if (jobType.IsValueType == false) continue;
                if (jobType.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(jobType) == false) continue;

                if (jobType.IsGenericType == true && jobType.DeclaringType != null && jobType.DeclaringType.IsGenericType == true) {
                } else if (jobType.IsGenericType == true) {
                    throw new System.Exception($"[ CodeGenerator ] Generic jobs are not supported (job type {jobType.FullName}). Use generic systems instead.");
                }

                var jobTypeFullName = EditorUtils.GetTypeName(jobType);
                var components = new System.Collections.Generic.List<string>();
                var componentsTypes = new System.Collections.Generic.List<System.Type>();
                var jobInterfaces = jobType.GetInterfaces();
                System.Type workInterface = null;
                foreach (var i in jobInterfaces) {
                    // Other generic interfaces on the job are not scheduling contracts.
                    if (i.Assembly != typeof(TJobBase).Assembly || !typeof(TJobBase).IsAssignableFrom(i)) continue;
                    if (i.IsGenericType == true) {
                        foreach (var type in i.GenericTypeArguments) {
                            if (typeof(T0).IsAssignableFrom(type) == true ||
                                typeof(T1).IsAssignableFrom(type) == true) {
                                if (this.IsValidTypeForAssembly(type) == false) continue;
                                components.Add(EditorUtils.GetTypeName(type));
                                componentsTypes.Add(type);
                            }
                        }

                        workInterface = i;
                        break;
                    } else if (typeof(T0) == typeof(TNull) && typeof(T1) == typeof(TNull) && i.Name.EndsWith("Base") == false) {
                        workInterface = i;
                        break;
                    }
                }

                if (workInterface != null && components.Count == workInterface.GenericTypeArguments.Length) {

                    var methods = this.earlyInitMethods ??= typeof(ME.BECS.Jobs.EarlyInit).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    MethodInfo methodInfoResult = null;
                    foreach (var methodInfo in methods) {
                        if (methodInfo.Name.StartsWith(method) == false) continue;
                        if (methodInfo.GetGenericArguments().Length != components.Count + 1) continue;

                        {
                            var types = methodInfo.GetGenericArguments();
                            var check = true;
                            for (int i = 0; i < componentsTypes.Count; ++i) {
                                if (types[i + 1].GetInterfaces()[0].IsAssignableFrom(componentsTypes[i]) == false) {
                                    check = false;
                                    break;
                                }
                            }
                            if (check == false) continue;
                        }

                        methodInfoResult = methodInfo;
                        break;
                    }

                    if (methodInfoResult == null) {
                        throw new System.InvalidOperationException($"EarlyInit selection failed for {jobTypeFullName} ({method}); initialization cannot omit a selected job.");
                    }
                    var str = $"EarlyInit.{methodInfoResult.Name}<{jobTypeFullName}, {string.Join(", ", components)}>();";
                    if (components.Count == 0) str = $"EarlyInit.{methodInfoResult.Name}<{jobTypeFullName}>();";
                    this.earlyInitDiagnostics.Add((jobType, str));

                }

            }
            
        }

        private System.Type[] GetTypesDerivedFrom(System.Type type) {
            var list = new System.Collections.Generic.List<System.Type>(this.jobTypes.Count);
            foreach (var item in this.jobTypes) {
                if (type.IsAssignableFrom(item) == true) {
                    var t = item;
                    if (item.IsGenericType == true && item.IsGenericTypeDefinition == false) {
                        t = item.GetGenericTypeDefinition();
                    }
                    list.Add(t);
                }
            }
            return list.ToArray();
            //return UnityEditor.TypeCache.GetTypesDerivedFrom(type).ToArray();
        }

        private readonly System.Collections.Generic.HashSet<System.Type> debugInputReferences = new System.Collections.Generic.HashSet<System.Type>();

        /// <summary>
        /// Adds the assembly references required by this feature's generated code.
        /// </summary>
        public override void AddSourceGeneratorReferences(System.Collections.Generic.List<System.Type> references) {
            // PrepareActiveInputs serializes the plans before collecting references.
            // Reuse that exact dependency snapshot, including transitive safety types
            // that need not occur in the job's public signature. Do not re-run analysis.
            var selected = new System.Collections.Generic.HashSet<System.Type>(this.debugInputReferences);
            foreach (var contract in EarlyInitContracts.Distinct()) {
                foreach (var job in this.SelectEarlyInitJobs(contract)) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job)) continue;
                    selected.Add(job);
                }
            }
            references.AddRange(selected.OrderBy(type => type.AssemblyQualifiedName, System.StringComparer.Ordinal));
        }
        
        /// <summary>
        /// Returns retired source files.
        /// </summary>
        public override System.Collections.Generic.IEnumerable<string> GetRetiredSourceFiles() =>
            new[] { "Debug.Cache", "Debug.Func", "Debug.Struct", "Debug.UnsafeStruct" };


        internal static System.Type GetDebugWorkInterface(System.Type job, System.Type contract) {
            var candidates = job.GetInterfaces().Where(type => type.IsGenericType && contract.IsAssignableFrom(type) &&
                type.Assembly == typeof(IJobForComponentsBase).Assembly).ToArray();
            if (candidates.Length > 1)
                throw new System.InvalidOperationException("Ambiguous debug job contract " + contract.FullName + " on " + job.FullName);
            return candidates.Length == 1 ? candidates[0] : null;
        }
        
        internal static string GetDebugWrapperName(System.Type job, System.Type contract) => "JobDebugData_" +
            ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(job.AssemblyQualifiedName + "\n" + contract.AssemblyQualifiedName);


        private sealed class DebugWrapperPlan {
            public System.Type job;
            public System.Type contract;
            public System.Type workInterface;
            public System.Type[] components;
            public System.Type[] aspects;
            public TypeInfo[] safety;
            public bool hasTypedArguments;
            public bool sourceSafety;
        }

        /// <summary>
        /// Adds this feature's registration inputs to the source-generator export.
        /// </summary>
        public override void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) {
            var steps = this.AppendSourceGeneratorInputsSteps(manifest);
            while (steps.MoveNext()) { }
        }

        // Yields between independent parts and every 64 jobs; the text is identical.
        /// <summary>
        /// Produces incremental steps for exporting this feature's source-generator inputs.
        /// </summary>
        public override System.Collections.IEnumerator AppendSourceGeneratorInputsSteps(System.Text.StringBuilder manifest) {
            this.debugInputReferences.Clear();
            System.Collections.Generic.List<(System.Type job, MethodInfo method)> initialization;
            using (CodeGeneratorTimings.Measure("JobsEarlyInit: select early init")) initialization = this.SelectSourceEarlyInit();
            yield return null;
            manifest.Append("job-early-init-schema\t0\tdjE=\n");
            for (var index = 0; index < initialization.Count; ++index) {
                var entry = initialization[index];
                var method = entry.method;
                var payload = new System.Text.StringBuilder("v1\n").Append(entry.job.AssemblyQualifiedName).Append('\n')
                    .Append(method?.DeclaringType.AssemblyQualifiedName ?? "").Append('\n').Append(method?.Name ?? "");
                this.debugInputReferences.Add(entry.job);
                if (method != null) {
                    this.debugInputReferences.Add(method.DeclaringType);
                    if (method.IsGenericMethod) foreach (var argument in method.GetGenericArguments()) {
                        this.debugInputReferences.Add(argument);
                        payload.Append('\n').Append(argument.AssemblyQualifiedName);
                    }
                }
                manifest.Append("job-early-init\t").Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                    .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload.ToString()))).Append('\n');
            }
            using (CodeGeneratorTimings.Measure("JobsEarlyInit: job init owners")) SourceGeneratorRegistrationOwners.AppendJobInit(manifest, this.editorAssembly);
            yield return null;
            manifest.Append("job-debug-schema\t0\tdjE=\n");
            var contracts = new[] { typeof(IJobParallelForComponentsBase), typeof(IJobForComponentsBase),
                typeof(IJobParallelForAspectsBase), typeof(IJobForAspectsBase),
                typeof(IJobForAspectsComponentsBase), typeof(IJobParallelForAspectsComponentsBase) };
            var ordinal = 0;
            var weightJobs = new System.Collections.Generic.HashSet<System.Type>();
            var entityFallbackOrdinal = 0;
            var entityILOrdinal = 0;
            var yieldCounter = 0;
            foreach (var contract in contracts) {
                var componentsOnly = contract == typeof(IJobParallelForComponentsBase) || contract == typeof(IJobForComponentsBase);
                var aspectsOnly = contract == typeof(IJobParallelForAspectsBase) || contract == typeof(IJobForAspectsBase);
                foreach (var job in this.SelectEarlyInitJobs(contract).Distinct()) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job)) continue;
                    CodeGeneratorTimings.Subject("Safety: " + job.FullName);
                    if (++yieldCounter % 64 == 0) yield return null;
                    DebugWrapperPlan plan;
                    using (CodeGeneratorTimings.Measure("JobsEarlyInit: safety plan")) plan = this.CreateDebugWrapperPlan(job, contract, aspectsOnly ? null : typeof(IComponentBase),
                        componentsOnly ? null : typeof(IAspect));
                    this.debugInputReferences.Add(plan.job);
                    this.debugInputReferences.Add(plan.contract);
                    if (plan.workInterface != null) this.debugInputReferences.Add(plan.workInterface);
                    foreach (var component in plan.components) this.debugInputReferences.Add(component);
                    foreach (var aspect in plan.aspects) this.debugInputReferences.Add(aspect);
                    foreach (var dependency in plan.safety) this.debugInputReferences.Add(dependency.type);
                    var payload = new System.Text.StringBuilder("v2\n").Append(job.AssemblyQualifiedName).Append('\n')
                        .Append(contract.AssemblyQualifiedName).Append('\n').Append(plan.workInterface?.AssemblyQualifiedName ?? "")
                        .Append('\n').Append(plan.hasTypedArguments ? "1" : "0");
                    foreach (var component in plan.components) payload.Append("\nC\t").Append(component.AssemblyQualifiedName);
                    foreach (var aspect in plan.aspects) payload.Append("\nA\t").Append(aspect.AssemblyQualifiedName);
                    payload.Append("\nS\til");
                    // An explicit IL snapshot is authoritative. Roslyn validates
                    // the types/modes but must not substitute source summaries.
                    foreach (var dependency in plan.safety)
                        payload.Append("\nS\t").Append(dependency.op).Append('\t').Append(dependency.type.AssemblyQualifiedName);
                    manifest.Append("job-debug\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                        .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload.ToString()))).Append('\n');
                    if (weightJobs.Add(job)) {
                        CodeGeneratorTimings.Subject("Entity counts: " + job.FullName);
                        string entityPayload; bool covered;
                        using (CodeGeneratorTimings.Measure("JobsEarlyInit: entity counts")) entityPayload = this.GetEntityInputPayload(job, out covered);
                        var entityOrdinal = covered ? entityILOrdinal++ : entityFallbackOrdinal++;
                        manifest.Append(covered ? "job-entity-il\t" : "job-entity-fallback\t")
                            .Append(entityOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture))
                            .Append('\t').Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entityPayload))).Append('\n');
                        CodeGeneratorTimings.Subject("Job weight: " + job.FullName);
                        manifest.Append("job-weight\t").Append((weightJobs.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                            .Append('\t').Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(job.AssemblyQualifiedName)))
                            .Append("\til\t").Append(MeasuredWeight(job).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                    }
                }
            }
        }

        private uint MeasuredWeight(System.Type job) {
            using (CodeGeneratorTimings.Measure("JobsEarlyInit: weights")) return this.SelectWeight(job);
        }

        private string GetEntityInputPayload(System.Type job, out bool covered) {
            // Compiled IL owns the production reservation for ordinary and closed
            // generic jobs alike. Source catalogs remain diagnostic-only, even if
            // their metadata is complete, stale, duplicated or unavailable.
            covered = ILJobEntityCounts.TryGetExportPayload(job, out var payload, out _);
            return covered ? payload : this.GetEntityFallbackPayload(job);
        }

        private string GetEntityFallbackPayload(System.Type job) {
            var info = GetJobEntInfo(job, this);
            if (info.brCount < 0) throw new System.InvalidOperationException("Negative entity loop count: " + job.FullName);
            var maximum = job.GetCustomAttribute<EntitiesJobMaxCountAttribute>()?.count ?? 0u;
            var reservations = info.count?.Select(count => checked((uint)count)).ToArray();
            if (maximum > 0u && info.brCount > 0) {
                reservations ??= new uint[info.loopGroups.Length];
                for (var index = 0; index < reservations.Length; ++index)
                    if (info.loopGroups[index]) reservations[index] = maximum;
            }
            var format = System.Globalization.CultureInfo.InvariantCulture;
            // Preserve this explicitly selected compatibility IL result. The
            // compiler must not substitute a diagnostic source catalog.
            var payload = new System.Text.StringBuilder("v1\n").Append(job.AssemblyQualifiedName).Append('\n')
                .Append(maximum.ToString(format)).Append('\n').Append(info.brCount.ToString(format)).Append('\n')
                .Append(reservations == null ? "0" : "1");
            if (reservations != null) {
                var groups = EntityTypeCodeGenerator.GetAllTypes(this, out var groupCount);
                if (reservations.Length != groupCount)
                    throw new System.InvalidOperationException("Entity reservation groups changed during export: " + job.FullName);
                for (var index = 0; index < reservations.Length; ++index) {
                    if (reservations[index] == 0u) continue;
                    var entity = groups[index].Item1;
                    payload.Append('\n').Append(entity.Assembly.FullName).Append("\tT:").Append(entity.FullName.Replace('+', '.'))
                        .Append('\t').Append(reservations[index].ToString(format));
                }
            }
            return payload.ToString();
        }

        private DebugWrapperPlan CreateDebugWrapperPlan(System.Type job, System.Type contract,
            System.Type componentContract, System.Type aspectContract) {
            var workInterface = GetDebugWorkInterface(job, contract);
            var arguments = workInterface?.GenericTypeArguments ?? System.Type.EmptyTypes;
            var components = arguments.Where(type => componentContract != null && componentContract.IsAssignableFrom(type) && this.IsValidTypeForAssembly(type)).ToArray();
            var aspects = arguments.Where(type => aspectContract != null && aspectContract.IsAssignableFrom(type) && this.IsValidTypeForAssembly(type)).ToArray();
            var safety = this.sourceSafety.SelectForExport(job, out var sourceSafety);
            UpdateDeps(safety);
            return new DebugWrapperPlan {
                job = job, contract = contract, workInterface = workInterface,
                components = components, aspects = aspects,
                sourceSafety = sourceSafety,
                hasTypedArguments = workInterface != null && components.Length + aspects.Length == arguments.Length,
                safety = safety.OrderBy(item => item.type.FullName, System.StringComparer.Ordinal)
                    .ThenBy(item => item.type.Assembly.FullName, System.StringComparer.Ordinal).ToArray(),
            };
        }


        /// <summary>
        /// Returns method types info.
        /// </summary>
        public static System.Collections.Generic.HashSet<TypeInfo> GetMethodTypesInfo(MethodInfo root, bool traverseHierarchy = true, bool useAnalyzer = false, bool methodParameters = true, System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction = null) {
            return GetBodyTypesInfo(root, traverseHierarchy, useAnalyzer, methodParameters, onInstruction);
        }

        internal static System.Collections.Generic.HashSet<TypeInfo> GetBodyTypesInfo(MethodBase root, bool traverseHierarchy = true, bool useAnalyzer = false, bool methodParameters = true, System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction = null) {
            // Discovery callbacks mutate their own inventories and must run on
            // every visit. Pure safety requests can share a defensive snapshot.
            if (onInstruction != null) return GetBodyTypesInfoCore(root, traverseHierarchy, useAnalyzer, methodParameters, onInstruction);
            var cached = ILAnalysisSession.Get((typeof(TypeInfo), root, traverseHierarchy, useAnalyzer, methodParameters),
                () => useAnalyzer || !ILPersistentAnalysis.Active ? GetBodyTypesInfoCore(root, traverseHierarchy, useAnalyzer, methodParameters, null) :
                    ILPersistentAnalysis.Get("safety", ILPersistentAnalysis.MethodIdentity(root) is var identity && identity != null
                            ? identity + "\n" + traverseHierarchy + "\n" + methodParameters : null,
                        () => GetBodyTypesInfoCore(root, traverseHierarchy, false, methodParameters, null), ILSummaryData.Encode, ILSummaryData.Decode));
            return new System.Collections.Generic.HashSet<TypeInfo>(cached);
        }

        internal static System.Collections.Generic.HashSet<TypeInfo> GetDiscoveryTypesInfo(MethodInfo root,
            System.Collections.Generic.HashSet<MethodBase> scannedBodies,
            System.Func<Instruction, System.Collections.Generic.Queue<MethodInfo>, bool> onInstruction) =>
            GetBodyTypesInfoCore(root, true, false, false, onInstruction, scannedBodies);

        internal static System.Collections.Generic.HashSet<TypeInfo> GetDiscoveryBodyInfo(MethodBase root,
            System.Collections.Generic.HashSet<MethodBase> nextBodies,
            System.Func<Instruction, System.Collections.Generic.Queue<MethodInfo>, bool> onInstruction) =>
            GetBodyTypesInfoCore(root, true, false, false, onInstruction, nextBodies: nextBodies);

        private static RefOp? ParameterAccess(MethodBase method, ParameterInfo parameter) {
            return ILAnalysisSession.Get((typeof(ParameterInfo), method, parameter.Position, "component-access"),
                () => {
                    var work = 0;
                    return ParameterAccessCore(method, parameter, new System.Collections.Generic.HashSet<MethodBase>(), ref work);
                });
        }

        private static RefOp? ParameterAccessCore(MethodBase method, ParameterInfo parameter,
            System.Collections.Generic.HashSet<MethodBase> visiting, ref int work) {
            ILAnalysisSession.Checkpoint(method);
            if (method.GetMethodBody() == null || visiting.Count >= 64 || !visiting.Add(method)) return RefOp.ReadWrite;
            try {
                var expected = parameter.Position + (method.IsStatic ? 0 : 1);
                var instructions = ILAnalysisSession.Instructions(method);
                var aliases = new System.Collections.Generic.HashSet<int>();
                int Local(Instruction instruction, string operation) {
                    var opcode = instruction.OpCode.Name;
                    if (opcode == operation || opcode == operation + ".s")
                        return instruction.Operand is LocalVariableInfo local ? local.LocalIndex : System.Convert.ToInt32(instruction.Operand);
                    return opcode.StartsWith(operation + ".", System.StringComparison.Ordinal) &&
                        int.TryParse(opcode.Substring(operation.Length + 1), out var index) ? index : -1;
                }
                bool Loads(Instruction instruction) {
                    if (aliases.Contains(Local(instruction, "ldloc")) || aliases.Contains(Local(instruction, "ldloca"))) return true;
                    var opcode = instruction.OpCode.Name;
                    if (opcode == "ldarg." + expected) return true;
                    if (opcode != "ldarg" && opcode != "ldarg.s" && opcode != "ldarga" && opcode != "ldarga.s") return false;
                    var index = instruction.Operand is ParameterInfo argument
                        ? argument.Position + (method.IsStatic ? 0 : 1) : System.Convert.ToInt32(instruction.Operand);
                    return index == expected;
                }
                Instruction Consumer(Instruction instruction) {
                    var nextInstruction = instruction.Next;
                    while (nextInstruction != null && (nextInstruction.OpCode.Name == "nop" || nextInstruction.OpCode.Name == "ldflda"))
                        nextInstruction = nextInstruction.Next;
                    return nextInstruction;
                }
                // MAY aliases across all paths: reassignment never erases a possible
                // component reference. Iterate to handle backedges and local-to-local copies.
                bool changed;
                do {
                    ILAnalysisSession.Checkpoint(method);
                    changed = false;
                    foreach (var instruction in instructions) {
                        if (++work > 65536) return RefOp.ReadWrite;
                        if (!Loads(instruction)) continue;
                        var consumer = Consumer(instruction);
                        if (consumer == null) continue;
                        var local = Local(consumer, "stloc");
                        if (local >= 0) changed |= aliases.Add(local);
                    }
                } while (changed);
                RefOp? access = null;
                void Merge(RefOp value) => access = !access.HasValue || access.Value == value ? value : RefOp.ReadWrite;
                foreach (var instruction in instructions) {
                    if (++work > 65536) return RefOp.ReadWrite;
                    var name = instruction.OpCode.Name;
                    if (!Loads(instruction)) continue;
                    if (name.StartsWith("ldloca", System.StringComparison.Ordinal) || name.StartsWith("ldarga", System.StringComparison.Ordinal)) return RefOp.ReadWrite;
                    // These instructions consume the reference and push a value copy;
                    // subsequent mutations of that copy do not write the component.
                    var consumer = Consumer(instruction);
                    // Taking a nested field address is not a write. Follow only a
                    // contiguous address chain; aliases/branches retain conservative handling.
                    var next = consumer?.OpCode.Name;
                    if (consumer != null && Local(consumer, "stloc") >= 0) continue;
                    if (!name.StartsWith("ldarga", System.StringComparison.Ordinal) &&
                        (next == "ldfld" || next == "ldobj" || next?.StartsWith("ldind.", System.StringComparison.Ordinal) == true)) {
                        Merge(RefOp.ReadOnly);
                        continue;
                    }
                    if (!name.StartsWith("ldarga", System.StringComparison.Ordinal) && next == "initobj") {
                        Merge(RefOp.WriteOnly);
                        continue;
                    }
                    if (TryParameterCall(consumer, ref work, out var helper, out var argument, out var directWrite)) {
                        if (directWrite) { Merge(RefOp.WriteOnly); continue; }
                        if (consumer != instruction.Next) return RefOp.ReadWrite;
                        if (argument.ParameterType == parameter.ParameterType) {
                            var nested = ParameterAccessCore(helper, argument, visiting, ref work);
                            if (nested == RefOp.ReadWrite) return RefOp.ReadWrite;
                            if (nested.HasValue) Merge(nested.Value);
                            continue;
                        }
                    }
                    return parameter.IsIn && !parameter.IsOut ? RefOp.ReadOnly : RefOp.ReadWrite;
                }
                return access;
            } finally { visiting.Remove(method); }
        }

        private static bool TryParameterCall(Instruction instruction, ref int work, out MethodInfo helper, out ParameterInfo parameter, out bool directWrite) {
            helper = null;
            parameter = null;
            directWrite = false;
            var above = 0;
            for (var current = instruction; current != null; current = current.Next) {
                if (++work > 65536) return false;
                // The value lies above the tracked destination address. The store
                // consumes both without reading the previous component value.
                var opcode = current.OpCode.Name;
                if (above == 1 && (opcode == "stfld" || opcode == "stobj" || opcode.StartsWith("stind.", System.StringComparison.Ordinal))) {
                    directWrite = true;
                    return true;
                }
                if (current.OpCode.Name == "call" && current.Operand is MethodInfo method) {
                    var arguments = method.GetParameters();
                    var popped = arguments.Length + (method.IsStatic ? 0 : 1);
                    if (popped > above) {
                        var index = arguments.Length - 1 - above;
                        if (index < 0 || method.ReturnType.IsByRef || method.ReturnType.IsPointer) return false;
                        helper = method;
                        parameter = arguments[index];
                        return true;
                    }
                    above += (method.ReturnType == typeof(void) ? 0 : 1) - popped;
                    continue;
                }
                if (current.OpCode.FlowControl != System.Reflection.Emit.FlowControl.Next &&
                    current.OpCode.FlowControl != System.Reflection.Emit.FlowControl.Meta) return false;
                var count = FixedStackCount(current.OpCode.StackBehaviourPop);
                var pushed = FixedStackCount(current.OpCode.StackBehaviourPush);
                if (count < 0 || pushed < 0 || count > above) return false;
                above += pushed - count;
            }
            return false;
        }

        private static int FixedStackCount(StackBehaviour behavior) {
            switch (behavior) {
                case StackBehaviour.Pop0: case StackBehaviour.Push0: return 0;
                case StackBehaviour.Pop1: case StackBehaviour.Popi: case StackBehaviour.Popref:
                case StackBehaviour.Push1: case StackBehaviour.Pushi: case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4: case StackBehaviour.Pushr8: case StackBehaviour.Pushref: return 1;
                case StackBehaviour.Pop1_pop1: case StackBehaviour.Popi_pop1: case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8: case StackBehaviour.Popi_popr4: case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1: case StackBehaviour.Popref_popi: case StackBehaviour.Push1_push1: return 2;
                case StackBehaviour.Popi_popi_popi: case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi: case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4: case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref: return 3;
                default: return -1; // Variable/unknown stack effects require a conservative result.
            }
        }

        private static System.Collections.Generic.HashSet<TypeInfo> GetBodyTypesInfoCore(MethodBase root, bool traverseHierarchy, bool useAnalyzer, bool methodParameters,
            System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction,
            System.Collections.Generic.HashSet<MethodBase> scannedBodies = null,
            System.Collections.Generic.HashSet<MethodBase> nextBodies = null) {
            var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
            var componentsType = new System.Collections.Generic.HashSet<System.Type>();

            var parametersOverrides = new System.Collections.Generic.HashSet<TypeInfo>();
            var parameterAccesses = new System.Collections.Generic.HashSet<TypeInfo>();
            if (methodParameters == true) {
                var parameters = root.GetParameters();
                foreach (var p in parameters) {
                    var parameterType = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                    var overrideWO = p.GetCustomAttribute<WOAttribute>() != null;
                    var overrideRO = p.GetCustomAttribute<ROAttribute>() != null;
                    var overrideRW = p.GetCustomAttribute<RWAttribute>() != null;
                    if (overrideRW == true) {
                        parametersOverrides.Add(new TypeInfo() {
                            type = p.ParameterType.GetElementType(),
                            op = RefOp.ReadWrite,
                            isArg = true,
                        });
                    } else if (overrideWO == true) {
                        parametersOverrides.Add(new TypeInfo() {
                            type = p.ParameterType.GetElementType(),
                            op = RefOp.WriteOnly,
                            isArg = true,
                        });
                    } else if (overrideRO == true) {
                        parametersOverrides.Add(new TypeInfo() {
                            type = p.ParameterType.GetElementType(),
                            op = RefOp.ReadOnly,
                            isArg = true,
                        });
                    } else if (p.ParameterType.IsByRef && typeof(IComponentBase).IsAssignableFrom(parameterType) &&
                               ParameterAccess(root, p) is RefOp parameterAccess) {
                        parameterAccesses.Add(new TypeInfo() {
                            type = parameterType,
                            op = parameterAccess,
                            isArg = true,
                        });
                    }

                    if (typeof(IAspect).IsAssignableFrom(parameterType)) {
                        aspectsType.Add(parameterType);
                    } else if (typeof(IComponentBase).IsAssignableFrom(parameterType)) {
                        componentsType.Add(parameterType);
                    }
                }
            }

            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            var constructors = new System.Collections.Generic.Queue<ConstructorInfo>();
            var visitedConstructors = new System.Collections.Generic.HashSet<ConstructorInfo>();
            if (root is MethodInfo methodRoot) q.Enqueue(methodRoot);
            else if (root is ConstructorInfo constructorRoot) { constructors.Enqueue(constructorRoot); visitedConstructors.Add(constructorRoot); }
            else throw new System.InvalidOperationException("Unsupported safety IL root: " + root);
            var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
            var visited = new System.Collections.Generic.HashSet<MethodPointerData>(MethodPointerData.ExactComparer);
            if (root is MethodInfo visitedRoot) visited.Add(new MethodPointerData(visitedRoot));
            var scanned = scannedBodies ?? new System.Collections.Generic.HashSet<MethodBase>();
            var scannedInRoot = 0;
            var initializers = new System.Collections.Generic.HashSet<System.Type>();
            bool IgnoreBody(MethodBase member) => member.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) ||
                member.IsDefined(typeof(DisableContainerSafetyRestrictionAttribute), false) || ILInfrastructure.SkipBody(member);
            void EnqueueConstructor(ConstructorInfo constructor) {
                if (constructor != null && !IgnoreBody(constructor) && visitedConstructors.Add(constructor) && constructor.GetMethodBody() != null)
                    constructors.Enqueue(constructor);
            }
            void Initialize(System.Type type) {
                // Reading metadata never executes a .cctor. A closed type's
                // initializer is a possible effect, collected once per analysis.
                if (traverseHierarchy && type != null && !type.ContainsGenericParameters && initializers.Add(type))
                    EnqueueConstructor(type.TypeInitializer);
            }
            void InitializeCall(MethodBase member) {
                if (member.IsStatic || member is ConstructorInfo || member.DeclaringType?.IsValueType == true)
                    Initialize(member.DeclaringType);
            }
            if (!IgnoreBody(root)) InitializeCall(root);
            var instructionCount = 0;
            while (q.Count > 0 || constructors.Count > 0) {
                MethodBase body = q.Count > 0 ? q.Dequeue() : (MethodBase)constructors.Dequeue();
                if (nextBodies != null && !body.Equals(root)) { nextBodies.Add(body); continue; }
                if (ILInfrastructure.SkipBody(body)) continue;
                if (!scanned.Add(body)) continue;
                CodeGeneratorTimings.Work(body);
                if (++scannedInRoot > 10000) throw new System.InvalidOperationException("Job safety IL traversal exceeded 10000 method instances: " + root.DeclaringType?.FullName + "." + root.Name);
                var deps = useAnalyzer && body is MethodInfo analyzedMethod ? ILAnalyzer.AnalyzeMethod(analyzedMethod) : null;
                if (deps != null) {
                    foreach (var item in deps) {
                        uniqueTypes.Add(new TypeInfo() {
                            type = item.type,
                            op = item.access,
                            isArg = componentsType.Contains(item.type),
                        });
                    }
                }

                var instructions = ILAnalysisSession.Instructions(body);
                instructionCount = checked(instructionCount + instructions.Length);
                if (instructionCount > 1000000) throw new System.InvalidOperationException("Job safety IL instruction limit exceeded: " + root);
                if (traverseHierarchy) {
                    foreach (var callback in ILFormattingCallbacks.Collect(body, instructions, out _)) {
                        if (callback.IsDefined(typeof(CodeGeneratorIgnoreAttribute), false) ||
                            callback.IsDefined(typeof(DisableContainerSafetyRestrictionAttribute), false)) continue;
                        InitializeCall(callback);
                        if (visited.Add(new MethodPointerData(callback, body.DeclaringType))) q.Enqueue(callback);
                    }
                }
                for (var index = 0; index < instructions.Length; ++index) {
                    var inst = instructions[index];
                    var continueTraverse = true;
                    if (onInstruction?.Invoke(inst, q) == true) continue;
                    {
                        if (inst.Operand is MethodInfo methodInfo && methodInfo.GetCustomAttribute<DisableContainerSafetyRestrictionAttribute>() != null) {
                            continue;
                        }
                    }
                    {
                        if (inst.Operand is FieldInfo fieldInfo && fieldInfo.GetCustomAttribute<DisableContainerSafetyRestrictionAttribute>() != null) {
                            continue;
                        }
                    }
                    var target = ILCallTargets.Resolve(instructions, index);
                    if (target != null)
                        foreach (var access in ILNativeComponentAccess.Collect(target)) uniqueTypes.Add(access);
                    if (ILCallTargets.TryBurstComponentReference(target, out var storageComponent)) {
                        uniqueTypes.Add(new TypeInfo { type = storageComponent, op = RefOp.ReadWrite });
                        continue;
                    }
                    if (traverseHierarchy) {
                        if (inst.Operand is FieldInfo staticField && staticField.IsStatic &&
                            (inst.OpCode == System.Reflection.Emit.OpCodes.Ldsfld || inst.OpCode == System.Reflection.Emit.OpCodes.Ldsflda ||
                             inst.OpCode == System.Reflection.Emit.OpCodes.Stsfld)) Initialize(staticField.DeclaringType);
                        var called = inst.OpCode == System.Reflection.Emit.OpCodes.Call || inst.OpCode == System.Reflection.Emit.OpCodes.Callvirt ||
                            inst.OpCode == System.Reflection.Emit.OpCodes.Newobj || inst.OpCode == System.Reflection.Emit.OpCodes.Jmp;
                        // CodeGeneratorIgnore suppresses implementation traversal,
                        // not the explicit SafetyCheck contract on that method.
                        if (called && target != null && !IgnoreBody(target)) {
                            // new T() is emitted as Activator.CreateInstance<T>().
                            // Bind that exact constructor, not Activator's reflection
                            // implementation or all constructors of its type argument.
                            if (ILCallTargets.TryConstruction(target, out var constructed, out var constructor)) {
                                Initialize(constructed);
                                EnqueueConstructor(constructor);
                                continue;
                            }
                            InitializeCall(target);
                            if (target is ConstructorInfo directConstructor) EnqueueConstructor(directConstructor);
                        }
                    }
                    {
                        if (inst.Operand is System.Reflection.FieldInfo field && typeof(IRefOp).IsAssignableFrom(field.FieldType) == true) {
                            var op = ILAnalysisSession.Get((typeof(IRefOp), field.FieldType), () =>
                                ILAnalysisSession.ReadMetadata(() => ((IRefOp)System.Activator.CreateInstance(field.FieldType)).Op));
                            uniqueTypes.Add(new TypeInfo() {
                                type = field.FieldType.GenericTypeArguments[0],
                                op = op,
                            });
                            continueTraverse = false;
                        }
                    }
                    /*{
                        if (inst.OpCode == System.Reflection.Emit.OpCodes.Initobj && typeof(IComponentBase).IsAssignableFrom((System.Type)inst.Operand) == true) {
                            uniqueTypes.Add(new TypeInfo() {
                                type = (System.Type)inst.Operand,
                                op = RefOp.WriteOnly,
                                isArg = componentsType.Contains((System.Type)inst.Operand),
                            });
                        }
                    }
                    {
                        if (inst.Operand is FieldInfo field && typeof(IComponentBase).IsAssignableFrom(field.DeclaringType) == true) {
                            uniqueTypes.Add(new TypeInfo() {
                                type = field.DeclaringType,
                                op = (componentsType.Contains(field.DeclaringType) == true || aspectsType.Contains(field.DeclaringType) == true) && (inst.OpCode == System.Reflection.Emit.OpCodes.Stfld || inst.OpCode == System.Reflection.Emit.OpCodes.Stobj || inst.OpCode == System.Reflection.Emit.OpCodes.Ldflda) ? RefOp.WriteOnly : RefOp.ReadOnly,
                                isArg = componentsType.Contains(field.DeclaringType),
                            });
                            continueTraverse = false;
                        }
                    }*/
                    if (inst.Operand is System.Reflection.MethodInfo method && method.IsGenericMethod == true) {
                        var safetyCheck = method.GetCustomAttribute<SafetyCheckAttribute>();
                        if (safetyCheck != null) {
                            var type = method.GetGenericArguments()[0];
                            if (typeof(IComponentBase).IsAssignableFrom(type) == true) {
                                if (type.IsGenericTypeParameter == true) {
                                    var constraints = type.GetGenericParameterConstraints();
                                    foreach (var constraint in constraints) {
                                        if (constraint == typeof(System.ValueType)) continue;
                                        var constTypes = ILAnalysisSession.ReadMetadata(() => UnityEditor.TypeCache.GetTypesDerivedFrom(constraint).ToArray());
                                        foreach (var constType in constTypes) {
                                            uniqueTypes.Add(new TypeInfo() {
                                                type = constType,
                                                op = safetyCheck.Op,
                                            });
                                        }
                                    }
                                } else {
                                    uniqueTypes.Add(new TypeInfo() {
                                        type = type,
                                        op = safetyCheck.Op,
                                    });
                                }
                                continueTraverse = false;
                            }
                        }
                    }

                    if (continueTraverse == true && traverseHierarchy == true && target is MethodInfo member && !ILInfrastructure.SkipBody(member)) {
                        if (member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null && (member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(new MethodPointerData(member, body.DeclaringType)) == true)) {
                            if (member.GetMethodBody() != null) q.Enqueue(member);
                        }
                    }
                }
            }

            foreach (var access in parameterAccesses) {
                var merged = access;
                foreach (var previous in uniqueTypes.Where(item => item.type == access.type))
                    if (previous.op != merged.op) merged.op = RefOp.ReadWrite;
                uniqueTypes.Remove(new TypeInfo { type = access.type, op = RefOp.ReadOnly });
                uniqueTypes.Remove(new TypeInfo { type = access.type, op = RefOp.WriteOnly });
                uniqueTypes.Remove(new TypeInfo { type = access.type, op = RefOp.ReadWrite });
                uniqueTypes.Add(merged);
            }
            foreach (var p in parametersOverrides) {
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.ReadOnly, });
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.WriteOnly, });
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.ReadWrite, });
                uniqueTypes.Add(p);
            }
            
            return uniqueTypes;
        }
        
        /// <summary>
        /// Stores new ent info for <c>JobsEarlyInitCodeGenerator</c>.
        /// </summary>
        public struct NewEntInfo {

            /// <summary>
            /// Number of entries tracked by this value.
            /// </summary>
            public int[] count;
            /// <summary>
            /// Br count for the associated storage.
            /// </summary>
            public int brCount;
            /// <summary>
            /// Loop groups used by <c>JobsEarlyInitCodeGenerator.NewEntInfo</c>.
            /// </summary>
            public bool[] loopGroups;

        }
        
        // Compatibility projection only. The compiler keeps its legacy origin
        // until unresolved dispatch/type initialization are independently covered.
        /// <summary>
        /// Returns job ent info.
        /// </summary>
        public static NewEntInfo GetJobEntInfo(System.Type jobType, CustomCodeGenerator codeGenerator) {
            var allTypes = EntityTypeCodeGenerator.GetAllTypes(codeGenerator, out var groupsCount)
                .ToDictionary(entry => entry.Item1, entry => entry.Item2);
            var known = ILJobEntityCounts.AnalyzeKnown(jobType, out _);
            var result = new NewEntInfo {
                count = new int[groupsCount],
                loopGroups = new bool[groupsCount],
            };
            var anyInline = false;
            foreach (var entry in known) {
                if (!allTypes.TryGetValue(entry.Key, out var group))
                    throw new System.InvalidOperationException("Entity reservation group is not registered: " + entry.Key + " in " + jobType);
                result.count[group] = checked((int)entry.Value.inline);
                result.loopGroups[group] = entry.Value.loop > 0u;
                result.brCount = checked(result.brCount + checked((int)entry.Value.loop));
                anyInline |= entry.Value.inline > 0u;
            }
            if (!anyInline) result.count = null;
            return result;
        }

        /// <summary>
        /// Stores weights info for <c>JobsEarlyInitCodeGenerator</c>.
        /// </summary>
        public struct WeightsInfo {

            /// <summary>
            /// Weight used by <c>JobsEarlyInitCodeGenerator.WeightsInfo</c>.
            /// </summary>
            public uint weight;

        }

        private struct MethodWeightInfo {

            public MethodInfo[] methods;
            public uint weight;

        }
        
        /// <summary>
        /// Returns job weights info.
        /// </summary>
        public static WeightsInfo GetJobWeightsInfo(System.Type jobType, System.Collections.Generic.Dictionary<string, uint> contributions = null) {
            contributions?.Clear();
            var config = new System.Collections.Generic.List<MethodWeightInfo>();
            config.Add(new MethodWeightInfo() {
                methods = new [] { typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) },
                weight = 10u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(EntExt).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(EntExt.Read) || x.Name == nameof(EntExt.Has) || x.Name == nameof(EntExt.TryRead)).ToArray(),
                weight = 1u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(EntExt).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(EntExt.Get) || x.Name == nameof(EntExt.Set) || x.Name == nameof(EntExt.Remove) || x.Name == nameof(EntExt.SetTag)).ToArray(),
                weight = 2u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(EntityConfigEntExt).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(EntityConfigEntExt.ReadStatic) || x.Name == nameof(EntityConfigEntExt.HasStatic) || x.Name == nameof(EntityConfigEntExt.TryReadStatic)).ToArray(),
                weight = 4u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(UnsafeEntityConfig).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(UnsafeEntityConfig.ReadStatic)).ToArray(),
                weight = 3u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(Components).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(Components.GetUnknownType) || x.Name == nameof(Components.RemoveUnknownType)).ToArray(),
                weight = 2u,
            });
            config.Add(new MethodWeightInfo() {
                methods = typeof(Components).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.Name == nameof(Components.ReadUnknownType) || x.Name == nameof(Components.HasUnknownType)).ToArray(),
                weight = 1u,
            });
            var visited = new System.Collections.Generic.HashSet<MethodPointerData>(MethodPointerData.ExactComparer);
            var instructions = GetJobExecuteMethods(jobType).SelectMany(root => root.GetInstructions()).ToList();
            var expandedBodies = 0;
            for (int i = 0; i < instructions.Count; ++i) {
                if (instructions.Count > 1000000) throw new System.InvalidOperationException("Legacy job weight instruction limit exceeded: " + jobType);
                var inst = instructions[i];
                if (inst.Operand is System.Reflection.MethodInfo member) {
                    if (member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() != null) continue;
                    if (member.DeclaringType.IsInterface) {
                        // Use the actual constrained receiver from IL, not the first generic
                        // parameter of the root job (helpers can use another specialization).
                        System.Type receiver = null;
                        for (var prefix = i - 1; prefix >= 0 && instructions[prefix].OpCode.OpCodeType == System.Reflection.Emit.OpCodeType.Prefix; --prefix) {
                            if (instructions[prefix].OpCode == System.Reflection.Emit.OpCodes.Constrained) {
                                receiver = instructions[prefix].Operand as System.Type;
                                break;
                            }
                        }
                        if (receiver != null && receiver.IsValueType && !receiver.ContainsGenericParameters && member.DeclaringType.IsAssignableFrom(receiver)) {
                            var map = receiver.GetInterfaceMap(member.DeclaringType);
                            var definition = member.IsGenericMethod ? member.GetGenericMethodDefinition() : member;
                            for (var index = 0; index < map.InterfaceMethods.Length; ++index) {
                                if (!map.InterfaceMethods[index].Equals(definition)) continue;
                                var target = map.TargetMethods[index];
                                if (member.IsGenericMethod && !member.IsGenericMethodDefinition)
                                    target = target.MakeGenericMethod(member.GetGenericArguments());
                                member = target;
                                break;
                            }
                        }
                    }
                    if ((member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(new MethodPointerData(member)) == true) && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                        if (member.GetMethodBody() != null) {
                            if (++expandedBodies > 10000) throw new System.InvalidOperationException("Legacy job weight traversal limit exceeded: " + jobType);
                            instructions.InsertRange(i + 1, member.GetInstructions());
                        }
                    }
                }
            }

            var weight = (uint)jobType.GetInterfaces().Sum(x => x.GenericTypeArguments.Length);
            for (int i = 0; i < instructions.Count; ++i) {
                var inst = instructions[i];
                if (inst.Operand is MethodInfo methodInfo) {
                    if (methodInfo.IsGenericMethod == true) {
                        methodInfo = methodInfo.GetGenericMethodDefinition();
                    }
                    foreach (var item in config) {
                        if (System.Array.IndexOf(item.methods, methodInfo) >= 0) {
                            weight += item.weight;
                            if (contributions != null) {
                                var key = methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
                                contributions.TryGetValue(key, out var previous);
                                contributions[key] = previous + item.weight;
                            }
                            break;
                        }
                    }
                }
            }

            return new WeightsInfo() {
                weight = weight,
            };
        }
        
        /// <summary>
        /// Returns job types info.
        /// </summary>
        public static System.Collections.Generic.HashSet<TypeInfo> GetJobTypesInfo(System.Type jobType, System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction = null) {
            var result = new System.Collections.Generic.HashSet<TypeInfo>();
            foreach (var root in GetJobExecuteMethods(jobType)) result.UnionWith(GetMethodTypesInfo(root, onInstruction: onInstruction));
            UpdateDeps(result);
            return result;
        }

        internal static MethodInfo[] GetJobExecuteMethods(System.Type jobType) {
            if (jobType == null || jobType.ContainsGenericParameters)
                throw new System.InvalidOperationException("Job safety requires a closed job type: " + jobType);
            var roots = new System.Collections.Generic.HashSet<MethodInfo>();
            foreach (var contract in jobType.GetInterfaces().OrderBy(type => type.AssemblyQualifiedName, System.StringComparer.Ordinal)) {
                if (contract.Namespace != "ME.BECS.Jobs" && contract.Namespace != "Unity.Jobs") continue;
                // Interface maps handle explicit implementations and Unity's
                // in/modreq forwarding methods; a same-named overload is not a root.
                var map = jobType.GetInterfaceMap(contract);
                for (var index = 0; index < map.InterfaceMethods.Length; ++index)
                    if (map.InterfaceMethods[index].Name == "Execute") roots.Add(UnwrapJobExecuteForwarder(map.TargetMethods[index]));
            }
            if (roots.Count == 0)
                throw new System.InvalidOperationException("No supported job Execute implementation: " + jobType.AssemblyQualifiedName);
            return roots.OrderBy(method => method.DeclaringType.AssemblyQualifiedName, System.StringComparer.Ordinal)
                .ThenBy(method => method.ToString(), System.StringComparer.Ordinal).ToArray();
        }

        internal static MethodInfo UnwrapJobExecuteForwarder(MethodInfo root) {
            var parameters = root.GetParameters();
            // Roslyn bridges imported `in` modreq signatures with an explicit
            // interface method. Its parameters omit the user's RO/WO annotations.
            // Only accept the exact load-this/arguments, call, ret adapter shape.
            if (!root.IsPrivate || !root.IsVirtual || !root.IsFinal || root.IsStatic || root.ReturnType != typeof(void) ||
                !parameters.Any(parameter => parameter.GetRequiredCustomModifiers().Any(modifier =>
                    modifier.FullName == "System.Runtime.InteropServices.InAttribute")) ||
                parameters.Any(parameter => parameter.IsDefined(typeof(ROAttribute), false) ||
                    parameter.IsDefined(typeof(WOAttribute), false) || parameter.IsDefined(typeof(RWAttribute), false))) return root;
            var body = root.GetMethodBody();
            if (body == null || body.ExceptionHandlingClauses.Count != 0) return root;
            var instructions = ILAnalysisSession.Instructions(root).Where(instruction => instruction.OpCode != OpCodes.Nop).ToArray();
            if (instructions.Length != parameters.Length + 3 || instructions[instructions.Length - 1].OpCode != OpCodes.Ret ||
                instructions[instructions.Length - 2].OpCode != OpCodes.Call ||
                !(instructions[instructions.Length - 2].Operand is MethodInfo target) || target == root ||
                target.IsStatic || target.Name != "Execute" || target.ReturnType != typeof(void) ||
                target.DeclaringType != root.DeclaringType || target.ContainsGenericParameters ||
                !target.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters.Select(parameter => parameter.ParameterType))) return root;
            for (var index = 0; index <= parameters.Length; ++index) {
                var instruction = instructions[index];
                var argument = instruction.OpCode == OpCodes.Ldarg_0 ? 0 : instruction.OpCode == OpCodes.Ldarg_1 ? 1 :
                    instruction.OpCode == OpCodes.Ldarg_2 ? 2 : instruction.OpCode == OpCodes.Ldarg_3 ? 3 :
                    (instruction.OpCode == OpCodes.Ldarg || instruction.OpCode == OpCodes.Ldarg_S) && instruction.Operand is ParameterInfo parameter
                        ? parameter.Position + 1 : -1;
                if (argument != index) return root;
            }
            return target;
        }

        /// <summary>
        /// Updates deps.
        /// </summary>
        public static void UpdateDeps(System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> uniqueTypes) {
            if (uniqueTypes == null) return;
            var list = uniqueTypes.ToList();
            for (uint j = 0u; j < list.Count; ++j) {
                var item = list[(int)j];
                var src = item;
                if (item.op == RefOp.ReadOnly) {
                    item.op = RefOp.WriteOnly;
                    if (uniqueTypes.Contains(item) == true) {
                        uniqueTypes.Remove(src);
                        uniqueTypes.Remove(item);
                        item.op = RefOp.ReadWrite;
                        uniqueTypes.Add(item);
                    }
                }
                if (item.op == RefOp.ReadOnly ||
                    item.op == RefOp.WriteOnly) {
                    item.op = RefOp.ReadWrite;
                    if (uniqueTypes.Contains(item) == true) {
                        uniqueTypes.Remove(src);
                    }
                }
            }
        }

        
        private void CollectLegacyInitialization() {
            this.CollectLegacyEarlyInit<IJobForComponentsBase, TNull, TNull>("DoComponents");
            this.CollectLegacyEarlyInit<IJobParallelForComponentsBase, IComponentBase, TNull>("DoParallelForComponents");
            this.CollectLegacyEarlyInit<IJobForComponentsBase, IComponentBase, TNull>("DoComponents");
            this.CollectLegacyEarlyInit<IJobParallelForAspectsBase, IAspect, TNull>("DoParallelForAspect");
            this.CollectLegacyEarlyInit<IJobForAspectsBase, IAspect, TNull>("DoAspect");
            this.CollectLegacyEarlyInit<IJobForAspectsComponentsBase, IAspect, IComponentBase>("DoAspectsComponents");
            this.CollectLegacyEarlyInit<IJobParallelForAspectsComponentsBase, IAspect, IComponentBase>("DoParallelForAspectsComponents");
            
        }


    }

}
