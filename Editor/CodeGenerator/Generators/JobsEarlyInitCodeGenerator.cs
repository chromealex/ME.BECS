using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor.Jobs {
    
    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {

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

        }

        private void Generate<TJobBase, T0, T1>(System.Collections.Generic.List<string> dataList, string method) {
            
            {
                var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase)).OrderBy(x => x.FullName).ToList();
                CodeGenerator.PatchSystemsList(jobsComponents);
                foreach (var jobType in jobsComponents) {

                    if (jobType.IsValueType == false) continue;
                    if (jobType.IsVisible == false) continue;

                    if (this.IsValidTypeForAssembly(jobType) == false) continue;

                    if (jobType.IsGenericType == true && jobType.DeclaringType != null && jobType.DeclaringType.IsGenericType == true) {
                    } else if (jobType.IsGenericType == true) {
                        throw new System.Exception($"Generic jobs are not supported: {jobType.FullName}.");
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

                        var methods = typeof(ME.BECS.Jobs.EarlyInit).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
                            UnityEngine.Debug.LogWarning($"[ CodeGenerator ] Failed to generate EarlyInit method for job type {jobTypeFullName}.");
                            continue;
                        }
                        var str = $"EarlyInit.{methodInfoResult.Name}<{jobTypeFullName}, {string.Join(", ", components)}>();";
                        if (components.Count == 0) str = $"EarlyInit.{methodInfoResult.Name}<{jobTypeFullName}>();";
                        dataList.Add(str);

                    }

                }
            }
            
        }

        public enum JobType {
            Aspect,
            Components,
            Combined,
        }
        
        public override FileContent[] AddFileContent() {

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
            
            cacheBuilderFile.content = $"public static unsafe class DebugJobsCache {{\n{cacheBuilder}\n}}";
            funcBuilderFile.content = $"public static unsafe class DebugJobsFunc {{\n{funcBuilder}\n}}";
            structBuilderFile.content = $"public static unsafe class DebugJobsStruct {{\n{structBuilder}\n}}";
            structUnsafeBuilderFile.content = $"public static unsafe class DebugJobsStructUnsafe {{\n{structUnsafeBuilder}\n}}";

            files[0] = cacheBuilderFile;
            files[1] = funcBuilderFile;
            files[2] = structBuilderFile;
            files[3] = structUnsafeBuilderFile;
            
            return files;
            
        }

        private void AddJobs<TJobBase, T0, T1>(ref int uniqueId, System.Text.StringBuilder cacheBuilder, System.Text.StringBuilder funcBuilder, System.Text.StringBuilder structBuilder, System.Text.StringBuilder structUnsafeBuilder, JobType genType) {
            
            {
                var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase)).OrderBy(x => x.FullName).ToList();
                CodeGenerator.PatchSystemsList(jobsComponents);
                foreach (var jobType in jobsComponents) {
                    if (jobType.IsValueType == false) continue;
                    if (jobType.IsVisible == false) continue;
                    if (this.IsValidTypeForAssembly(jobType) == false) continue;

                    if (jobType.IsGenericType == true && jobType.DeclaringType != null && jobType.DeclaringType.IsGenericType == true) {
                    } else if (jobType.IsGenericType == true) {
                        throw new System.Exception($"Generic jobs are not supported: {jobType.FullName}.");
                    }
                    
                    var jobTypeFullName = EditorUtils.GetTypeName(jobType);
                    var aspects = new System.Collections.Generic.List<string>();
                    var components = new System.Collections.Generic.List<string>();
                    var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
                    var componentsType = new System.Collections.Generic.HashSet<System.Type>();
                    var interfaces = jobType.GetInterfaces();
                    System.Type workInterface = null;
                    foreach (var i in interfaces) {
                        if (i.IsGenericType == true) {
                            foreach (var type in i.GenericTypeArguments) {
                                if (typeof(T0).IsAssignableFrom(type) == true) {
                                    if (this.IsValidTypeForAssembly(type) == false) continue;
                                    components.Add(EditorUtils.GetDataTypeName(type));
                                    componentsType.Add(type);
                                }

                                if (typeof(T1).IsAssignableFrom(type) == true) {
                                    if (this.IsValidTypeForAssembly(type) == false) continue;
                                    aspects.Add(EditorUtils.GetDataTypeName(type));
                                    aspectsType.Add(type);
                                }
                            }

                            workInterface = i;
                            break;
                        }
                    }

                    var uniqueTypes = GetJobTypesInfo(jobType);
                    
                    ++uniqueId;
                    var structName = $"JobDebugData{uniqueId}";

                    cacheBuilder.AppendLine($"private struct Cache{structName} {{");
                    cacheBuilder.AppendLine($"public static readonly SharedStatic<System.IntPtr> cache = SharedStatic<System.IntPtr>.GetOrCreate<Cache{structName}>();");
                    cacheBuilder.AppendLine($"}}");

                    funcBuilder.AppendLine($"{{ // {jobType.FullName}");
                    funcBuilder.AppendLine($"Cache{structName}.cache.Data = default;");
                    funcBuilder.AppendLine($"[BurstCompile]");
                    funcBuilder.AppendLine($"static void* Method(void* jobData, CommandBuffer* buffer, bool unsafeMode, ScheduleFlags scheduleFlags) {{");
                    funcBuilder.AppendLine($"{structName}* data = ({structName}*)Cache{structName}.cache.Data;");
                    funcBuilder.AppendLine($"if (data == null) {{");
                    funcBuilder.AppendLine($"if (unsafeMode == true) {{");
                    funcBuilder.AppendLine($"data = ({structName}*)_makeDefault(new {structName}Unsafe(), Constants.ALLOCATOR_DOMAIN).ptr;");
                    funcBuilder.AppendLine($"}} else {{");
                    funcBuilder.AppendLine($"data = ({structName}*)_makeDefault(new {structName}(), Constants.ALLOCATOR_DOMAIN).ptr;");
                    funcBuilder.AppendLine($"}}");
                    funcBuilder.AppendLine($"Cache{structName}.cache.Data = (System.IntPtr)data;");
                    funcBuilder.AppendLine($"}}");
                    funcBuilder.AppendLine($"data->scheduleFlags = scheduleFlags;");
                    funcBuilder.AppendLine($"data->jobData = *({jobTypeFullName}*)jobData;");
                    funcBuilder.AppendLine($"data->buffer = buffer;");
                    
                    structBuilder.AppendLine($"public struct {structName} {{ // {jobType.FullName}");
                    structBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public ScheduleFlags scheduleFlags;");
                    structBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                    structBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                    structUnsafeBuilder.AppendLine($"public struct {structName}Unsafe {{ // {jobType.FullName}");
                    structUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public ScheduleFlags scheduleFlags;");
                    structUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                    structUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                    if (workInterface != null && (components.Count + aspects.Count) == workInterface.GenericTypeArguments.Length) {

                        {
                            var i = 0u;
                            
                            i = 0u;
                            foreach (var component in aspects) {
                                structBuilder.AppendLine($"public {component} a{i};");
                                structUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public {component} a{i};");
                                funcBuilder.AppendLine($"data->a{i} = buffer->state.ptr->aspectsStorage.Initialize<{component}>(buffer->state);");
                                ++i;
                            }
                            
                            i = 0u;
                            foreach (var component in components) {
                                structBuilder.AppendLine($"public RefRW<{component}> c{i};");
                                structUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public RefRW<{component}> c{i};");
                                funcBuilder.AppendLine($"data->c{i} = buffer->state.ptr->components.GetRW<{component}>(buffer->state, buffer->worldId);");
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
                                funcBuilder.AppendLine($"data->{fieldName} = new SafetyComponentContainer{RWRO}<{type}>(buffer->state, buffer->worldId);");
                                structBuilder.AppendLine($"public SafetyComponentContainer{RWRO}<{type}> {fieldName};");
                                structUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public SafetyComponentContainer{RWRO}<{type}> {fieldName};");
                                ++i;
                            }
                        }

                    }
                    structBuilder.AppendLine($"}}");
                    structUnsafeBuilder.AppendLine($"}}");
                    
                    funcBuilder.AppendLine($"return data;");
                    funcBuilder.AppendLine($"}}");
                    funcBuilder.AppendLine($"var fn = BurstCompiler.CompileFunctionPointer<CompiledJobCallback>(Method);");
                    funcBuilder.AppendLine($"CompiledJobs<{jobTypeFullName}>.SetFunction(fn, (unsafeMode) => unsafeMode == true ? typeof({structName}Unsafe) : typeof({structName}));");
                    funcBuilder.AppendLine($"}}");
                    
                }
            }
            
        }

        public static System.Collections.Generic.HashSet<TypeInfo> GetMethodTypesInfo(MethodInfo root, bool traverseHierarchy = true) {
            var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
            var componentsType = new System.Collections.Generic.HashSet<System.Type>();

            var parameters = root.GetParameters();
            foreach (var p in parameters) {
                if (p.ParameterType.GetInterfaces().Contains(typeof(IAspect)) == true) {
                    aspectsType.Add(p.ParameterType.GetElementType());
                } else if (p.ParameterType.GetInterfaces().Contains(typeof(IComponentBase)) == true) {
                    componentsType.Add(p.ParameterType.GetElementType());
                }
            }
            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
            var visited = new System.Collections.Generic.HashSet<System.Reflection.MethodInfo>();
            while (q.Count > 0) {
                var body = q.Dequeue();
                var instructions = body.GetInstructions();
                foreach (var inst in instructions) {
                    var continueTraverse = true;
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
                    {
                        if (inst.Operand is FieldInfo field && typeof(IComponentBase).IsAssignableFrom(field.DeclaringType) == true) {
                            uniqueTypes.Add(new TypeInfo() {
                                type = field.DeclaringType,
                                op = (componentsType.Contains(field.DeclaringType) == true || aspectsType.Contains(field.DeclaringType) == true) && (inst.OpCode == System.Reflection.Emit.OpCodes.Stfld || inst.OpCode == System.Reflection.Emit.OpCodes.Stobj || inst.OpCode == System.Reflection.Emit.OpCodes.Ldflda) ? RefOp.WriteOnly : RefOp.ReadOnly,
                                isArg = componentsType.Contains(field.DeclaringType),
                            });
                            continueTraverse = false;
                        }
                    }
                    if (inst.Operand is System.Reflection.MethodInfo method && method.IsGenericMethod == true) {
                        var safetyCheck = method.GetCustomAttribute<SafetyCheckAttribute>();
                        if (safetyCheck != null) {
                            var type = method.GetGenericArguments()[0];
                            if (typeof(IComponentBase).IsAssignableFrom(type) == true) {
                                uniqueTypes.Add(new TypeInfo() {
                                    type = type,
                                    op = safetyCheck.Op,
                                });
                                continueTraverse = false;
                            }
                        }
                    }

                    if (continueTraverse == true && traverseHierarchy == true && inst.Operand is System.Reflection.MethodInfo member) {
                        if (visited.Add(member) == true && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                            if (member.GetMethodBody() != null) q.Enqueue(member);
                        }
                    }
                }
            }

            return uniqueTypes;
        }
        
        public static System.Collections.Generic.HashSet<TypeInfo> GetJobTypesInfo(System.Type jobType) {
            var root = jobType.GetMethod("Execute");
            return GetMethodTypesInfo(root);
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

            this.GenerateJobsDebug(dataList, references);
            this.Generate<IJobForComponentsBase, TNull, TNull>(dataList, "DoComponents");
            this.Generate<IJobParallelForComponentsBase, IComponentBase, TNull>(dataList, "DoParallelForComponents");
            this.Generate<IJobForComponentsBase, IComponentBase, TNull>(dataList, "DoComponents");
            this.Generate<IJobParallelForAspectsBase, IAspect, TNull>(dataList, "DoParallelForAspect");
            this.Generate<IJobForAspectsBase, IAspect, TNull>(dataList, "DoAspect");
            this.Generate<IJobForAspectsComponentsBase, IAspect, IComponentBase>(dataList, "DoAspectsComponents");
            this.Generate<IJobParallelForAspectsComponentsBase, IAspect, IComponentBase>(dataList, "DoParallelForAspectsComponents");
            
        }

    }

}