using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using System.Reflection.Emit;

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

            public override string ToString() {
                return $"{this.type} {this.op} {this.isArg}";
            }

        }

        private void Generate<TJobBase, T0, T1>(System.Collections.Generic.List<string> dataList, string method) {

            this.cache.SetKey($"{method}:{typeof(TJobBase).Name}:{typeof(T0).Name}:{typeof(T1).Name}");
            var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase)).OrderBy(x => x.FullName).ToList();
            CodeGenerator.PatchSystemsList(jobsComponents);
            foreach (var jobType in jobsComponents) {

                if (this.cache.TryGetValue<System.Collections.Generic.List<string>>(jobType, out var list) == true) {
                    dataList.AddRange(list);
                    continue;
                }
                
                if (jobType.IsValueType == false) continue;
                if (jobType.IsVisible == false) continue;

                if (this.IsValidTypeForAssembly(jobType) == false) continue;

                var content = new System.Collections.Generic.List<string>();
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

                var entsInfo = GetJobEntInfo(jobType);
                if (entsInfo.count > 0 || entsInfo.brCount > 0) {
                    content.Add($"JobStaticInfo<{jobTypeFullName}>.loopCount = {entsInfo.brCount}u;");
                    content.Add($"JobStaticInfo<{jobTypeFullName}>.inlineCount = {entsInfo.count}u;");
                }

                var typeInfos = GetJobTypesInfo(jobType);
                var maxStructSize = 0u;
                foreach (var item in typeInfos) {
                    if (typeof(IComponent).IsAssignableFrom(item.type) == false) continue;
                    var size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(item.type);
                    if (size > maxStructSize) {
                        maxStructSize = size;
                    }
                }
                
                var weightsInfo = GetJobWeightsInfo(jobType);
                content.Add($"JobStaticInfo<{jobTypeFullName}>.opsWeight = {weightsInfo.weight}u;");
                content.Add($"JobStaticInfo<{jobTypeFullName}>.maxStructSize = {maxStructSize}u;");

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
                    content.Add(str);

                }

                this.cache.Add(jobType, content);
                dataList.AddRange(content);

            }
            
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
            funcBuilderFile.content = $"public unsafe partial class DebugJobs {{\n{funcBuilder}\n}}";
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
            var jobsComponents = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(TJobBase)).OrderBy(x => x.FullName).ToList();
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

                var uniqueTypes = GetJobTypesInfo(jobType);
                
                ++uniqueId;
                var structName = $"JobDebugData{uniqueId}";

                tempCacheBuilder.AppendLine($"private struct Cache{structName} {{");
                tempCacheBuilder.AppendLine($"public static readonly SharedStatic<System.IntPtr> cache = SharedStatic<System.IntPtr>.GetOrCreate<Cache{structName}>();");
                tempCacheBuilder.AppendLine($"}}");

                tempFuncBuilder.AppendLine($"{{ // {jobType.FullName}");
                tempFuncBuilder.AppendLine($"Cache{structName}.cache.Data = default;");
                tempFuncBuilder.AppendLine($"[BurstCompile]");
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

        public static System.Collections.Generic.HashSet<TypeInfo> GetMethodTypesInfo(MethodInfo root, bool traverseHierarchy = true, bool useAnalyzer = false, System.Predicate<Instruction> onInstruction = null) {
            var aspectsType = new System.Collections.Generic.HashSet<System.Type>();
            var componentsType = new System.Collections.Generic.HashSet<System.Type>();

            var parametersOverrides = new System.Collections.Generic.HashSet<TypeInfo>();
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
            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
            var visited = new System.Collections.Generic.HashSet<MethodInfo>();
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
                    if (onInstruction?.Invoke(inst) == true) continue;
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
                                uniqueTypes.Add(new TypeInfo() {
                                    type = type,
                                    op = safetyCheck.Op,
                                });
                                continueTraverse = false;
                            }
                        }
                    }

                    if (continueTraverse == true && traverseHierarchy == true && inst.Operand is System.Reflection.MethodInfo member) {
                        if ((member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(member) == true) && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                            if (member.GetMethodBody() != null) q.Enqueue(member);
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

            public int count;
            public int brCount;

        }
        
        public static NewEntInfo GetJobEntInfo(System.Type jobType) {
            var newEntMethod = typeof(Ent).GetMethod(nameof(Ent.NewEnt_INTERNAL), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var root = jobType.GetMethod("Execute");
            var visited = new System.Collections.Generic.HashSet<MethodInfo>();
            var instructions = root.GetInstructions().ToList();
            var brOpen = 0;
            var count = 0;
            var brCount = 0;
            for (int i = 0; i < instructions.Count; ++i) {
                var inst = instructions[i];
                if ((inst.OpCode == System.Reflection.Emit.OpCodes.Br ||
                     inst.OpCode == System.Reflection.Emit.OpCodes.Br_S ||
                     inst.OpCode == System.Reflection.Emit.OpCodes.Brtrue ||
                     inst.OpCode == System.Reflection.Emit.OpCodes.Brtrue_S ||
                     inst.OpCode == System.Reflection.Emit.OpCodes.Brfalse ||
                     inst.OpCode == System.Reflection.Emit.OpCodes.Brfalse_S) &&
                    ((Instruction)inst.Operand).Offset < inst.Offset) {
                    // jump to previous instruction - make it open
                    ++((Instruction)inst.Operand).loopInfo.openCount;
                    ++inst.loopInfo.closeCount;
                }
                if (inst.Operand is System.Reflection.MethodInfo member) {
                    if ((member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(member) == true) && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
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
                    if (methodInfo == newEntMethod) {
                        if (brOpen > 0) {
                            ++brCount;
                        } else {
                            ++count;
                        }
                    }
                }
            }

            return new NewEntInfo() {
                count = count,
                brCount = brCount,
            };
        }

        public struct WeightsInfo {

            public uint weight;

        }

        private struct MethodWeightInfo {

            public MethodInfo[] methods;
            public uint weight;

        }
        
        public static WeightsInfo GetJobWeightsInfo(System.Type jobType) {
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
            var visited = new System.Collections.Generic.HashSet<MethodInfo>();
            var instructions = root.GetInstructions().ToList();
            for (int i = 0; i < instructions.Count; ++i) {
                var inst = instructions[i];
                if (inst.Operand is System.Reflection.MethodInfo member) {
                    if (member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() != null) continue;
                    if ((member.GetCustomAttribute<CodeGeneratorIgnoreVisitedAttribute>() != null || visited.Add(member) == true) && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
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
                            break;
                        }
                    }
                }
            }

            return new WeightsInfo() {
                weight = weight,
            };
        }
        
        public static System.Collections.Generic.HashSet<TypeInfo> GetJobTypesInfo(System.Type jobType, System.Predicate<Instruction> onInstruction = null) {
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