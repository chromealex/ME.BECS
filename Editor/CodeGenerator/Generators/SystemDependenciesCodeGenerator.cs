using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using ME.BECS.Editor.Jobs;

namespace ME.BECS.Editor.Systems {

    public class SystemDependenciesCodeGenerator : CustomCodeGenerator {

        public override void AddInitialization(System.Collections.Generic.List<string> dataList, System.Collections.Generic.List<System.Type> references) {
            
        }

        public override System.Collections.Generic.List<CodeGenerator.MethodDefinition> AddMethods(System.Collections.Generic.List<System.Type> references) {

            if (this.editorAssembly == false) return new System.Collections.Generic.List<CodeGenerator.MethodDefinition>();
            
            var content = new System.Collections.Generic.List<string>();
            content.Add("/*");
            
            var nodes = new System.Collections.Generic.List<Graph.Node>();
            var systems = UnityEditor.TypeCache.GetTypesDerivedFrom(typeof(ISystem)).OrderBy(x => x.FullName).ToArray();
            foreach (var system in systems) {

                if (system.IsValueType == false) continue;
                if (system.IsVisible == false) continue;

                content.Add($"// system: {system.FullName}");

                /*var awakeMethod = system.GetMethod("OnAwake");
                if (awakeMethod != null) {
                    var deps = this.GetDeps(awakeMethod);
                    if (deps.ops != null) {
                        foreach (var dep in deps.ops) {
                            content.Add($"//    {dep.op}: {dep.type.FullName}");
                        }
                    }

                    var node = new Graph.Node() {
                        system = system,
                        dependencies = deps.GetDependencies(),
                        inputs = deps.GetInputs(),
                        outputs = deps.GetOutputs(),
                    };
                    nodes.Add(node);

                }*/

                var updateMethod = system.GetMethod("OnUpdate");
                if (updateMethod != null) {
                    var deps = this.GetDeps(updateMethod);
                    if (deps.ops != null) {
                        foreach (var dep in deps.ops) {
                            content.Add($"//    {dep.op}: {dep.type.FullName}");
                        }
                    }

                    var node = new Graph.Node() {
                        system = system,
                        dependencies = deps.GetDependencies(),
                        inputs = deps.GetInputs(),
                        outputs = deps.GetOutputs(),
                    };
                    nodes.Add(node);

                }

            }

            var graph = new Graph(nodes);
            content.Add(graph.ToString());
            
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

        public class Graph {

            public class Node {

                public System.Type system;
                public System.Collections.Generic.List<System.Type> dependencies;
                public System.Type[] inputs;
                public System.Type[] outputs;

                public override string ToString() {
                    return this.system.Name + "\n|------ " + string.Join("\n|------ ", this.dependencies.Select(x => x.ToString()).ToArray());//"Inputs: " + string.Join(", ", this.inputs.Select(x => x.ToString()).ToArray()) + ", Outputs: " + string.Join(", ", this.outputs.Select(x => x.ToString()).ToArray());
                }

                public bool ContainsAny(System.Type[] types) {
                    for (int i = 0; i < this.outputs.Length; ++i) {
                        for (int j = 0; j < types.Length; ++j) {
                            if (this.outputs[i] == types[j]) return true;
                        }
                    }
                    return false;
                }

            }

            public Node[] nodes;

            public Graph(System.Collections.Generic.List<Graph.Node> nodes) {
                this.nodes = nodes.ToArray();
                // find dependencies for each node
                // node has dependency if current node's inputs contained in any outputs
                foreach (var node in nodes) {
                    if (node.inputs.Length > 0) {
                        var arr = nodes.Where(x => x != node && x.ContainsAny(node.inputs) == true).Select(x => x.system).ToArray();
                        node.dependencies.AddRange(arr);
                    }
                }
            }

            public override string ToString() {

                var str = new System.Text.StringBuilder();
                str.Append("Nodes:\n");
                foreach (var node in this.nodes) {
                    str.Append(node.ToString());
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

            public System.Type[] GetInputs() {
                return this.ops.Where(x => x.op == RefOp.ReadOnly || x.op == RefOp.ReadWrite).Select(x => x.type).ToArray();
            }

            public System.Type[] GetOutputs() {
                return this.ops.Where(x => (x.op == RefOp.WriteOnly || x.op == RefOp.ReadWrite) && typeof(ISystem).IsAssignableFrom(x.type) == false).Select(x => x.type).ToArray();
            }

            public System.Collections.Generic.List<System.Type> GetDependencies() {
                return this.ops.Where(x => typeof(ISystem).IsAssignableFrom(x.type) == true).Select(x => x.type).ToList();
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