using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using ME.BECS.Editor.Jobs;

namespace ME.BECS.Editor.Systems {

    public class SystemDependenciesCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
        }

        public override string AddPublicContent() {

            if (this.editorAssembly == false) return string.Empty;
            
            var content = new System.Collections.Generic.List<string>();
            
            var nodes = new System.Collections.Generic.Dictionary<System.Type, Graph.Node>();
            var systems = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem)).OrderBy(x => x.FullName).ToArray();
            foreach (var system in systems) {

                if (system.IsValueType == false) continue;
                if (system.IsVisible == false) continue;

                content.Add($"// system: {system.FullName}");

                {
                    var method = system.GetMethod("OnUpdate");
                    if (method != null) {
                        var deps = this.GetDeps(method);
                        if (deps.ops != null && deps.ops.Count > 0) {
                            content.Add($"// |- OnUpdate:");
                            foreach (var dep in deps.ops) {
                                content.Add($"// |--- {dep.op}: {dep.type.FullName}");
                            }
                        }
                        var node = new Graph.Node() {
                            system = system,
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
                            }
                        }

                        if (nodes.TryGetValue(system, out var node) == false) {
                            node = new Graph.Node() {
                                system = system,
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
                            }
                        }

                        if (nodes.TryGetValue(system, out var node) == false) {
                            node = new Graph.Node() {
                                system = system,
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
                            }
                        }

                        if (nodes.TryGetValue(system, out var node) == false) {
                            node = new Graph.Node() {
                                system = system,
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

            }

            var graph = new Graph(nodes);
            content.Add(graph.GetInitializationString());
            
            var str = new System.Text.StringBuilder();
            str.AppendLine("private static s::Dictionary<System.Type, s::HashSet<System.Type>> systemDependenciesGraph;");
            str.AppendLine("public static s::HashSet<System.Type> GetSystemDependencies(System.Type type) => systemDependenciesGraph[type];");
            str.AppendLine("public static void InitializeSystemDependenciesInfo() {");
            str.AppendLine("if (systemDependenciesGraph != null) return;");
            str.AppendLine("systemDependenciesGraph = new s::Dictionary<System.Type, s::HashSet<System.Type>>();");
            str.Append(string.Join("\n", content));
            str.AppendLine("}");
            return str.ToString();
            
        }

        public class Graph {

            public class Node {

                public System.Type system;
                public System.Collections.Generic.List<System.Type> dependencies;
                public System.Collections.Generic.List<System.Type> inputs;
                public System.Collections.Generic.List<System.Type> outputs;

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
                        str.Append(EditorUtils.GetTypeName(this.system));
                        str.Append(")");
                    }
                    str.Append(",");
                    if (this.dependencies.Count > 0) {
                        str.Append("new s::HashSet<System.Type>() {\ntypeof(" +
                                   string.Join("),\ntypeof(", this.dependencies.Select(EditorUtils.GetTypeName).ToArray()) +
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

            public System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> ops;

            public MethodInfoDependencies(System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo> types) {
                this.ops = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
                foreach (var item in types) {
                    this.ops.Add(item);
                }
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

            var getSystemMethod = typeof(SystemsWorldExt).GetMethod(nameof(SystemsWorldExt.GetSystemPtr));
            var withMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.With));
            var withAnyMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.WithAny));
            var withoutMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.Without));
            var withAspectMethod = typeof(ArchetypeQueries.QueryCompose).GetMethod(nameof(ArchetypeQueries.QueryCompose.WithAspect));
            
            var uniqueTypes = new System.Collections.Generic.HashSet<JobsEarlyInitCodeGenerator.TypeInfo>();
            var q = new System.Collections.Generic.Queue<System.Reflection.MethodInfo>();
            q.Enqueue(root);
            var visited = new System.Collections.Generic.HashSet<System.Reflection.MethodInfo>();
            while (q.Count > 0) {
                var body = q.Dequeue();
                var instructions = body.GetInstructions();
                foreach (var inst in instructions) {
                    if (inst.Operand is MethodInfo methodInfo) {
                        if (IsMethod(methodInfo, getSystemMethod) == true) {
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadWrite,
                            });
                        } else if (IsMethod(methodInfo, withMethod) == true) {
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                        } else if (IsMethod(methodInfo, withAnyMethod) == true) {
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                        } else if (IsMethod(methodInfo, withoutMethod) == true) {
                            uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                type = methodInfo.GetGenericArguments()[0],
                                op = RefOp.ReadOnly,
                            });
                        } else if (IsMethod(methodInfo, withAspectMethod) == true) {
                            var aspect = methodInfo.GetGenericArguments()[0];
                            var fields = aspect.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var field in fields) {
                                if (field.GetCustomAttribute<QueryWithAttribute>() != null) {
                                    var type = field.FieldType.GetGenericArguments()[0];
                                    uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                        type = type,
                                        op = RefOp.ReadOnly,
                                    });
                                }
                            }
                        } else if (methodInfo.Name == "Schedule" && methodInfo.IsGenericMethod == true) {
                            var jobType = methodInfo.GetGenericArguments()[0];
                            var info = JobsEarlyInitCodeGenerator.GetJobTypesInfo(jobType);
                            foreach (var typeInfo in info) {
                                uniqueTypes.Add(new JobsEarlyInitCodeGenerator.TypeInfo() {
                                    type = typeInfo.type,
                                    op = typeInfo.op,
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
            
            JobsEarlyInitCodeGenerator.UpdateDeps(uniqueTypes);
            var deps = new MethodInfoDependencies(uniqueTypes);
            return deps;
        }

        private static bool IsMethod(MethodInfo method1, MethodInfo method2) {
            return method1.MetadataToken == method2.MetadataToken && method1.Module == method2.Module && method1.DeclaringType == method2.DeclaringType;
        }

    }

}