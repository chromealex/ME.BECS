using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using ME.BECS.Editor.Jobs;

namespace ME.BECS.Editor.Systems {

    public class SystemDependenciesCodeGenerator : CustomCodeGenerator {

        private static uint awaitCount;
        private readonly object lockObj = new object();

        public struct Item {

            public System.Type system;
            public System.Collections.Generic.Dictionary<System.Type, Graph.Node> nodes;
            public System.Collections.Generic.List<string> content;

        }
        
        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
        }

        public override string AddPublicContent() {

            if (this.editorAssembly == false) return string.Empty;
            
            var allContent = new System.Collections.Generic.List<string>();
            var allNodes = new System.Collections.Generic.Dictionary<System.Type, Graph.Node>();
            var tempItems = new System.Collections.Generic.Dictionary<System.Type, Item>();
            
            awaitCount = 0;
            var systems = this.systems;
            foreach (var sys in systems) {

                if (sys.IsValueType == false) continue;
                if (sys.IsVisible == false) continue;

                //UnityEditor.EditorUtility.DisplayProgressBar(CodeGenerator.PROGRESS_BAR_CAPTION, sys.Name, this.awaitCount / (float)systems.Count);
                
                //UnityEngine.Debug.Log("Processing: " + sys.FullName);

                var system = sys;
                if (system.IsGenericType == true) {
                    system = system.GetGenericTypeDefinition();
                    system = EditorUtils.MakeGenericConstraintType(system);
                }
                if (tempItems.ContainsKey(system) == true) continue;
                tempItems.Add(system, new Item());
                
                JobUtils.Increment(ref awaitCount);

                var content = new System.Collections.Generic.List<string>();
                var nodes = new System.Collections.Generic.Dictionary<System.Type, Graph.Node>();
                var systemToComponents = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                System.Threading.ThreadPool.QueueUserWorkItem((state) => {

                    //UnityEngine.Debug.Log("Processing: " + system.FullName);

                    try {

                        content.Add("{");
                        content.Add($"// system: {system.FullName}");
                        content.Add("var list = new s::List<ComponentDependencyGraphInfo>();");
                        content.Add("var errors = new s::List<Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error>();");
                        content.Add($"systemDependenciesComponentsGraph.Add(typeof({EditorUtils.GetTypeName(system)}), list);");
                        content.Add($"systemDependenciesGraphErrors.Add(typeof({EditorUtils.GetTypeName(system)}), errors);");

                        {
                            var method = system.GetMethod("OnUpdate");
                            if (method != null) {
                                var deps = this.GetDeps(method);
                                if (deps.ops != null && deps.ops.Count > 0) {
                                    content.Add($"// |- OnUpdate:");
                                    foreach (var dep in deps.ops) {
                                        content.Add($"// |--- {dep.op}: {dep.type.FullName}");
                                        systemToComponents.Add(dep);
                                    }
                                }

                                if (deps.errors != null) {
                                    foreach (var dep in deps.errors) {
                                        content.Add($"// {dep.message}");
                                        content.Add(dep.AsString());
                                    }
                                }

                                var node = new Graph.Node() {
                                    system = system,
                                    errors = deps.errors,
                                    dependencies = deps.GetDependencies(),
                                    inputs = deps.GetInputs(),
                                    outputs = deps.GetOutputs(),
                                };
                                nodes.Add(system, node);
                            }
                        }

                        {
                            var method = system.GetMethod("OnAwake");
                            if (method != null) {
                                var deps = this.GetDeps(method);
                                if (deps.ops != null && deps.ops.Count > 0) {
                                    content.Add($"// |- OnAwake:");
                                    foreach (var dep in deps.ops) {
                                        content.Add($"// |--- {dep.op}: {dep.type.FullName}");
                                        systemToComponents.Add(dep);
                                    }
                                }

                                if (deps.errors != null) {
                                    foreach (var dep in deps.errors) {
                                        content.Add($"// {dep.message}");
                                        content.Add(dep.AsString());
                                    }
                                }

                                if (nodes.TryGetValue(system, out var node) == false) {
                                    node = new Graph.Node() {
                                        system = system,
                                        errors = deps.errors,
                                        dependencies = deps.GetDependencies(),
                                        inputs = deps.GetInputs(),
                                        outputs = deps.GetOutputs(),
                                    };
                                    nodes.Add(system, node);
                                } else {
                                    node.dependencies.AddRange(deps.GetDependencies());
                                    node.inputs.AddRange(deps.GetInputs());
                                    node.outputs.AddRange(deps.GetOutputs());
                                }
                            }
                        }

                        {
                            var method = system.GetMethod("OnStart");
                            if (method != null) {
                                var deps = this.GetDeps(method);
                                if (deps.ops != null && deps.ops.Count > 0) {
                                    content.Add($"// |- OnStart:");
                                    foreach (var dep in deps.ops) {
                                        content.Add($"// |--- {dep.op}: {dep.type.FullName}");
                                        systemToComponents.Add(dep);
                                    }
                                }

                                if (deps.errors != null) {
                                    foreach (var dep in deps.errors) {
                                        content.Add($"// {dep.message}");
                                        content.Add(dep.AsString());
                                    }
                                }

                                if (nodes.TryGetValue(system, out var node) == false) {
                                    node = new Graph.Node() {
                                        system = system,
                                        errors = deps.errors,
                                        dependencies = deps.GetDependencies(),
                                        inputs = deps.GetInputs(),
                                        outputs = deps.GetOutputs(),
                                    };
                                    nodes.Add(system, node);
                                } else {
                                    node.dependencies.AddRange(deps.GetDependencies());
                                    node.inputs.AddRange(deps.GetInputs());
                                    node.outputs.AddRange(deps.GetOutputs());
                                }
                            }
                        }

                        {
                            var method = system.GetMethod("OnDestroy");
                            if (method != null) {
                                var deps = this.GetDeps(method);
                                if (deps.ops != null && deps.ops.Count > 0) {
                                    content.Add($"// |- OnDestroy:");
                                    foreach (var dep in deps.ops) {
                                        content.Add($"// |--- {dep.op}: {dep.type.FullName}");
                                        systemToComponents.Add(dep);
                                    }
                                }

                                if (deps.errors != null) {
                                    foreach (var dep in deps.errors) {
                                        content.Add($"// {dep.message}");
                                        content.Add(dep.AsString());
                                    }
                                }

                                if (nodes.TryGetValue(system, out var node) == false) {
                                    node = new Graph.Node() {
                                        system = system,
                                        errors = deps.errors,
                                        dependencies = deps.GetDependencies(),
                                        inputs = deps.GetInputs(),
                                        outputs = deps.GetOutputs(),
                                    };
                                    nodes.Add(system, node);
                                } else {
                                    node.dependencies.AddRange(deps.GetDependencies());
                                    node.inputs.AddRange(deps.GetInputs());
                                    node.outputs.AddRange(deps.GetOutputs());
                                }
                            }
                        }

                        JobsEarlyInitCodeGenerator.UpdateDeps(systemToComponents);
                        foreach (var item in systemToComponents) {
                            content.Add($"list.Add(new ComponentDependencyGraphInfo() {{ type = typeof({EditorUtils.GetTypeName(item.type)}), op = {(byte)item.op} }});");
                        }

                        content.Add("}");

                        {
                            var item = new Item() {
                                system = system,
                                nodes = nodes,
                                content = content,
                            };
                            lock (this.lockObj) tempItems[system] = item;
                        }

                        //UnityEngine.Debug.Log("Processed: " + system.FullName);

                    } catch (System.Exception ex) {
                        UnityEngine.Debug.LogException(ex);
                    } finally {
                        //UnityEngine.Debug.Log("BREAK: " + system.FullName);
                        JobUtils.Decrement(ref awaitCount);
                    }

                });

            }

            while (awaitCount > 0) {
                UnityEditor.EditorUtility.DisplayProgressBar(CodeGenerator.PROGRESS_BAR_CAPTION, $"Await for Systems Generator ({(systems.Count - awaitCount)} of {systems.Count})", (systems.Count - awaitCount) / (float)systems.Count);
            }
            
            //UnityEditor.EditorUtility.DisplayProgressBar(CodeGenerator.PROGRESS_BAR_CAPTION, "Finalizing...", 1f);

            /*var sw = System.Diagnostics.Stopwatch.StartNew();
            var awaitCount = 0;
            do {
                lock (this.lockObj) {
                    awaitCount = this.awaitCount;
                }
                System.Threading.Thread.Sleep(1);
                if (sw.ElapsedMilliseconds > 100_000_000) {
                    throw new System.Exception("Iter break " + awaitCount);
                }
            } while (awaitCount > 0);*/

            var selectItems = new System.Collections.Generic.List<Item>();
            foreach (var kv in tempItems) {
                selectItems.Add(kv.Value);
            }
            var items = selectItems.OrderBy(x => x.system.FullName).ToArray();
            foreach (var item in items) {
                foreach (var kv in item.nodes) {
                    allNodes.Add(kv.Key, kv.Value);
                }
                allContent.AddRange(item.content);
            }


            var graph = new Graph(allNodes);
            allContent.Add(graph.GetInitializationString());
            
            var str = new System.Text.StringBuilder();
            str.AppendLine("private static s::Dictionary<System.Type, s::HashSet<System.Type>> systemDependenciesGraph;");
            str.AppendLine("private static s::Dictionary<System.Type, s::List<ComponentDependencyGraphInfo>> systemDependenciesComponentsGraph;");
            str.AppendLine("private static s::Dictionary<System.Type, s::List<Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error>> systemDependenciesGraphErrors;");
            str.AppendLine("public static s::List<ComponentDependencyGraphInfo> GetSystemComponentsDependencies(System.Type type) { InitializeSystemDependenciesInfo(); return systemDependenciesComponentsGraph[type]; }");
            str.AppendLine("public static s::List<Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error> GetSystemDependenciesErrors(System.Type type) { InitializeSystemDependenciesInfo(); return systemDependenciesGraphErrors[type]; }");
            str.AppendLine("public static s::HashSet<System.Type> GetSystemDependencies(System.Type type) { InitializeSystemDependenciesInfo(); return systemDependenciesGraph[type]; }");
            str.AppendLine("public static void InitializeSystemDependenciesInfo() {");
            str.AppendLine("if (systemDependenciesGraph != null) return;");
            str.AppendLine("systemDependenciesGraph = new s::Dictionary<System.Type, s::HashSet<System.Type>>();");
            str.AppendLine("systemDependenciesComponentsGraph = new s::Dictionary<System.Type, s::List<ComponentDependencyGraphInfo>>();");
            str.AppendLine("systemDependenciesGraphErrors = new s::Dictionary<System.Type, s::List<Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error>>();");
            str.Append(string.Join("\n", allContent));
            str.AppendLine("}");
            return str.ToString();
            
        }

        public class Graph {

            public class Node {

                public System.Type system;
                public System.Collections.Generic.List<System.Type> dependencies;
                public System.Collections.Generic.List<System.Type> inputs;
                public System.Collections.Generic.List<System.Type> outputs;
                public System.Collections.Generic.List<MethodInfoDependencies.Error> errors;

                public override string ToString() {
                    return "// " + this.system.FullName + "\n// |------ " + string.Join("\n// |------ ", this.dependencies.Select(x => x.ToString()).ToArray());
                }

                public bool ContainsAny(System.Collections.Generic.List<System.Type> types) {
                    for (int i = 0; i < this.outputs.Count; ++i) {
                        for (int j = 0; j < types.Count; ++j) {
                            if (this.outputs[i] == types[j]) return true;
                        }
                    }
                    return false;
                }

                public void GetInitializationString(System.Text.StringBuilder str) {
                    
                    str.Append("systemDependenciesGraph.Add(");
                    {
                        str.Append("typeof(");
                        str.Append(EditorUtils.GetTypeName(this.system, showGenericType: false));
                        str.Append(")");
                    }
                    str.Append(",");
                    if (this.dependencies.Count > 0) {
                        str.Append("new s::HashSet<System.Type>() {\ntypeof(" +
                                   string.Join("),\ntypeof(", this.dependencies.Select(x => EditorUtils.GetTypeName(x, showGenericType: false)).ToArray()) +
                                   ")\n}");
                    } else {
                        str.Append("null");
                    }
                    str.Append(");");
                    
                }

            }

            public Node[] nodes;

            public Graph(System.Collections.Generic.Dictionary<System.Type, Graph.Node> nodes) {
                this.nodes = nodes.Select(x => x.Value).ToArray();
                // find dependencies for each node
                // node has dependency if current node's inputs contained in any outputs
                foreach (var node in this.nodes) {
                    if (node.inputs.Count > 0) {
                        var arr = nodes.Where(x => x.Value != node && x.Value.ContainsAny(node.inputs) == true).Select(x => x.Value.system).ToArray();
                        node.dependencies.AddRange(arr);
                    }
                    node.dependencies = node.dependencies.OrderBy(x => x.FullName).ToList();
                }
            }

            public string GetInitializationString() {

                var str = new System.Text.StringBuilder();
                str.Append("// Nodes:\n");
                foreach (var node in this.nodes) {
                    node.GetInitializationString(str);
                    str.Append('\n');
                }
                return str.ToString();
                
            }

        }

        public struct MethodInfoDependencies {

            public struct Error : System.IEquatable<Error> {

                public enum Code {

                    MethodCallRequired,
                    MethodNotRequired,

                }

                public Code code;
                public MethodInfo callerMethodInfo;
                public string message;
                
                public string AsString() {
                    string msg = string.Empty;
                    if (this.code == Code.MethodCallRequired) {
                        msg = $"Method {this.callerMethodInfo.Name} requires a context.dependsOn.Complete() before accessing components.";
                    } else if (this.code == Code.MethodNotRequired) {
                        msg = $"Method {this.callerMethodInfo.Name} doesn't require context.dependsOn.Complete() call.";
                    }

                    return $"errors.Add(new Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error() {{ code = Systems.SystemDependenciesCodeGenerator.MethodInfoDependencies.Error.Code.{this.code}, message = \"{msg}\" }});";
                }

                public bool Equals(Error other) {
                    return this.code == other.code && Equals(this.callerMethodInfo, other.callerMethodInfo);
                }

                public override bool Equals(object obj) {
                    return obj is Error other && this.Equals(other);
                }

                public override int GetHashCode() {
                    return System.HashCode.Combine((int)this.code, this.callerMethodInfo);
                }

            }

            public System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> ops;
            public System.Collections.Generic.List<Error> errors;

            public MethodInfoDependencies(System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> types) {
                this.ops = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                this.errors = new System.Collections.Generic.List<Error>();
                foreach (var item in types) {
                    this.ops.Add(item);
                }
            }

            public void AddError(Error error) {
                this.errors.Add(error);
            }

            public System.Collections.Generic.List<System.Type> GetInputs() {
                return this.ops.Where(x => x.op == RefOp.ReadOnly || x.op == RefOp.ReadWrite).Select(x => x.type).OrderBy(x => x.FullName).ToList();
            }

            public System.Collections.Generic.List<System.Type> GetOutputs() {
                return this.ops.Where(x => (x.op == RefOp.WriteOnly || x.op == RefOp.ReadWrite) && typeof(ISystem).IsAssignableFrom(x.type) == false).Select(x => x.type).OrderBy(x => x.FullName).ToList();
            }

            public System.Collections.Generic.List<System.Type> GetDependencies() {
                return this.ops.Where(x => typeof(ISystem).IsAssignableFrom(x.type) == true).Select(x => x.type).OrderBy(x => x.FullName).ToList();
            }

        }

        private MethodInfoDependencies GetDeps(MethodInfo root) {

            if (root == null) return default;

            var errors = new System.Collections.Generic.List<MethodInfoDependencies.Error>();
            
            var completeHandleMethod = typeof(Unity.Jobs.JobHandle).GetMethod(nameof(Unity.Jobs.JobHandle.Complete));
            var getSystemMethod = typeof(SystemsWorldExt).GetMethod(nameof(SystemsWorldExt.GetSystemPtr));
            var withMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.With));
            var withAnyMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.WithAny));
            var withoutMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.Without));
            var withAspectMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.WithAspect));
            var asReadonlyMethod = typeof(QueryBuilder).GetMethod(nameof(QueryBuilder.AsReadonly));
            
            var uniqueTypes = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var methodCallRequired = false;
            var hasDirectComponentChange = false;
            var hasCompleteHandle = false;
            var hasInterestInstructions = false;
            var visited = new System.Collections.Generic.HashSet<System.Reflection.MethodInfo>();
            while (q.Count > 0) {
                var body = q.Dequeue();
                var instructions = body.GetInstructions();
                var isReadonly = false;
                foreach (var inst in instructions) {
                    var continueTraverse = true;
                    if (inst.Operand is MethodInfo methodInfo) {
                        if (hasCompleteHandle == false && hasInterestInstructions == false && body == root) {
                            // search for Complete
                            if (IsMethod(methodInfo, completeHandleMethod) == true) {
                                hasCompleteHandle = true;
                            }
                        }
                        if (IsMethod(methodInfo, getSystemMethod) == true) {
                            hasInterestInstructions = true;
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadWrite,
                            });
                            continueTraverse = false;
                        } else if (IsMethod(methodInfo, withMethod) == true) {
                            hasInterestInstructions = true;
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                            continueTraverse = false;
                        } else if (IsMethod(methodInfo, withAnyMethod) == true) {
                            hasInterestInstructions = true;
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                            continueTraverse = false;
                        } else if (IsMethod(methodInfo, withoutMethod) == true) {
                            hasInterestInstructions = true;
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                            continueTraverse = false;
                        } else if (IsMethod(methodInfo, withAspectMethod) == true) {
                            hasInterestInstructions = true;
                            var aspect = methodInfo.GetGenericArguments()[0];
                            var fields = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var field in fields) {
                                if (field.GetCustomAttribute<QueryWithAttribute>() != null) {
                                    var type = field.FieldType.GetGenericArguments()[0];
                                    uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                        type = type,
                                        op = RefOp.ReadOnly,
                                    });
                                    continueTraverse = false;
                                }
                            }
                        } else if (IsMethod(methodInfo, asReadonlyMethod) == true) {
                            hasInterestInstructions = true;
                            isReadonly = true;
                            continueTraverse = false;
                        } else if (methodInfo.Name == "Schedule" && methodInfo.IsGenericMethod == true) {
                            hasInterestInstructions = true;
                            if (methodInfo.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                                var jobType = methodInfo.GetGenericArguments()[0];
                                var info = JobsEarlyInitCodeGenerator.GetJobTypesInfo(jobType);
                                foreach (var typeInfo in info) {
                                    uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                        type = typeInfo.type,
                                        op = isReadonly == true && typeInfo.isArg == true ? RefOp.ReadOnly : typeInfo.op,
                                    });
                                    //continueTraverse = false;
                                }

                                isReadonly = false;
                            }
                        } else {
                            if (methodInfo.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null && methodInfo.GetMethodBody() != null) {
                                var info = JobsEarlyInitCodeGenerator.GetMethodTypesInfo(methodInfo, false);
                                if (info.Count > 0) {
                                    hasInterestInstructions = true;
                                    hasDirectComponentChange = true;
                                    // Check if complete method exists
                                    if (hasCompleteHandle == false) {
                                        methodCallRequired = true;
                                    }
                                }
                                foreach (var typeInfo in info) {
                                    uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                        type = typeInfo.type,
                                        op = typeInfo.op,
                                    });
                                    //continueTraverse = false;
                                }
                            }
                        }
                    }
                    
                    if (continueTraverse == true && inst.Operand is System.Reflection.MethodInfo member) {
                        if (visited.Add(member) == true && member.GetCustomAttribute<CodeGeneratorIgnoreAttribute>() == null) {
                            if (member.GetMethodBody() != null) {
                                q.Enqueue(member);
                            }
                        }
                    }
                }
            }

            if (methodCallRequired == true) {
                // Add error
                var err = new MethodInfoDependencies.Error() {
                    callerMethodInfo = root,
                    code = MethodInfoDependencies.Error.Code.MethodCallRequired,
                };
                errors.Add(err);
            }

            if (hasDirectComponentChange == false && hasCompleteHandle == true) {
                // Add error
                var err = new MethodInfoDependencies.Error() {
                    callerMethodInfo = root,
                    code = MethodInfoDependencies.Error.Code.MethodNotRequired,
                };
                errors.Add(err);
            }
            
            JobsEarlyInitCodeGenerator.UpdateDeps(uniqueTypes);
            var deps = new MethodInfoDependencies(uniqueTypes);
            foreach (var err in errors) deps.AddError(err);
            return deps;
        }

        private static bool IsMethod(MethodInfo method1, MethodInfo method2) {
            return method1.MetadataToken == method2.MetadataToken && method1.Module == method2.Module && method1.DeclaringType == method2.DeclaringType;
        }

    }

}