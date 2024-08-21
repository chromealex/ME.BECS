using System.Linq;
using System.Reflection;
using Unity.Collections;

namespace ME.BECS.Editor.Systems {

    using scg = System.Collections.Generic;
    using ME.BECS.FeaturesGraph;
    
    public class SystemsCodeGenerator : CustomCodeGenerator {

        private void AddMethod<T>(SystemsGraph graph, string methodName, out scg::List<string> content, out scg::List<string> innerMethods) where T : class {
            //var name = System.Text.RegularExpressions.Regex.Replace(graph.name, @"(\s+|@|&|'|\(|\)|<|>|#|-)", "_");
            content = new scg::List<string>();
            content.Add($"[AOT.MonoPInvokeCallback(typeof(SystemsStatic.{methodName}))]");
            content.Add($"public static void Graph{methodName}_{GetId(graph)}_{this.GetType().Name}(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {{");
            //content.Add("/*");
            {
                content.Add("// " + graph.name);
                var startNodeIndex = 0;
                //UnityEngine.Debug.LogWarning("GRAPH: " + graph.name);
                innerMethods = new System.Collections.Generic.List<string>();
                innerMethods = AddGraph<T>(this, startNodeIndex, methodName, content, graph);
            }
            //content.Add("*/");
            content.Add("}");
        }

        public override string AddPublicContent() {

            var content = new scg::List<string>();
            if (this.editorAssembly == false) {
                //content.Add("/*");
                var graphs = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
                foreach (var guid in graphs) {

                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(path);
                    var id = GetId(graph);
                    //var name = System.Text.RegularExpressions.Regex.Replace(graph.name, @"(\s+|@|&|'|\(|\)|<|>|#|-)", "_");
                    content.Add($"private static NativeArray<System.IntPtr> graphNodes{GetId(graph)}_{this.GetType().Name};");

                    { // initialize method
                        content.Add($"[AOT.MonoPInvokeCallback(typeof(SystemsStatic.InitializeGraph))]");
                        content.Add($"public static void GraphInitialize_{GetId(graph)}_{this.GetType().Name}() {{"); 
                        {
                            content.Add($"// {graph.name}");
                            content.Add("var allocator = (AllocatorManager.AllocatorHandle)Constants.ALLOCATOR_DOMAIN;");
                            InitializeGraph(this, content, graph);
                        }
                        content.Add("}");
                    }
                    {
                        this.AddMethod<IAwake>(graph, "OnAwake", out var caller, out var innerMethods);
                        content.AddRange(innerMethods);
                        content.AddRange(caller);
                    }
                    {
                        this.AddMethod<IUpdate>(graph, "OnUpdate", out var caller, out var innerMethods);
                        content.AddRange(innerMethods);
                        content.AddRange(caller);
                    }
                    {
                        this.AddMethod<IDestroy>(graph, "OnDestroy", out var caller, out var innerMethods);
                        content.AddRange(innerMethods);
                        content.AddRange(caller);
                    }
                    {
                        this.AddMethod<IDrawGizmos>(graph, "OnDrawGizmos", out var caller, out var innerMethods);
                        content.AddRange(innerMethods);
                        content.AddRange(caller);
                    }
                    {
                        content.Add($"[AOT.MonoPInvokeCallback(typeof(SystemsStatic.GetSystem))]");
                        content.Add($"public static void GraphGetSystem_{id}_{this.GetType().Name}(int index, out void* ptr) {{");
                        content.Add($"ptr = (void*)graphNodes{id}_{this.GetType().Name}[index];");
                        content.Add("}");
                    }

                }
                //content.Add("*/");

                // initialize callbacks
                content.Add("[UnityEngine.Scripting.PreserveAttribute]");
                content.Add("[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]");
                content.Add("public static void SystemsLoad() {");
                foreach (var guid in graphs) {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(path);
                    var id = GetId(graph);
                    var graphId = graph.GetId();
                    content.Add($"// Graph: {graph.name}");
                    content.Add("{");
                    content.Add($"SystemsStatic.RegisterMethod(GraphInitialize_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterAwakeMethod(GraphOnAwake_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterUpdateMethod(GraphOnUpdate_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterDrawGizmosMethod(GraphOnDrawGizmos_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterDestroyMethod(GraphOnDestroy_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterGetSystemMethod(GraphGetSystem_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add("}");
                }
                content.Add("}");
            }
            
            var newContent = string.Join("\n", content);
            return FormatCode(newContent.Split("\n"));
            
        }

        private static string FormatCode(string[] content) {

            var result = new System.Text.StringBuilder(content.Length * 256);
            var indent = 2;
            for (int i = 0; i < content.Length; ++i) {
                var line = content[i];
                var open = line.Contains('{');
                var close = line.Contains('}');
                if (close == true) --indent;
                for (int j = 0; j < indent; ++j) {
                    result.Append(' ', 4);
                }
                result.Append(line);
                result.Append('\n');
                if (open == true) ++indent;
            }
            
            return result.ToString();

        }

        private struct GraphLink {

            public ME.BECS.Extensions.GraphProcessor.BaseGraph graph;
            public int index;

            public override string ToString() {
                return $"{GetId(this.graph)}_{this.index}";
            }

        }

        public static scg::List<string> AddGraph<T>(CustomCodeGenerator generator, int startNodeIndex, string method, scg::List<string> content, SystemsGraph graph) where T : class {

            static void AddNodesArrDefinition(CustomCodeGenerator generator, scg::List<string> content, SystemsGraph graph, scg::List<string> arrMethodDef) {

                content.Add($"var localNodes_{GetId(graph)} = (System.IntPtr*)graphNodes{GetId(graph)}_{generator.GetType().Name}.GetUnsafePtr();");
                arrMethodDef.Add($"localNodes_{GetId(graph)}");
                
                foreach (var node in graph.nodes) {
                    if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        AddNodesArrDefinition(generator, content, graphNode.graphValue, arrMethodDef);
                    }
                }
            }

            var arrMethodDef = new scg::List<string>();
            AddNodesArrDefinition(generator, content, graph, arrMethodDef);

            var insertIndex = content.Count;

            var scheme = new scg::List<string>();
            var innerMethods = new scg::List<string>();
            {
                var startNode = graph.GetStartNode(startNodeIndex);
                if (startNode != null) {

                    var containers = new scg::List<string>();
                    var collectedDeps = new scg::HashSet<string>();
                    string lastDependency = string.Empty;
                    {
                        var customInputDeps = new scg::Dictionary<ME.BECS.Extensions.GraphProcessor.BaseNode, ME.BECS.Extensions.GraphProcessor.BaseNode>();
                        var printedDependencies = new scg::HashSet<string>();
                        printedDependencies.Add($"dep{GetIndex(startNode, startNode.graph)}");
                        var q = new scg::Queue<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                        q.Enqueue(startNode);
                        var containsInQueue = new scg::HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                        containsInQueue.Add(startNode);
                        var isOpened = false;
                        var isInBurst = false;
                        var prevOpenIndex = -1;
                        var prevOpenIndexDeps = 0;
                        var maxIter = 10_000;
                        var methodContent = content;

                        void AddApply(ME.BECS.Extensions.GraphProcessor.BaseNode node, GraphLink index, ref string schemeDependsOn) {

                            if (node.syncPoint == false) return;
                            if (customInputDeps.TryGetValue(node, out var parentNode) == true) {
                                while (parentNode != null) {
                                    if (parentNode.syncPoint == false) return;
                                    customInputDeps.TryGetValue(parentNode, out var parentNodeInner);
                                    parentNode = parentNodeInner;
                                }
                            }

                            var resDep = $"dep{index.ToString()}";
                            scheme.Add($" * {Align("Batches.Apply", 32)} :  {Align($"{schemeDependsOn} => {resDep}", 16 + 32 + 4, true)} [  SYNC   ]");
                            //methodContent.Add($"{resDep} = Batches.Apply({resDep}, in world);");
                            schemeDependsOn = resDep;
                            //methodContent.Add($"dep{index.ToString()} = Batches.Apply(localContext{index.ToString()}.dependsOn, world.state);");
                        }
                        
                        while (q.Count > 0) {

                            if (--maxIter == 0) {
                                UnityEngine.Debug.LogError("max iter");
                                break;
                            }

                            var n = q.Dequeue();
                            containsInQueue.Remove(n);

                            //methodContent.Add($"// {n.name} ({n.graph.name})");

                            var index = new GraphLink() {
                                graph = n.graph,
                                index = n.graph.GetNodeIndex(n),
                            };

                            var depNode = n;
                            if (n is ME.BECS.FeaturesGraph.Nodes.StartNode customStartNodeInner) {
                                if (customInputDeps.TryGetValue(customStartNodeInner, out var parentNode) == true) {
                                    depNode = parentNode;
                                }
                            }

                            var dependsOn = GetDeps(startNodeIndex, depNode, out var schemeDependsOn, out var deps, collectedDeps);

                            // we must already print all deps
                            var allPrinted = true;
                            foreach (var dep in deps) {
                                if (printedDependencies.Contains(dep) == false) {
                                    //methodContent.Add($"// FAILED DEP {dep}");
                                    allPrinted = false;
                                    break;
                                }
                            }

                            if (allPrinted == false) {

                                if (containsInQueue.Contains(n) == false) {
                                    q.Enqueue(n);
                                    containsInQueue.Add(n);
                                }

                                continue;

                            }

                            if (n is ME.BECS.FeaturesGraph.Nodes.ExitNode exitNode) {

                                if (customInputDeps.TryGetValue(exitNode, out var parentNode) == true) {

                                    var gr = (SystemsGraph)exitNode.graph;
                                    var graphStartNode = gr.GetStartNode(startNodeIndex);
                                    var graphEndNode = gr.GetEndNode();
                                    customInputDeps.Remove(graphStartNode);
                                    
                                    customInputDeps.Remove(graphEndNode);

                                    n = parentNode;
                                    var dep = GetIndex(n, n.graph);
                                    printedDependencies.Add($"dep{dep}");
                                    var dependsOnExit = GetDeps(startNodeIndex, exitNode, out var schemeDependsOnExit, out var depsExit, collectedDeps);
                                    scheme.Add($" * EXIT dep{dep} = {schemeDependsOnExit};");
                                    methodContent.Add($"dep{dep} = {dependsOnExit};");
                                    collectedDeps.Add($"dep{dep}");

                                } else {

                                    var dependsOnExit = GetDeps(startNodeIndex, exitNode, out var schemeDependsOnExit, out var depsExit, collectedDeps);
                                    scheme.Add($" * EXIT dependsOn = {schemeDependsOnExit};");
                                    methodContent.Add($"dependsOn = {dependsOnExit};");
                                    //collectedDeps.Add(dependsOnExit);
                                    lastDependency = dependsOnExit;

                                }

                            } else if (
                                n is ME.BECS.FeaturesGraph.Nodes.SystemNode ||
                                n is ME.BECS.Extensions.GraphProcessor.RelayNode ||
                                n is ME.BECS.FeaturesGraph.Nodes.StartNode) {

                                if (n is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null &&
                                    n.enabled == true && n.IsGroupEnabled() == true) {

                                    var notUsedDescr = " - Empty Node";
                                    var hasMethod = HasMethod<T>(n);
                                    if (hasMethod == false ||
                                        n.enabled == false || n.IsGroupEnabled() == false) {

                                        methodContent.Add($"dep{index.ToString()} = {dependsOn};");
                                        collectedDeps.Add($"dep{index.ToString()}");

                                        if (hasMethod == false) {
                                            notUsedDescr = $" - Method {typeof(T)} was not found. Node skipped.";
                                        }

                                    } else {

                                        notUsedDescr = string.Empty;

                                        var isBursted = IsBursted(generator, n, method);
                                        if (isOpened == false || isInBurst != isBursted) {
                                            if (isOpened == true) {
                                                // close
                                                if (isInBurst == true) {
                                                    methodContent.Add("// BURST ENABLE CLOSE");
                                                } else {
                                                    methodContent.Add("// BURST DISABLE CLOSE");
                                                }

                                                if (collectedDeps.Count - prevOpenIndexDeps > 0) {
                                                    var data = $", {GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}) {{";
                                                    methodContent.Insert(prevOpenIndex, data);
                                                    var dataDef = $", {GetMethodDeps("ref", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}";
                                                    containers.Add(dataDef);
                                                }

                                                methodContent.Add("}"); // close previous
                                                containers.Add(");\n");

                                                //innerMethods.AddRange(methodContent);

                                            }

                                            methodContent = innerMethods;
                                            //methodContent.Clear();
                                            // open
                                            if (isBursted == true) {
                                                methodContent.Add("// BURST ENABLE OPEN");
                                            } else {
                                                methodContent.Add("// BURST DISABLE OPEN");
                                            }

                                            var methodDeps = GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, 0, collectedDeps.Count);
                                            var methodDepsDef = GetMethodDeps("ref", collectedDeps, 0, collectedDeps.Count);
                                            var methodData = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(float dt, in World world, ref Unity.Jobs.JobHandle dependsOn, {GetMethodDeps("System.IntPtr*", arrMethodDef, 0, arrMethodDef.Count)}, {methodDeps}";
                                            methodContent.Add($"{(isBursted == true ? "[BURST] " : string.Empty)}private static void {methodData}"); // open next
                                            var methodDef = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(dt, in world, ref dependsOn, {GetMethodDeps("", arrMethodDef, 0, arrMethodDef.Count)}, {methodDepsDef}";
                                            containers.Add(methodDef);
                                            prevOpenIndex = methodContent.Count;
                                            prevOpenIndexDeps = collectedDeps.Count;

                                            isOpened = true;
                                            isInBurst = isBursted;
                                        }

                                        methodContent.Add("{");
                                        methodContent.Add($"var localContext{index.ToString()} = SystemContext.Create(dt, in world, {dependsOn});");
                                        methodContent.Add($"(({GetTypeName(systemNode.system.GetType())}*)(localNodes_{GetId(index.graph)}[{index.index}]))->{method}(ref localContext{index.ToString()});");
                                        methodContent.Add($"dep{index.ToString()} = localContext{index.ToString()}.dependsOn;");
                                        AddApply(systemNode, index, ref schemeDependsOn);
                                        methodContent.Add("}");
                                        collectedDeps.Add($"dep{index.ToString()}");

                                    }

                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, true)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}]{notUsedDescr}");

                                } else {

                                    methodContent.Add($"dep{index.ToString()} = {dependsOn};");
                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, true)} [ SKIPPED ]");
                                    collectedDeps.Add($"dep{index.ToString()}");

                                }

                                {

                                    //scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, true)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}]");

                                    foreach (var dep in deps) printedDependencies.Add(dep);
                                    printedDependencies.Add($"dep{index.ToString()}");
                                }

                            } else if (n is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode && graphNode.graphValue != null) {

                                if (n.enabled == true && n.IsGroupEnabled() == true) {

                                    var graphStartNode = graphNode.graphValue.GetStartNode(startNodeIndex);
                                    var graphEndNode = graphNode.graphValue.GetEndNode();
                                    //methodContent.Add("//    START GRAPH: " + graphNode.graphValue.name + ", node: " + graphStartNode.name);
                                    //UnityEngine.Debug.Log(graphStartNode.graph + " :: " + graphStartNode.graph.GetNodeIndex(graphStartNode));
                                    customInputDeps.Add(graphEndNode, graphNode);
                                    {
                                        customInputDeps.Add(graphStartNode, graphNode);
                                        q.Enqueue(graphStartNode);
                                        containsInQueue.Add(graphStartNode);
                                    }

                                } else {

                                    methodContent.Add($"dep{index.ToString()} = {dependsOn};");
                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, true)} [ SKIPPED ]");
                                    collectedDeps.Add($"dep{index.ToString()}");
                                    printedDependencies.Add($"dep{index.ToString()}");

                                }

                            }
                            
                            foreach (var port in n.outputPorts) {
                                var edges = port.GetEdges();
                                foreach (var edge in edges) {
                                    if (containsInQueue.Contains(edge.inputNode) == false) {
                                        q.Enqueue(edge.inputNode);
                                        containsInQueue.Add(edge.inputNode);
                                        if (customInputDeps.TryGetValue(n, out var parentNode) == true) {
                                            customInputDeps.TryAdd(edge.inputNode, parentNode);
                                        }
                                    }
                                }
                            }

                        }

                        if (isOpened == true) {
                            // burst method is opened - close
                            if (isInBurst == true) {
                                content.Add("// BURST ENABLE CLOSE");
                            } else {
                                content.Add("// BURST DISABLE CLOSE");
                            }

                            if (collectedDeps.Count - prevOpenIndexDeps > 0) {
                                var data = $", {GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}) {{";
                                innerMethods.Insert(prevOpenIndex, data);
                                var dataDef = $", {GetMethodDeps("ref", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}";
                                containers.Add(dataDef);
                            }

                            innerMethods.Add("}"); // close last
                            containers.Add(");\n");
                        }

                    }

                    foreach (var dep in collectedDeps) {
                        content.Insert(insertIndex, $"Unity.Jobs.JobHandle {dep} = default;");
                    }

                    content.AddRange(containers);

                    content.Add($"dependsOn = {lastDependency};");

                    content.Add("// Dependencies scheme:");
                    foreach (var sch in scheme) {
                        content.Add("//" + sch);
                    }

                    content.Add($"// * {Align(lastDependency, 32)} => {Align("dependsOn", 16)}");
                    content.Add("//");

                }
            }

            return innerMethods;

        }

        private static string Align(string str, int align, bool cut = false) {

            if (str.Length < align) {

                var builder = new System.Text.StringBuilder(str);
                builder.Append(' ', align - str.Length);
                return builder.ToString();

            } else if (cut == true) {

                str = str.Substring(0, align - 3);
                str += "...";

            }

            return str;

        }

        private static string GetMethodDeps(string prefix, System.Collections.Generic.HashSet<string> collectedDeps, int i, int collectedDepsCount) {
            return collectedDeps.Count > 0 ? (prefix + " " + string.Join($", {prefix} ", collectedDeps.ToArray(), i, collectedDepsCount)) : string.Empty;
        }

        private static string GetMethodDeps(string prefix, System.Collections.Generic.List<string> collectedDeps, int i, int collectedDepsCount) {
            return collectedDeps.Count > 0 ? (prefix + " " + string.Join($", {prefix} ", collectedDeps.ToArray(), i, collectedDepsCount)) : string.Empty;
        }

        private static GraphLink GetIndex(ME.BECS.Extensions.GraphProcessor.BaseNode node, scg::HashSet<SystemsGraph> graphs) {
            foreach (var graph in graphs) {
                var idx = graph.GetNodeIndex(node);
                if (idx >= 0) {
                    return new GraphLink() {
                        graph = graph,
                        index = idx,
                    };
                }
            }

            return default;
        }

        private static GraphLink GetIndex(ME.BECS.Extensions.GraphProcessor.BaseNode node, ME.BECS.Extensions.GraphProcessor.BaseGraph graph) {
            if (graph == null) {
                UnityEngine.Debug.LogError($"Graph is null for node {node}");
            }
            var idx = graph.GetNodeIndex(node);
            if (idx >= 0) {
                return new GraphLink() {
                    graph = graph,
                    index = idx,
                };
            }
            
            return default;
        }

        private static string GetDeps(ME.BECS.Extensions.GraphProcessor.BaseNode node, out string scheme, scg::HashSet<string> collectedDeps) {
            var result = string.Empty;
            scheme = string.Empty;
            if (node.inputPorts.Count == 0) {
                result = "dependsOn";
                scheme = result;
            } else {
                var arr = node.inputPorts[0].GetEdges().Select(x => "dep" + GetIndex(x.outputNode, x.outputNode.graph).ToString()).Distinct().ToArray();
                foreach (var item in arr) {
                    collectedDeps.Add(item);
                }
                if (arr.Length == 1) {
                    result = $"{arr[0]}";
                    scheme = result;
                } else {
                    var list = string.Join(", ", arr);
                    result = "JobsExt.CombineDependencies(" + list + ")";
                    scheme = list;
                }
            }

            return result;
        }

        private static string GetDeps(int startNodeIndex, ME.BECS.Extensions.GraphProcessor.BaseNode node, out string scheme, out string[] deps, scg::HashSet<string> collectedDeps) {
            var result = string.Empty;
            scheme = string.Empty;
            if (node.inputPorts.Count == 0) {
                result = "dependsOn";
                scheme = result;
                deps = System.Array.Empty<string>();
            } else {
                var arr = node.inputPorts[0].GetEdges().Where(x => x.outputNode.graph != null)
                              .Where(x => ((SystemsGraph)node.graph).IsValidStartNodeOrOther(x.outputNode, startNodeIndex))
                              .Select(x => "dep" + GetIndex(x.outputNode, x.outputNode.graph).ToString()).Distinct().ToArray();
                foreach (var item in arr) {
                    collectedDeps.Add(item);
                }
                if (arr.Length == 1) {
                    deps = new[] { arr[0] };
                    result = $"{arr[0]}";
                    scheme = result;
                } else {
                    var list = string.Join(", ", arr);
                    deps = arr;
                    result = $"JobsExt.CombineDependencies({list})";
                    scheme = list;
                }
            }

            return result;
        }

        private static int GetId(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) {
            var id = graph.GetId();
            if (id < 0) return -id;
            return id;
        }
        
        private static bool IsBursted(CustomCodeGenerator generator, ME.BECS.Extensions.GraphProcessor.BaseNode node, string method) {
            var sysNode = node as ME.BECS.FeaturesGraph.Nodes.SystemNode;
            if (sysNode == null || sysNode.system == null) return false;
            var type = sysNode.system.GetType();
            var isSystemBursted = generator.burstedTypes.Contains(type);
            var methodInfo = type.GetMethod(method);
            var isBursted = generator.burstedTypes.Contains(methodInfo);
            var isDiscarded = generator.burstDiscardedTypes.Contains(methodInfo);
            if (isSystemBursted == true && isDiscarded == true) return false;
            if (isSystemBursted == false && isBursted == false) return false;
            return true;
        }

        private static bool HasMethod<T>(ME.BECS.Extensions.GraphProcessor.BaseNode node) where T : class {
            if (node == null) return false;
            var sysNode = node as ME.BECS.FeaturesGraph.Nodes.SystemNode;
            if (sysNode == null) return true;
            return System.Array.IndexOf(sysNode.system.GetType().GetInterfaces(), typeof(T)) >= 0;
        }

        public static void GetSystemGraph(CustomCodeGenerator generator, scg::List<string> content, SystemsGraph graph) {
            var name = GetId(graph);
            var k = 0;
            foreach (var node in graph.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode) {
                    content.Add($"return (void*)graphNodes{name}_{generator.GetType().Name}[{k}];");
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    GetSystemGraph(generator, content, graphNode.graphValue);
                }
                ++k;
            }
        }

        public static void InitializeGraph(CustomCodeGenerator generator, scg::List<string> content, SystemsGraph graph) {
            var id = GetId(graph);
            content.Add($"// {graph.name}");
            content.Add($"graphNodes{id}_{generator.GetType().Name} = CollectionHelper.CreateNativeArray<System.IntPtr>({graph.nodes.Count}, allocator);");
            var k = 0;
            foreach (var node in graph.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                    var system = systemNode.system;
                    if (system == null) {
                        content.Add("// [!] system is null");
                    } else {
                        var type = system.GetType().FullName;
                        var systemType = GetTypeName(systemNode.system.GetType());
                        content.Add($"var item{id}_{k} = allocator.Allocate(TSize<{type}>.sizeInt, TAlign<{type}>.alignInt);");
                        //content.Add($"_memclear(item{name}_{k}, TSize<{type}>.size);");
                        content.Add($"*({systemType}*)item{id}_{k} = {GetDefinition(systemNode.system)};");
                        content.Add($"TSystem<{systemType}>.index.Data = {k};");
                        content.Add($"TSystemGraph<{systemType}>.index.Data = {graph.GetId()};");
                        content.Add($"graphNodes{id}_{generator.GetType().Name}[{k}] = (System.IntPtr)item{id}_{k};");
                    }
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    InitializeGraph(generator, content, graphNode.graphValue);
                }
                ++k;
            }
        }

        private static string GetDefinition(object system) {

            var result = new System.Text.StringBuilder(100);
            result.Append("new ");
            result.Append(GetTypeName(system.GetType()));
            result.Append(" {\n");
            //var result = $"new {GetTypeName(system.GetType())}() {{\n";
            var fields = system.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance/* | System.Reflection.BindingFlags.NonPublic*/);
            foreach (var field in fields) {
                //var isSerializable = field.FieldType.GetCustomAttribute<System.SerializableAttribute>() != null;
                if (field.IsInitOnly == true) continue;
                if (field.IsPublic == false) continue;
                //if (field.IsPublic == false && field.GetCustomAttribute<UnityEngine.SerializeField>() == null) continue;
                //if (isSerializable == false) continue;
                result.Append(field.Name);
                result.Append(" = ");
                if (field.FieldType.IsEnum == true) {
                    result.Append(field.FieldType.FullName);
                    result.Append(".");
                    result.Append(field.GetValue(system));
                } else if (field.FieldType.IsPrimitive == true) {
                    var val = field.GetValue(system);
                    if (val is double) {
                        result.Append(val);
                        result.Append("d");
                    } else if (val is float fVal) {
                        result.Append(fVal.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        result.Append("f");
                    } else if (val is bool) {
                        result.Append(val.ToString().ToLower());
                    } else if (val is string str) {
                        result.Append("\"");
                        result.Append(str);
                        result.Append("\"");
                    } else {
                        result.Append(val);
                    }
                } else {
                    result.Append(GetDefinition(field.GetValue(system)));
                }
                result.Append(",\n");
            }
            result.Append("}\n");
            return result.ToString();

        }

    }

}