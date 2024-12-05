using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor.Jobs {
    
    public class JobsEarlyInitCodeGenerator : CustomCodeGenerator {

        private void Generate<TJobBase, T0, T1>(System.Collections.Generic.List<string> dataList, string method) {
            
            {
                var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase));
                foreach (var jobType in jobsComponents) {

                    if (jobType.IsValueType == false) continue;
                    if (jobType.IsVisible == false) continue;

                    if (this.IsValidTypeForAssembly(jobType) == false) continue;

                    var jobTypeFullName = EditorUtils.GetTypeName(jobType);
                    var components = new System.Collections.Generic.List<string>();
                    var interfaces = jobType.GetInterfaces();
                    System.Type workInterface = null;
                    foreach (var i in interfaces) {
                        if (i.IsGenericType == true) {
                            foreach (var type in i.GenericTypeArguments) {
                                if (typeof(T0).IsAssignableFrom(type) == true ||
                                    typeof(T1).IsAssignableFrom(type) == true) {
                                    if (this.IsValidTypeForAssembly(type) == false) continue;
                                    components.Add(EditorUtils.GetDataTypeName(type));
                                }
                            }

                            workInterface = i;
                            break;
                        }
                    }

                    if (workInterface != null && components.Count == workInterface.GenericTypeArguments.Length) {

                        var str = $"ME.BECS.Jobs.EarlyInit.{method}<{jobTypeFullName}, {string.Join(", ", components)}>();";
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
        
        public override string AddPublicContent() {

            var funcBuilder = new System.Text.StringBuilder();
            var structBuilder = new System.Text.StringBuilder();
            var structUnsafeBuilder = new System.Text.StringBuilder();
            var uniqueId = 0;
            structBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            structUnsafeBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            funcBuilder.AppendLine($"public static void InitializeJobsDebug() {{");
            this.AddJobs<IJobParallelForComponentsBase, IComponentBase, TNull>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForComponentsBase, IComponentBase, TNull>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Components);
            this.AddJobs<IJobParallelForAspectsBase, TNull, IAspect>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsBase, TNull, IAspect>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Aspect);
            this.AddJobs<IJobForAspectsComponentsBase, IComponentBase, IAspect>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
            this.AddJobs<IJobParallelForAspectsComponentsBase, IComponentBase, IAspect>(ref uniqueId, funcBuilder, structBuilder, structUnsafeBuilder, JobType.Combined);
            funcBuilder.AppendLine($"}}");
            funcBuilder.AppendLine($"#endif");
            structBuilder.AppendLine($"#endif");
            structUnsafeBuilder.AppendLine($"#endif");
            return structBuilder.ToString() + "\n" + structUnsafeBuilder.ToString() + "\n" + funcBuilder.ToString();

        }

        public struct TypeInfo : System.IEquatable<TypeInfo> {

            public System.Type type;
            public RefOp op;

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
        
        private void AddJobs<TJobBase, T0, T1>(ref int uniqueId, System.Text.StringBuilder funcBuilder, System.Text.StringBuilder structBuilder, System.Text.StringBuilder structUnsafeBuilder, JobType genType) {
            
            {
                var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase));
                foreach (var jobType in jobsComponents) {
                    if (jobType.IsValueType == false) continue;
                    if (jobType.IsVisible == false) continue;
                    if (this.IsValidTypeForAssembly(jobType) == false) continue;

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
                    
                    var root = jobType.GetMethod("Execute");
                    var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
                    q.Enqueue(root);
                    var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
                    var visited = new System.Collections.Generic.HashSet<System.Reflection.MethodInfo>();
                    while (q.Count > 0) {
                        var body = q.Dequeue();
                        var instructions = body.GetInstructions();
                        foreach (var inst in instructions) {
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
                                }
                            }
                            {
                                if (inst.Operand is FieldInfo field && typeof(IComponentBase).IsAssignableFrom(field.DeclaringType) == true) {
                                    uniqueTypes.Add(new TypeInfo() {
                                        type = field.DeclaringType,
                                        op = (componentsType.Contains(field.DeclaringType) == true || aspectsType.Contains(field.DeclaringType) == true) && inst.OpCode == System.Reflection.Emit.OpCodes.Stfld ? RefOp.WriteOnly : RefOp.ReadOnly,
                                    });
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
                                    }
                                }
                            }

                            if (inst.Operand is System.Reflection.MethodInfo member) {
                                if (visited.Add(member) == true) {
                                    if (member.GetMethodBody() != null) q.Enqueue(member);
                                }
                            }
                        }
                    }

                    ++uniqueId;
                    var structName = $"JobDebugData{uniqueId}";

                    funcBuilder.AppendLine($"{{ // {jobType.FullName}");
                    funcBuilder.AppendLine($"[BurstCompile]");
                    funcBuilder.AppendLine($"static void* Method(void* jobData, CommandBuffer* buffer, bool unsafeMode) {{");
                    funcBuilder.AppendLine($"{structName}* data = null;");
                    funcBuilder.AppendLine($"if (unsafeMode == true) {{");
                    funcBuilder.AppendLine($"data = ({structName}*)_make(new {structName}Unsafe(), Constants.ALLOCATOR_TEMP);");
                    funcBuilder.AppendLine($"}} else {{");
                    funcBuilder.AppendLine($"data = ({structName}*)_make(new {structName}(), Constants.ALLOCATOR_TEMP);");
                    funcBuilder.AppendLine($"}}");
                    funcBuilder.AppendLine($"data->jobData = *({jobTypeFullName}*)jobData;");
                    funcBuilder.AppendLine($"data->buffer = buffer;");
                    
                    structBuilder.AppendLine($"public struct {structName} {{ // {jobType.FullName}");
                    structBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                    structBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                    structUnsafeBuilder.AppendLine($"public struct {structName}Unsafe {{ // {jobType.FullName}");
                    structUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public {jobTypeFullName} jobData;");
                    structUnsafeBuilder.AppendLine($"[NativeDisableUnsafePtrRestriction] public CommandBuffer* buffer;");
                    if (workInterface != null && (components.Count + aspects.Count) == workInterface.GenericTypeArguments.Length) {

                        {
                            var i = 0u;
                            
                            i = 0u;
                            foreach (var component in aspects) {
                                structBuilder.AppendLine($"public {component} a{i};");
                                structUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public {component} a{i};");
                                funcBuilder.AppendLine($"data->a{i} = buffer->state->aspectsStorage.Initialize<{component}>(buffer->state);");
                                ++i;
                            }
                            
                            i = 0u;
                            foreach (var component in components) {
                                structBuilder.AppendLine($"public RefRW<{component}> c{i};");
                                structUnsafeBuilder.AppendLine($"[NativeDisableContainerSafetyRestriction] public RefRW<{component}> c{i};");
                                funcBuilder.AppendLine($"data->c{i} = buffer->state->components.GetRW<{component}>(buffer->state, buffer->worldId);");
                                ++i;
                            }

                        }

                        {
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

        private void GenerateJobsDebug(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            dataList.Add("#if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS");
            dataList.Add("StaticMethods.InitializeJobsDebug();");
            dataList.Add("#endif");
        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {

            this.GenerateJobsDebug(dataList, references);
            this.Generate<IJobParallelForComponentsBase, IComponentBase, TNull>(dataList, "DoParallelForComponents");
            this.Generate<IJobForComponentsBase, IComponentBase, TNull>(dataList, "DoComponents");
            this.Generate<IJobParallelForAspectsBase, IAspect, TNull>(dataList, "DoParallelForAspect");
            this.Generate<IJobForAspectsBase, IAspect, TNull>(dataList, "DoAspect");
            this.Generate<IJobForAspectsComponentsBase, IAspect, IComponentBase>(dataList, "DoParallelForAspectsComponents");
            this.Generate<IJobParallelForAspectsComponentsBase, IAspect, IComponentBase>(dataList, "DoParallelForAspectsComponents");
            
        }

    }

}