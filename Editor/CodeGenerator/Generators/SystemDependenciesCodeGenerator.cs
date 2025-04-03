using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;

namespace ME.BECS.Editor.Aspects {

    public class SystemDependenciesCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            if (this.editorAssembly == false) return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            
            var content = new System.Collections.Generic.List<string>();
            content.Add("/*");
            
            var systems = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem)).OrderBy(x => x.FullName).ToArray();
            foreach (var system in systems) {

                if (system.IsValueType == false) continue;
                if (system.IsVisible == false) continue;

                content.Add($"// system: {system.FullName}");

                var updateMethod = system.GetMethod("OnUpdate");
                if (updateMethod != null) {
                    //try {
                        var deps = this.GetDeps(updateMethod);
                        if (deps.ops != null) {
                            foreach (var dep in deps.ops) {
                                content.Add($"//    {dep.op}: {dep.type.FullName}");
                            }
                        }
                    //} catch (System.Exception e) {
                    //    UnityEngine.Debug.LogError(system + ": " + e.Message);
                    //    content.Add($"// ERROR: {e.Message}");
                    //}
                }

            }
            
            content.Add("*/");
            var def = new CodeGenerator.MethodDefinition() {
                methodName = "InitializeSystemDependenciesInfo",
                type = "World",
                registerMethodName = "RegisterCallback",
                definition = "ref World world",
                content = string.Join("\n", content),
            };
            return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>() { def };

        }

        public struct MethodInfoDependencies {

            public System.Collections.Generic.HashSet<TypeInfo> ops;

            public void Add(System.Type componentType, RefOp op) {
                
                if (this.ops == null) this.ops = new System.Collections.Generic.HashSet<TypeInfo>();
                {
                    var roOp = new TypeInfo() {
                        type = componentType,
                        op = RefOp.ReadOnly,
                    };
                    if (this.ops.Contains(roOp) == true) {
                        if (op == RefOp.WriteOnly) {
                            this.ops.Remove(roOp);
                            this.ops.Add(new TypeInfo() {
                                type = componentType,
                                op = op,
                            });
                        } else if (op == RefOp.ReadWrite) {
                            this.ops.Remove(roOp);
                            this.ops.Add(new TypeInfo() {
                                type = componentType,
                                op = op,
                            });
                        }
                    } else {
                        this.ops.Add(new TypeInfo() {
                            type = componentType,
                            op = op,
                        });
                    }
                }
                {
                    var roOp = new TypeInfo() {
                        type = componentType,
                        op = RefOp.WriteOnly,
                    };
                    if (this.ops.Contains(roOp) == true) {
                        if (op == RefOp.ReadWrite) {
                            this.ops.Remove(roOp);
                            this.ops.Add(new TypeInfo() {
                                type = componentType,
                                op = op,
                            });
                        }
                    } else {
                        this.ops.Add(new TypeInfo() {
                            type = componentType,
                            op = op,
                        });
                    }
                }

            }

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
        
        private MethodInfoDependencies GetDeps(MethodInfo root) {

            if (root == null) return default;
            
            var withMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod("With");
            var withAnyMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod("WithAny");
            var withoutMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod("Without");
            var withAspectMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod("WithAspect");
            
            var deps = new MethodInfoDependencies();
            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var uniqueTypes = new System.Collections.Generic.HashSet<TypeInfo>();
            var visited = new System.Collections.Generic.HashSet<System.Reflection.MethodInfo>();
            while (q.Count > 0) {
                var body = q.Dequeue();
                var instructions = body.GetInstructions();
                foreach (var inst in instructions) {
                    if (inst.Operand is MethodInfo methodInfo) {
                        if (IsMethod(methodInfo, withMethod) == true) {
                            deps.Add(methodInfo.GetGenericArguments()[0], RefOp.ReadOnly);
                        } else if (IsMethod(methodInfo, withAnyMethod) == true) {
                            deps.Add(methodInfo.GetGenericArguments()[0], RefOp.ReadOnly);
                        } else if (IsMethod(methodInfo, withoutMethod) == true) {
                            deps.Add(methodInfo.GetGenericArguments()[0], RefOp.ReadOnly);
                        } else if (IsMethod(methodInfo, withAspectMethod) == true) {
                            var aspect = methodInfo.GetGenericArguments()[0];
                            var fields = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var field in fields) {
                                if (field.GetCustomAttribute<QueryWithAttribute>() != null) {
                                    var type = field.FieldType.GetGenericArguments()[0];
                                    deps.Add(type, RefOp.ReadOnly);
                                }
                            }
                        } else if (methodInfo.Name == "Schedule" && methodInfo.IsGenericMethod == true) {
                            var jobType = methodInfo.GetGenericArguments()[0];
                            var info = ME.BECS.Editor.Jobs.JobsEarlyInitCodeGenerator.GetJobTypesInfo(jobType);
                            foreach (var typeInfo in info) {
                                deps.Add(typeInfo.type, typeInfo.op);
                            }
                        }
                    }
                    /*{
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
                    }*/

                    if (inst.Operand is System.Reflection.MethodInfo member) {
                        if (visited.Add(member) == true) {
                            if (member.GetMethodBody() != null) q.Enqueue(member);
                        }
                    }
                }
            }

            return deps;
        }

        private static bool IsMethod(MethodInfo method1, MethodInfo method2) {
            return method1.MetadataToken == method2.MetadataToken && method1.Module == method2.Module && method1.DeclaringType == method2.DeclaringType;
        }

    }

}