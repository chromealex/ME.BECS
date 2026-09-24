using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using System.Reflection.Emit;
using ME.BECS.Editor.Systems;

namespace ME.BECS.Editor.Jobs {
    
    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {

        private System.Collections.Generic.List<(System.Type job, string call)> earlyInitDiagnostics;
        private MethodInfo[] earlyInitMethods;
        private static readonly System.Type[] EarlyInitContracts = {
            typeof(IJobForComponentsBase), typeof(IJobParallelForComponentsBase), typeof(IJobForComponentsBase),
            typeof(IJobParallelForAspectsBase), typeof(IJobForAspectsBase), typeof(IJobForAspectsComponentsBase),
            typeof(IJobParallelForAspectsComponentsBase),
        };
        private readonly SourceGeneratorJobWeights sourceWeights = new SourceGeneratorJobWeights();
        private readonly SourceGeneratorJobEntityCounts sourceEntityCounts = new SourceGeneratorJobEntityCounts();
        private readonly SourceGeneratorJobSizes sourceSizes = new SourceGeneratorJobSizes();
        private readonly SourceGeneratorJobSafety sourceSafety = new SourceGeneratorJobSafety();
        private readonly System.Collections.Generic.Dictionary<System.Type, uint> selectedWeights = new System.Collections.Generic.Dictionary<System.Type, uint>();

        // Only one generation pass may reuse these statements: their dependencies include
        // called methods, component layouts, entity-group ordering and source catalogs.
        private readonly System.Collections.Generic.Dictionary<System.Type, string[]> jobInitializations =
            new System.Collections.Generic.Dictionary<System.Type, string[]>();

        private uint SelectWeight(System.Type jobType) {
            if (this.selectedWeights.TryGetValue(jobType, out var weight)) return weight;
            weight = this.sourceWeights.TryGetComplete(jobType, out var sourceWeight)
                ? sourceWeight : GetJobWeightsInfo(jobType).weight;
            this.selectedWeights.Add(jobType, weight);
            return weight;
        }

        private string WeightInitialization(System.Type jobType) => this.sourceWeights.TryGetInitializer(jobType, out var call)
            ? call : $"JobStaticInfo<{EditorUtils.GetTypeName(jobType)}>.opsWeight = {this.SelectWeight(jobType)}u;";

        internal static string CompareEarlyInit(System.Collections.Generic.List<System.Type> jobs, bool editor, out int unavailable) {
            return CompareEarlyInit(jobs, editor, out unavailable, out _);
        }

        private static string CompareEarlyInit(System.Collections.Generic.List<System.Type> jobs, bool editor, out int unavailable,
            out System.Collections.Generic.List<(System.Type job, string call)> initialization) {
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
            out System.Collections.Generic.List<(System.Type job, string call)> initialization) {
            initialization = new System.Collections.Generic.List<(System.Type job, string call)>();
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
                    string generated = null;
                    if (candidates.Length == 1) {
                        var call = candidates[0].Value;
                        source.Add((job, call));
                        generated = SourceGeneratorBridge.ResolveJobEarlyInit(job, call, out var reason);
                        if (generated == call) {
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

        private string[] GetJobInitialization(System.Type jobType) {
            if (this.jobInitializations.TryGetValue(jobType, out var cached)) return cached;
            var content = new System.Collections.Generic.List<string>();
            var jobTypeFullName = EditorUtils.GetTypeName(jobType);
            if (this.sourceEntityCounts.TrySelect(jobType, this, out var countInitializer)) {
                content.Add(countInitializer);
            } else {
                var entsInfo = GetJobEntInfo(jobType, this);
                var maximum = jobType.GetCustomAttribute<EntitiesJobMaxCountAttribute>()?.count ?? 0u;
                content.Add($"JobStaticInfo<{jobTypeFullName}>.entitiesMaxCount = {maximum}u;");
                uint[] reservations = entsInfo.count?.Select(count => (uint)count).ToArray();
                if (maximum > 0u && entsInfo.brCount > 0) {
                    reservations ??= new uint[entsInfo.loopGroups.Length];
                    for (var group = 0; group < reservations.Length; ++group)
                        if (entsInfo.loopGroups[group]) reservations[group] = maximum;
                }
                content.Add($"JobStaticInfo<{jobTypeFullName}>.loopCount = {entsInfo.brCount}u;");
                if (reservations != null) {
                    content.Add($"JobStaticInfo<{jobTypeFullName}>.inlineCount = _makeArray<uint>({reservations.Length}u, Allocator.Domain);");
                    for (uint i = 0u; i < reservations.Length; ++i) {
                        if (reservations[i] > 0) content.Add($"JobStaticInfo<{jobTypeFullName}>.inlineCount[{i}u] = {reservations[i]}u;");
                    }
                } else {
                    content.Add($"JobStaticInfo<{jobTypeFullName}>.inlineCount = default;");
                }
            }

            var typeInfos = this.sourceSafety.Select(jobType);
            var sizeComponents = typeInfos.Select(item => item.type)
                .Where(type => typeof(IComponent).IsAssignableFrom(type)).Distinct().ToArray();
            content.Add(this.WeightInitialization(jobType));
            if (this.sourceSizes.TrySelect(jobType, sizeComponents, out var sizeInitializer))
                content.Add(sizeInitializer);
            else {
                var maxStructSize = 0u;
                foreach (var component in sizeComponents) {
                    var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(component);
                    if (size > maxStructSize) maxStructSize = size;
                }
                content.Add($"JobStaticInfo<{jobTypeFullName}>.maxStructSize = {maxStructSize}u;");
            }

            var result = content.ToArray();
            this.jobInitializations.Add(jobType, result);
            return result;
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
        
        public override FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {

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
            var uniqueId = 0;
            cacheBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            structBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            structUnsafeBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"public static void InitializeJobsDebug() {{");
            this.AddJobs<IJobParallelForComponentsBase, IComponentBase, TNull>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForComponentsBase, IComponentBase, TNull>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Components);
            this.AddJobs<IJobParallelForAspectsBase, TNull, IAspect>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsBase, TNull, IAspect>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsComponentsBase, IComponentBase, IAspect>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
            this.AddJobs<IJobParallelForAspectsComponentsBase, IComponentBase, IAspect>(ref uniqueId, cacheBuilder, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
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
        
        private void AddJobs<TJobBase, T0, T1>(ref int uniqueId, System.Text.StringBuilder cacheBuilder, System.Text.StringBuilder funcBuilder, System.Text.StringBuilder structBuilder, System.Text.StringBuilder structUnsafeBuilder, JobType genType) {
            
            this.cache.SetKey(typeof(TJobBase).Name);
            var jobsComponents = this.GetTypesDerivedFrom(typeof(TJobBase)).OrderBy(x => x.FullName).ToList();
            CodeGenerator.PatchSystemsList(jobsComponents);
            foreach (var jobType in jobsComponents) {
                if (jobType.IsValueType == false) continue;
                if (jobType.IsVisible == false) continue;
                if (this.IsValidTypeForAssembly(jobType) == false) continue;
                this.references.Add(jobType);
                if (this.cache.TryGetValue<Item>(jobType, out var item) == true) {
                    cacheBuilder.AppendLine(item.cacheBuilder);
                    funcBuilder.AppendLine(item.funcBuilder);
                    structBuilder.AppendLine(item.structBuilder);
                    structUnsafeBuilder.AppendLine(item.structUnsafeBuilder);
                    ++uniqueId;
                    continue;
                }

                if (jobType.IsGenericType == true && jobType.DeclaringType != null && jobType.DeclaringType.IsGenericType == true) {
                } else if (jobType.IsGenericType == true) {
                    throw new System.Exception($"Generic jobs are not supported: {jobType.FullName}.");
                }
                
                var tempCacheBuilder = new System.Text.StringBuilder();
                var tempFuncBuilder = new System.Text.StringBuilder();
                var tempStructBuilder = new System.Text.StringBuilder();
                var tempStructUnsafeBuilder = new System.Text.StringBuilder();
                
                var jobTypeFullName = EditorUtils.GetTypeName(jobType);
                var aspects = new System.Collections.Generic.List<string>();
                var components = new System.Collections.Generic.List<string>();
                //var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
                //var componentsType = new System.Collections.Generic.HashSet<System.Type>();
                var interfaces = jobType.GetInterfaces();
                System.Type workInterface = null;
                foreach (var i in interfaces) {
                    if (i.IsGenericType == true) {
                        foreach (var type in i.GenericTypeArguments) {
                            if (typeof(T0).IsAssignableFrom(type) == true) {
                                if (this.IsValidTypeForAssembly(type) == false) continue;
                                components.Add(EditorUtils.GetDataTypeName(type));
                                //componentsType.Add(type);
                            }

                            if (typeof(T1).IsAssignableFrom(type) == true) {
                                if (this.IsValidTypeForAssembly(type) == false) continue;
                                aspects.Add(EditorUtils.GetDataTypeName(type));
                                //aspectsType.Add(type);
                            }
                        }

                        workInterface = i;
                        break;
                    }
                }

                var uniqueTypes = this.sourceSafety.Select(jobType);
                
                ++uniqueId;
                var structName = $"JobDebugData{uniqueId}";

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
                if (workInterface != null && (components.Count + aspects.Count) == workInterface.GenericTypeArguments.Length) {

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
                        UpdateDeps(uniqueTypes);

                        var i = 0u;
                        var uniqueTypesSorted = uniqueTypes.ToList().OrderBy(x => x.type.FullName);
                        foreach (var typeInfo in uniqueTypesSorted) {
                            var type = EditorUtils.GetDataTypeName(typeInfo.type);
                            var RWRO = string.Empty;
                            if (typeInfo.op == RefOp.ReadOnly) RWRO = "RO";
                            if (typeInfo.op == RefOp.WriteOnly) RWRO = "WO";
                            if (typeInfo.op == RefOp.ReadWrite) RWRO = "RW";
                            var fieldName = EditorUtils.GetCodeName(type);
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
                this.cache.Add(jobType, data);
                
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

        private void GenerateJobsDebug(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            dataList.Add("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            dataList.Add("DebugJobs.InitializeJobsDebug();");
            dataList.Add("#endif");
        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            if (this.earlyInitDiagnostics == null) {
                // Until the legacy oracle is retired, every export must prove full selection/order
                // parity for the current assemblies. A mismatch is not permission to fall back.
                var comparison = CompareEarlyInit(this.jobTypes, this.editorAssembly, out var issues, out var initialization);
                if (issues != 0) throw new System.InvalidOperationException("Source EarlyInit preflight failed:\n" + comparison);
                this.GenerateJobsDebug(dataList, references);
                this.AddSourceEarlyInit(dataList, initialization);
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

        private void AddSourceEarlyInit(System.Collections.Generic.List<string> dataList,
            System.Collections.Generic.List<(System.Type job, string call)> initialization) {
            // Emit exactly the snapshot verified by preflight; do not rediscover, reselect or
            // invoke argument getters between comparison and emission.
            foreach (var entry in initialization) {
                dataList.AddRange(this.GetJobInitialization(entry.job));
                if (entry.call != null) dataList.Add(entry.call);
            }
        }

    }

}
