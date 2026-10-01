using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using System.Reflection.Emit;
using ME.BECS.Editor.Systems;

namespace ME.BECS.Editor.Jobs {
    
    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {
        public override string SourceInitializationKind => this.GetType() == typeof(JobsEarlyInitCodeGenerator) ? "jobs" : null;

        private System.Collections.Generic.List<(System.Type job, string call)> earlyInitDiagnostics;
        private MethodInfo[] earlyInitMethods;
        private static readonly System.Type[] EarlyInitContracts = {
            typeof(IJobForComponentsBase), typeof(IJobParallelForComponentsBase), typeof(IJobForComponentsBase),
            typeof(IJobParallelForAspectsBase), typeof(IJobForAspectsBase), typeof(IJobForAspectsComponentsBase),
            typeof(IJobParallelForAspectsComponentsBase),
        };
        private readonly SourceGeneratorJobWeights sourceWeights = new SourceGeneratorJobWeights();
        private readonly SourceGeneratorJobEntityCounts sourceEntityCounts = new SourceGeneratorJobEntityCounts();
        private readonly SourceGeneratorJobSafety sourceSafety = new SourceGeneratorJobSafety();
        private readonly System.Collections.Generic.Dictionary<System.Type, uint> selectedWeights = new System.Collections.Generic.Dictionary<System.Type, uint>();

        private uint SelectWeight(System.Type jobType) {
            if (this.selectedWeights.TryGetValue(jobType, out var weight)) return weight;
            weight = this.sourceWeights.TryGetComplete(jobType, out var sourceWeight)
                ? sourceWeight : GetJobWeightsInfo(jobType).weight;
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
            // Same selection path as real generation, but no cache, IL analysis, debug metadata or writes.
            generator.AddInitialization(new System.Collections.Generic.List<string>(), new System.Collections.Generic.List<System.Type>());
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

        private System.Collections.Generic.List<System.Type> SelectEarlyInitJobs(System.Type contract) {
            var jobs = this.GetTypesDerivedFrom(contract).OrderBy(type => type.FullName).ToList();
            CodeGenerator.PatchSystemsList(jobs);
            return jobs;
        }

        public struct TypeInfo : System.IEquatable<TypeInfo> {

            public System.Type type;
            public RefOp op;
            public bool isArg;

            public bool Equals(TypeInfo other) {
                return Equals(this.type, other.type) && this.op == other.op;
            }

            public override bool Equals(object obj) {
                return obj is TypeInfo other && this.Equals(other);
            }

            public override int GetHashCode() {
                return System.HashCode.Combine(this.type, (int)this.op);
            }

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
                    if (i.IsGenericType == true) {
                        foreach (var type in i.GenericTypeArguments) {
                            if (typeof(T0).IsAssignableFrom(type) == true ||
                                typeof(T1).IsAssignableFrom(type) == true) {
                                if (this.IsValidTypeForAssembly(type) == false) continue;
                                components.Add(EditorUtils.GetDataTypeName(type));
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

        public enum JobType {
            Aspect,
            Components,
            Combined,
        }

        private System.Collections.Generic.List<System.Type> references;
        private readonly System.Collections.Generic.HashSet<System.Type> debugInputReferences = new System.Collections.Generic.HashSet<System.Type>();

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
        
        public override FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {
            return new[] {
                new FileContent { filename = "Debug.Cache", content = "public unsafe partial class DebugJobs { private static void SourceDebugPlanV1() { } }" },
                new FileContent { filename = "Debug.Func", content = "// Debug callbacks are emitted by the source generator." },
                new FileContent { filename = "Debug.Struct", content = "// Debug layouts are emitted by the source generator." },
                new FileContent { filename = "Debug.UnsafeStruct", content = "// Unsafe debug layouts are emitted by the source generator." },
            };
        }

        // Retained as a comparison oracle during the transition, not called by export.
        private FileContent[] GenerateLegacyDebugFiles(System.Collections.Generic.List<System.Type> references) {

            this.references = references;
            
            var files = new FileContent[4];
            var cacheBuilderFile = new FileContent() {
                filename = "Debug.Cache",
            };
            var structBuilderFile = new FileContent() {
                filename = "Debug.Struct",
            };
            var structUnsafeBuilderFile = new FileContent() {
                filename = "Debug.UnsafeStruct",
            };
            var funcBuilderFile = new FileContent() {
                filename = "Debug.Func",
            };
            
            var cacheBuilder = new System.Text.StringBuilder();
            var funcBuilder = new System.Text.StringBuilder();
            var structBuilder = new System.Text.StringBuilder();
            var structUnsafeBuilder = new System.Text.StringBuilder();
            cacheBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            structBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            structUnsafeBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"public static void InitializeJobsDebug() {{");
            this.AddJobs<IJobParallelForComponentsBase, IComponentBase, TNull>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForComponentsBase, IComponentBase, TNull>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Components);
            this.AddJobs<IJobParallelForAspectsBase, TNull, IAspect>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsBase, TNull, IAspect>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsComponentsBase, IComponentBase, IAspect>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
            this.AddJobs<IJobParallelForAspectsComponentsBase, IComponentBase, IAspect>(cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
            funcBuilder.AppendLine($"}}");
            funcBuilder.AppendLine($"#endif");
            structBuilder.AppendLine($"#endif");
            structUnsafeBuilder.AppendLine($"#endif");
            cacheBuilder.AppendLine($"#endif");
            
            cacheBuilderFile.content = $"public unsafe partial class DebugJobs {{\n{cacheBuilder}\n}}";
            funcBuilderFile.content = $"[BURST] public unsafe partial class DebugJobs {{\n{funcBuilder}\n}}";
            structBuilderFile.content = $"public unsafe partial class DebugJobs {{\n{structBuilder}\n}}";
            structUnsafeBuilderFile.content = $"public unsafe partial class DebugJobs {{\n{structUnsafeBuilder}\n}}";

            files[0] = cacheBuilderFile;
            files[1] = funcBuilderFile;
            files[2] = structBuilderFile;
            files[3] = structUnsafeBuilderFile;
            
            return files;
            
        }

        public struct Item {

            public string cacheBuilder;
            public string funcBuilder;
            public string structBuilder;
            public string structUnsafeBuilder;

        }

        internal static System.Type GetDebugWorkInterface(System.Type job, System.Type contract) {
            var candidates = job.GetInterfaces().Where(type => type.IsGenericType && contract.IsAssignableFrom(type) &&
                type.Assembly == typeof(IJobForComponentsBase).Assembly).ToArray();
            if (candidates.Length > 1)
                throw new System.InvalidOperationException("Ambiguous debug job contract " + contract.FullName + " on " + job.FullName);
            return candidates.Length == 1 ? candidates[0] : null;
        }
        
        internal static string GetDebugWrapperName(System.Type job, System.Type contract) => "JobDebugData_" +
            ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(job.AssemblyQualifiedName + "\n" + contract.AssemblyQualifiedName);

        internal static string GetDebugSafetyFieldName(System.Type component) => "safety_" +
            ME.BECS.CodeGeneration.SourceGeneratorNames.Hash(component.AssemblyQualifiedName);

        private sealed class DebugWrapperPlan {
            public System.Type job;
            public System.Type contract;
            public System.Type workInterface;
            public System.Type[] components;
            public System.Type[] aspects;
            public TypeInfo[] safety;
            public bool hasTypedArguments;
        }

        public override void AppendSourceGeneratorInputs(System.Text.StringBuilder manifest) {
            this.debugInputReferences.Clear();
            var comparison = CompareEarlyInit(this.jobTypes, this.editorAssembly, out var issues, out var initialization);
            if (issues != 0) throw new System.InvalidOperationException("Source EarlyInit preflight failed:\n" + comparison);
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
            manifest.Append("job-debug-schema\t0\tdjE=\n");
            var contracts = new[] { typeof(IJobParallelForComponentsBase), typeof(IJobForComponentsBase),
                typeof(IJobParallelForAspectsBase), typeof(IJobForAspectsBase),
                typeof(IJobForAspectsComponentsBase), typeof(IJobParallelForAspectsComponentsBase) };
            var ordinal = 0;
            var weightJobs = new System.Collections.Generic.HashSet<System.Type>();
            var entityInitializerOrdinal = 0;
            var entityFallbackOrdinal = 0;
            foreach (var contract in contracts) {
                var componentsOnly = contract == typeof(IJobParallelForComponentsBase) || contract == typeof(IJobForComponentsBase);
                var aspectsOnly = contract == typeof(IJobParallelForAspectsBase) || contract == typeof(IJobForAspectsBase);
                foreach (var job in this.SelectEarlyInitJobs(contract).Distinct()) {
                    if (!job.IsValueType || !job.IsVisible || !this.IsValidTypeForAssembly(job)) continue;
                    var plan = this.CreateDebugWrapperPlan(job, contract, aspectsOnly ? typeof(TNull) : typeof(IComponentBase),
                        componentsOnly ? typeof(TNull) : typeof(IAspect));
                    this.debugInputReferences.Add(plan.job);
                    this.debugInputReferences.Add(plan.contract);
                    if (plan.workInterface != null) this.debugInputReferences.Add(plan.workInterface);
                    foreach (var component in plan.components) this.debugInputReferences.Add(component);
                    foreach (var aspect in plan.aspects) this.debugInputReferences.Add(aspect);
                    foreach (var dependency in plan.safety) this.debugInputReferences.Add(dependency.type);
                    var payload = new System.Text.StringBuilder("v1\n").Append(job.AssemblyQualifiedName).Append('\n')
                        .Append(contract.AssemblyQualifiedName).Append('\n').Append(plan.workInterface?.AssemblyQualifiedName ?? "")
                        .Append('\n').Append(plan.hasTypedArguments ? "1" : "0");
                    foreach (var component in plan.components) payload.Append("\nC\t").Append(component.AssemblyQualifiedName);
                    foreach (var aspect in plan.aspects) payload.Append("\nA\t").Append(aspect.AssemblyQualifiedName);
                    foreach (var dependency in plan.safety)
                        payload.Append("\nS\t").Append(dependency.op).Append('\t').Append(dependency.type.AssemblyQualifiedName);
                    manifest.Append("job-debug\t").Append((ordinal++).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\t')
                        .Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload.ToString()))).Append('\n');
                    if (weightJobs.Add(job)) {
                        if (this.sourceEntityCounts.TrySelectPlan(job, this, out var entityPlan)) {
                            this.debugInputReferences.Add(entityPlan.initializer);
                            var entityPayload = "v1\n" + job.AssemblyQualifiedName + "\n" + entityPlan.initializer.AssemblyQualifiedName +
                                string.Concat(entityPlan.groupKeys.Select(key => "\n" + key));
                            manifest.Append("job-entity-initializer\t").Append((entityInitializerOrdinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                                .Append('\t').Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entityPayload))).Append('\n');
                        } else {
                            var entityPayload = this.GetEntityFallbackPayload(job);
                            manifest.Append("job-entity-fallback\t").Append((entityFallbackOrdinal++).ToString(System.Globalization.CultureInfo.InvariantCulture))
                                .Append('\t').Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(entityPayload))).Append('\n');
                        }
                        var hasInitializer = this.sourceWeights.TryGetInitializerType(job, out var initializerType);
                        if (hasInitializer) this.debugInputReferences.Add(initializerType);
                        manifest.Append("job-weight\t").Append((weightJobs.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                            .Append('\t').Append(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(job.AssemblyQualifiedName)))
                            .Append('\t').Append(hasInitializer ? "source" : "value").Append('\t')
                            .Append(hasInitializer ? System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(initializerType.AssemblyQualifiedName)) :
                                this.SelectWeight(job).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                    }
                }
            }
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
            var components = arguments.Where(type => componentContract.IsAssignableFrom(type) && this.IsValidTypeForAssembly(type)).ToArray();
            var aspects = arguments.Where(type => aspectContract.IsAssignableFrom(type) && this.IsValidTypeForAssembly(type)).ToArray();
            var safety = this.sourceSafety.Select(job);
            UpdateDeps(safety);
            return new DebugWrapperPlan {
                job = job, contract = contract, workInterface = workInterface,
                components = components, aspects = aspects,
                hasTypedArguments = workInterface != null && components.Length + aspects.Length == arguments.Length,
                safety = safety.OrderBy(item => item.type.FullName, System.StringComparer.Ordinal)
                    .ThenBy(item => item.type.Assembly.FullName, System.StringComparer.Ordinal).ToArray(),
            };
        }

        private void AddJobs<TJobBase, T0, T1>(System.Text.StringBuilder cacheBuilder, System.Text.StringBuilder funcBuilder, System.Text.StringBuilder structBuilder, System.Text.StringBuilder structUnsafeBuilder, JobType genType) {
            
            // Wrapper layout includes transitive safety dependencies. A persisted
            // fragment keyed by the job type can outlive changes in called helpers.
            // Re-select from the run-local source/IL snapshot on every export.
            var jobsComponents = this.SelectEarlyInitJobs(typeof(TJobBase));
            foreach (var jobType in jobsComponents) {
                if (jobType.IsValueType == false) continue;
                if (jobType.IsVisible == false) continue;
                if (this.IsValidTypeForAssembly(jobType) == false) continue;
                this.references.Add(jobType);

                if (jobType.ContainsGenericParameters)
                    throw new System.InvalidOperationException($"Debug job requires a closed specialization: {jobType.FullName}.");
                
                var tempCacheBuilder = new System.Text.StringBuilder();
                var tempFuncBuilder = new System.Text.StringBuilder();
                var tempStructBuilder = new System.Text.StringBuilder();
                var tempStructUnsafeBuilder = new System.Text.StringBuilder();
                
                var jobTypeFullName = SourceGeneratorInputManifest.GetClosedTypeName(jobType);
                var plan = this.CreateDebugWrapperPlan(jobType, typeof(TJobBase), typeof(T0), typeof(T1));
                var aspects = plan.aspects.Select(SourceGeneratorInputManifest.GetClosedTypeName).ToArray();
                var components = plan.components.Select(SourceGeneratorInputManifest.GetClosedTypeName).ToArray();
                
                var structName = GetDebugWrapperName(jobType, typeof(TJobBase));

                tempCacheBuilder.AppendLine($"private struct Cache{structName} {{");
                tempCacheBuilder.AppendLine($"public static readonly SharedStatic<System.IntPtr> cache = SharedStatic<System.IntPtr>.GetOrCreate<Cache{structName}>();");
                tempCacheBuilder.AppendLine($"}}");

                tempFuncBuilder.AppendLine($"{{ // {jobType.FullName}");
                tempFuncBuilder.AppendLine($"Cache{structName}.cache.Data = default;");
                tempFuncBuilder.AppendLine($"[BURST]");
                tempFuncBuilder.AppendLine($"static void* Method(void* jobData, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags, in JobInfo jobInfo) {{");
                tempFuncBuilder.AppendLine($"{structName}* data = ({structName}*)Cache{structName}.cache.Data;");
                tempFuncBuilder.AppendLine($"if (data == null) {{");
                tempFuncBuilder.AppendLine($"if (unsafeMode == true) {{");
                tempFuncBuilder.AppendLine($"data = ({structName}*)_makeDefault(new {structName}Unsafe(), Constants.ALLOCATOR_DOMAIN).ptr;");
                tempFuncBuilder.AppendLine($"}} else {{");
                tempFuncBuilder.AppendLine($"data = ({structName}*)_makeDefault(new {structName}(), Constants.ALLOCATOR_DOMAIN).ptr;");
                tempFuncBuilder.AppendLine($"}}");
                tempFuncBuilder.AppendLine($"Cache{structName}.cache.Data = (System.IntPtr)data;");
                tempFuncBuilder.AppendLine($"}}");
                tempFuncBuilder.AppendLine($"data->scheduleFlags = scheduleFlags;");
                tempFuncBuilder.AppendLine($"data->jobInfo = jobInfo;");
                tempFuncBuilder.AppendLine($"data->jobData = *({jobTypeFullName}*)jobData;");
                tempFuncBuilder.AppendLine($"data->buffer = buffer;");
                tempStructBuilder.AppendLine($"public struct {structName} {{ // {jobType.FullName}");
                tempStructBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public ScheduleFlags scheduleFlags;");
                tempStructBuilder.AppendLine($"public JobInfo jobInfo;");
                tempStructBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                tempStructBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                tempStructUnsafeBuilder.AppendLine($"public struct {structName}Unsafe {{ // {jobType.FullName}");
                tempStructUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public ScheduleFlags scheduleFlags;");
                tempStructUnsafeBuilder.AppendLine($"public JobInfo jobInfo;");
                tempStructUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                tempStructUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                if (plan.hasTypedArguments) {

                    {
                        var i = 0u;
                        
                        i = 0u;
                        foreach (var component in aspects) {
                            tempStructBuilder.AppendLine($"public {component} a{i};");
                            tempStructUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public {component} a{i};");
                            tempFuncBuilder.AppendLine($"data->a{i} = WorldAspectStorage.Initialize<{component}>(buffer->worldId);");
                            ++i;
                        }
                        
                        i = 0u;
                        foreach (var component in components) {
                            tempStructBuilder.AppendLine($"public RefRW<{component}> c{i};");
                            tempStructUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public RefRW<{component}> c{i};");
                            tempFuncBuilder.AppendLine($"data->c{i} = buffer->state.ptr->components.GetRW<{component}>(buffer->state, buffer->worldId);");
                            ++i;
                        }

                    }

                    {
                        var i = 0u;
                        foreach (var typeInfo in plan.safety) {
                            var type = SourceGeneratorInputManifest.GetClosedTypeName(typeInfo.type);
                            var RWRO = string.Empty;
                            if (typeInfo.op == RefOp.ReadOnly) RWRO = "RO";
                            if (typeInfo.op == RefOp.WriteOnly) RWRO = "WO";
                            if (typeInfo.op == RefOp.ReadWrite) RWRO = "RW";
                            var fieldName = GetDebugSafetyFieldName(typeInfo.type);
                            tempFuncBuilder.AppendLine($"data->{fieldName} = new SafetyComponentContainer{RWRO}<{type}>(buffer->state, buffer->worldId);");
                            tempStructBuilder.AppendLine($"public SafetyComponentContainer{RWRO}<{type}> {fieldName};");
                            tempStructUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public SafetyComponentContainer{RWRO}<{type}> {fieldName};");
                            ++i;
                        }
                    }

                }
                tempStructBuilder.AppendLine($"}}");
                tempStructUnsafeBuilder.AppendLine($"}}");
                tempFuncBuilder.AppendLine($"return data;");
                tempFuncBuilder.AppendLine($"}}");
                tempFuncBuilder.AppendLine($"var fn = BurstCompiler.CompileFunctionPointer<CompiledJobCallback>(Method);");
                tempFuncBuilder.AppendLine($"CompiledJobs<{jobTypeFullName}>.SetFunction(fn, (unsafeMode) => unsafeMode == true ? typeof({structName}Unsafe) : typeof({structName}));");
                tempFuncBuilder.AppendLine($"}}");

                var data = new Item() {
                    cacheBuilder = tempCacheBuilder.ToString(),
                    funcBuilder = tempFuncBuilder.ToString(),
                    structBuilder = tempStructBuilder.ToString(),
                    structUnsafeBuilder = tempStructUnsafeBuilder.ToString(),
                };
                cacheBuilder.AppendLine(data.cacheBuilder);
                funcBuilder.AppendLine(data.funcBuilder);
                structBuilder.AppendLine(data.structBuilder);
                structUnsafeBuilder.AppendLine(data.structUnsafeBuilder);
                
            }
            
        }

        public static System.Collections.Generic.HashSet<TypeInfo> GetMethodTypesInfo(MethodInfo root, bool traverseHierarchy = true, bool useAnalyzer = false, bool methodParameters = true, System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction = null) {
            var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
            var componentsType = new System.Collections.Generic.HashSet<System.Type>();

            var parametersOverrides = new System.Collections.Generic.HashSet<TypeInfo>();
            if (methodParameters == true) {
                var parameters = root.GetParameters();
                foreach (var p in parameters) {
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
                    }

                    if (p.ParameterType.GetInterfaces().Contains(typeof(IAspect)) == true) {
                        aspectsType.Add(p.ParameterType.GetElementType());
                    } else if (p.ParameterType.GetInterfaces().Contains(typeof(IComponentBase)) == true) {
                        componentsType.Add(p.ParameterType.GetElementType());
                    }
                }
            }

            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
            var visited = new System.Collections.Generic.HashSet<MethodPointerData>(MethodPointerData.ExactComparer);
            while (q.Count > 0) {
                var body = q.Dequeue();
                var deps = useAnalyzer == true ? ILAnalyzer.AnalyzeMethod(body) : null;
                if (deps != null) {
                    foreach (var item in deps) {
                        uniqueTypes.Add(new TypeInfo() {
                            type = item.type,
                            op = item.access,
                            isArg = componentsType.Contains(item.type),
                        });
                    }
                }

                var instructions = body.GetInstructions();
                foreach (var inst in instructions) {
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
                    {
                        if (inst.Operand is System.Reflection.FieldInfo field && typeof(IRefOp).IsAssignableFrom(field.FieldType) == true) {
                            var op = (IRefOp)System.Activator.CreateInstance(field.FieldType);
                            //UnityEngine.Debug.Log(field.FieldType + " :: " + op.Op);
                            uniqueTypes.Add(new TypeInfo() {
                                type = field.FieldType.GenericTypeArguments[0],
                                op = op.Op,
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
                                        var constTypes = UnityEditor.TypeCache.GetTypesDerivedFrom(constraint);
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

                    if (continueTraverse == true && traverseHierarchy == true && inst.Operand is System.Reflection.MethodInfo member) {
                        if (member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null && (member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(new MethodPointerData(member, body.DeclaringType)) == true)) {
                            if (body.DeclaringType.IsGenericType == true && member.DeclaringType.IsInterface == true) {
                                var arg = body.DeclaringType.GetGenericArguments()[0];
                                if (member.DeclaringType.IsAssignableFrom(arg)) {
                                    var map = arg.GetInterfaceMap(member.DeclaringType);
                                    var definition = member.IsGenericMethod ? member.GetGenericMethodDefinition() : member;
                                    for (var methodIndex = 0; methodIndex < map.InterfaceMethods.Length; ++methodIndex) {
                                        if (!map.InterfaceMethods[methodIndex].Equals(definition)) continue;
                                        var target = map.TargetMethods[methodIndex];
                                        if (member.IsGenericMethod && !member.IsGenericMethodDefinition)
                                            target = target.MakeGenericMethod(member.GetGenericArguments());
                                        if (target.GetMethodBody() != null) q.Enqueue(target);
                                        break;
                                    }
                                } else if (member.GetMethodBody() != null) {
                                    q.Enqueue(member);
                                }
                            } else {
                                if (member.GetMethodBody() != null) q.Enqueue(member);
                            }
                        }
                    }
                }
            }

            foreach (var p in parametersOverrides) {
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.ReadOnly, });
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.WriteOnly, });
                uniqueTypes.Remove(new TypeInfo() { type = p.type, op = RefOp.ReadWrite, });
                uniqueTypes.Add(p);
            }
            
            return uniqueTypes;
        }
        
        public struct NewEntInfo {

            public int[] count;
            public int brCount;
            public bool[] loopGroups;

        }
        
        public static NewEntInfo GetJobEntInfo(System.Type jobType, CustomCodeGenerator codeGenerator) {

            var allTypesArray = EntityTypeCodeGenerator.GetAllTypes(codeGenerator, out var groupsCount);
            var allTypes = allTypesArray.ToDictionary(x => x.Item1, x => x.Item2);
            var result = new NewEntInfo();
            var anyCount = 0;
            result.count = new int[groupsCount];
            result.loopGroups = new bool[groupsCount];
            result.brCount = 0;
            var newEntMethod = typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetGenericMethodDefinition();
            var root = jobType.GetMethod("Execute");
            var instructions = root.GetInstructions().ToList();
            var visited = new System.Collections.Generic.HashSet<MethodPointerData>();
            var brOpen = 0;
            for (int i = 0; i < instructions.Count; ++i) {
                var inst = instructions[i];
                if ((inst.OpCode.FlowControl == FlowControl.Branch || inst.OpCode.FlowControl == FlowControl.Cond_Branch) &&
                    inst.Operand is Instruction loopTarget && loopTarget.Offset < inst.Offset) {
                    // jump to previous instruction - make it open
                    ++loopTarget.loopInfo.openCount;
                    ++inst.loopInfo.closeCount;
                }

                if (inst.Operand is System.Reflection.MethodInfo member) {
                    if ((member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(new MethodPointerData(member)) == true) &&
                        member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                        if (member.GetMethodBody() != null) {
                            instructions.InsertRange(i + 1, member.GetInstructions());
                        }
                    }
                }
            }

            for (int i = 0; i < instructions.Count; ++i) {
                var inst = instructions[i];
                if (inst.loopInfo.openCount > 0) {
                    brOpen += inst.loopInfo.openCount;
                }

                if (inst.loopInfo.closeCount > 0) {
                    brOpen -= inst.loopInfo.closeCount;
                }

                if (inst.Operand is MethodInfo methodInfo) {
                    if (methodInfo.IsGenericMethod == true && methodInfo.GetGenericMethodDefinition() == newEntMethod && allTypes.TryGetValue(methodInfo.GetGenericArguments()[0], out var gId) == true) {
                        if (brOpen > 0) {
                            ++result.brCount;
                            result.loopGroups[gId] = true;
                        } else {
                            ref var count = ref result.count[gId];
                            ++count;
                            ++anyCount;
                        }
                    }
                }
            }

            if (anyCount == 0) {
                result.count = null;
            }

            return result;
        }

        public struct WeightsInfo {

            public uint weight;

        }

        private struct MethodWeightInfo {

            public MethodInfo[] methods;
            public uint weight;

        }
        
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
            var root = jobType.GetMethod("Execute");
            var visited = new System.Collections.Generic.HashSet<MethodPointerData>(MethodPointerData.ExactComparer);
            var instructions = root.GetInstructions().ToList();
            for (int i = 0; i < instructions.Count; ++i) {
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
        
        public static System.Collections.Generic.HashSet<TypeInfo> GetJobTypesInfo(System.Type jobType, System.Func<Instruction, System.Collections.Generic.Queue<System.Reflection.MethodInfo>, bool> onInstruction = null) {
            var root = jobType.GetMethod("Execute");
            return GetMethodTypesInfo(root, onInstruction: onInstruction);
        }

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

        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            if (this.earlyInitDiagnostics == null) {
                // Custom derived feeders retain this compatibility entry point. Built-in
                // bootstrap dispatch and the complete ordered body are compiler-owned.
                dataList.Add("global::ME.BECS.SourceGenerated.JobBootstrapInputs.Initialize();");
                return;
            }
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
