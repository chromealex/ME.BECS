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
            content.Add($"public static void Graph{methodName}_{GetId(graph)}_{this.GetType().Name}(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {{");
            //content.Add("/*");
            {
                content.Add("// " + graph.name);
                //UnityEngine.Debug.LogWarning("GRAPH: " + graph.name);
                innerMethods = new System.Collections.Generic.List<string>();
                innerMethods = AddGraph<T>(this, methodName, content, graph);
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
                        content.Add($"public static void GraphInitialize_{GetId(graph)}_{this.GetType().Name}() {{"); 
                        {
                            content.Add($"// {graph.name}");
                            content.Add("var allocator = Constants.ALLOCATOR_PERSISTENT_ST;");
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
                        content.Add($"public static void* GraphGetSystem_{id}_{this.GetType().Name}(int index) {{");
                        content.Add($"  return (void*)graphNodes{id}_{this.GetType().Name}[index];");
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
                    var graphId = graph.GetInstanceID();
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
                return GetId(this.graph) + "_" + this.index;
            }

        }

        public static scg::List<string> AddGraph<T>(CustomCodeGenerator generator, string method, scg::List<string> content, SystemsGraph graph) where T : class {

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
                var startNode = graph.GetStartNode();
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
                        var maxIter = 100;
                        var methodContent = content;
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

                            var dependsOn = GetDeps(depNode, out var schemeDependsOn, out var deps);
                            
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
                                    var graphStartNode = gr.GetStartNode();
                                    var graphEndNode = gr.GetEndNode();
                                    customInputDeps.Remove(graphStartNode);
                                    customInputDeps.Remove(graphEndNode);
                                    
                                    n = parentNode;
                                    var dep = GetIndex(n, n.graph);
                                    printedDependencies.Add($"dep{dep}");
                                    var dependsOnExit = GetDeps(exitNode, out var schemeDependsOnExit, out var depsExit);
                                    scheme.Add($" * EXIT dep{dep} = {schemeDependsOnExit};");
                                    methodContent.Add($"dep{dep} = {dependsOnExit};");
                                    collectedDeps.Add($"dep{dep}");

                                } else {
                                    
                                    var dependsOnExit = GetDeps(exitNode, out var schemeDependsOnExit, out var depsExit);
                                    scheme.Add($" * EXIT dependsOn = {schemeDependsOnExit};");
                                    methodContent.Add($"dependsOn = {dependsOnExit};");
                                    collectedDeps.Add(dependsOnExit);
                                    lastDependency = dependsOnExit;

                                }
                                
                            } else if (n is ME.BECS.FeaturesGraph.Nodes.SystemNode ||
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

                                        methodContent.Add($"var localContext{index.ToString()} = SystemContext.Create(dt, in world, {dependsOn});");
                                        methodContent.Add($"(({GetTypeName(systemNode.system.GetType())}*)(localNodes_{GetId(index.graph)}[{index.index}]))->{method}(ref localContext{index.ToString()});");
                                        methodContent.Add($"dep{index.ToString()} = Batches.Apply(localContext{index.ToString()}.dependsOn, world.state);");
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

                                    var graphStartNode = graphNode.graphValue.GetStartNode();
                                    var graphEndNode = graphNode.graphValue.GetEndNode();
                                    //methodContent.Add("//    START GRAPH: " + graphNode.graphValue.name + ", node: " + graphStartNode.name);
                                    //UnityEngine.Debug.Log(graphStartNode.graph + " :: " + graphStartNode.graph.GetNodeIndex(graphStartNode));
                                    customInputDeps.Add(graphStartNode, graphNode);
                                    customInputDeps.Add(graphEndNode, graphNode);
                                    q.Enqueue(graphStartNode);
                                    containsInQueue.Add(graphStartNode);

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

                    /*
                    static string MoveNext(CustomCodeGenerator generator, string method, scg::List<string> scheme, scg::List<string> arrMethodDef, scg::List<string> containers, scg::List<string> baseContainer, ref int prevOpenIndexDeps, ref int prevOpenIndex, ref bool isInBurst, ref bool isOpened, scg::List<string> collectedDeps, scg::List<string> content, ME.BECS.Extensions.GraphProcessor.BaseNode node, SystemsGraph graph, scg::HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode> continuousQueue, scg::HashSet<SystemsGraph> rootGraphs, ME.BECS.Extensions.GraphProcessor.BaseNode dependenciesForced = null) {
                        var next = new System.Collections.Generic.List<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                        //UnityEngine.Debug.Log(" -- !!Node: " + node.name);
                        foreach (var port in node.outputPorts) {
                            var edges = port.GetEdges();
                            foreach (var edge in edges) {
                                if (edge.inputNode.inputPorts[0].GetEdges().Count == 1) {
                                    next.Add(edge.inputNode);
                                } else {
                                    continuousQueue.Add(edge.inputNode);
                                }
                            }
                        }

                        string lastDependency;
                        {
                            var index = GetIndex(node, rootGraphs);
                            string dependsOn;
                            string schemeDependsOn;
                            if (dependenciesForced != null) {
                                dependsOn = GetDeps(dependenciesForced, rootGraphs, out schemeDependsOn);
                            } else {
                                dependsOn = GetDeps(node, rootGraphs, out schemeDependsOn);
                            }

                            var systemNode = node as ME.BECS.FeaturesGraph.Nodes.SystemNode;
                            if (systemNode != null) {
                                var isBursted = IsBursted(generator, systemNode, method);
                                if (isOpened == false || isInBurst != isBursted) {
                                    if (isOpened == true) {
                                        // insert output nodes
                                        if (collectedDeps.Count - prevOpenIndexDeps > 0) {
                                            var data = $", {GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}) {{";
                                            content.Insert(prevOpenIndex, data);
                                            var dataDef = $", {GetMethodDeps("ref", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}";
                                            containers.Add(dataDef);
                                        }
                                        content.Add("}"); // close previous
                                        containers.Add(");\n");
                                    }

                                    var deps = GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, 0, collectedDeps.Count);
                                    var depsDef = GetMethodDeps("ref", collectedDeps, 0, collectedDeps.Count);
                                    var methodData = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(float dt, in World world, ref Unity.Jobs.JobHandle dependsOn, {GetMethodDeps("System.IntPtr*", arrMethodDef, 0, arrMethodDef.Count)}, {deps}";
                                    content.Add($"{(isBursted == true ? "[BURST] " : string.Empty)}private static void {methodData}"); // open next
                                    var methodDef = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(dt, in world, ref dependsOn, {GetMethodDeps("", arrMethodDef, 0, arrMethodDef.Count)}, {depsDef}";
                                    containers.Add(methodDef);
                                    prevOpenIndex = content.Count;
                                    prevOpenIndexDeps = collectedDeps.Count;
                                    isOpened = true;
                                    isInBurst = isBursted;
                                }
                            }

                            var hasMethod = HasMethod<T>(systemNode);
                            if (node is ME.BECS.FeaturesGraph.Nodes.StartNode ||
                                hasMethod == false ||
                                node.enabled == false || node.IsGroupEnabled() == false) {

                                if (isOpened == false) {
                                    baseContainer.Add($"dep{index.ToString()} = {dependsOn};");
                                } else {
                                    content.Add($"dep{index.ToString()} = {dependsOn};");
                                }

                                collectedDeps.Add($"dep{index.ToString()}");

                                var notUsedDescr = "Empty Node";
                                if (hasMethod == false) {
                                    notUsedDescr = $"Required method {typeof(T)} was not found. Node skipped.";
                                }
                                scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(node.name, 32, true)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}] - {notUsedDescr}");

                            } else if (systemNode != null) {
                                
                                content.Add($"// {node.name} ({graph.name})");
                                content.Add($"var localContext{index.ToString()} = SystemContext.Create(dt, in world, {dependsOn});");
                                //content.Add("Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = 0;");
                                //var test = systemNode.system.GetType().FullName;
                                content.Add($"(({GetTypeName(systemNode.system.GetType())}*)(localNodes_{GetId(index.graph)}[{index.index}]))->{method}(ref localContext{index.ToString()});");
                                
                                //content.Add("Unity.Jobs.LowLevel.Unsafe.JobsUtility.ResetJobWorkerCount();");
                                content.Add($"dep{index.ToString()} = Batches.Apply(localContext{index.ToString()}.dependsOn, world.state);");
                    
                                collectedDeps.Add($"dep{index.ToString()}");
                                scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(node.name, 32, true)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}]");

                            }
                            
                            lastDependency = $"dep{index.ToString()}";
                        }

                        foreach (var nextNode in next) {
                            //UnityEngine.Debug.Log("NODE: " + nextNode.name);
                            if (nextNode is ME.BECS.FeaturesGraph.Nodes.ExitNode) continue;
                            if (nextNode is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                                rootGraphs.Add(graphNode.graphValue);
                                lastDependency = MoveNext(generator, method, scheme, arrMethodDef, containers, baseContainer, ref prevOpenIndexDeps, ref prevOpenIndex, ref isInBurst, ref isOpened, collectedDeps, content, graphNode.graphValue.GetStartNode(), graphNode.graphValue, continuousQueue, rootGraphs, graphNode);
                                continuousQueue.Add(graphNode);
                                //lastDependency = MoveNext(generator, method, scheme, arrMethodDef, containers, baseContainer, ref prevOpenIndexDeps, ref prevOpenIndex, ref isInBurst, ref isOpened, collectedDeps, content, nextNode, graph, continuousQueue, rootGraphs, graphNode);
                            } else {
                                lastDependency = MoveNext(generator, method, scheme, arrMethodDef, containers, baseContainer, ref prevOpenIndexDeps, ref prevOpenIndex, ref isInBurst, ref isOpened, collectedDeps, content, nextNode, graph, continuousQueue, rootGraphs);
                            }
                        }

                        return lastDependency;
                    }

                    var rootGraphs = new scg::HashSet<SystemsGraph>();
                    rootGraphs.Add(graph);
                    var queue = new scg::HashSet<ME.BECS.Extensions.GraphProcessor.BaseNode>();
                    var node = (ME.BECS.Extensions.GraphProcessor.BaseNode)startNode;
                    var isOpened = false;
                    var isInBurst = false;
                    var prevOpenIndex = -1;
                    var prevOpenIndexDeps = 0;
                    while (true) {
                        lastDependency = MoveNext(generator, method, scheme, arrMethodDef, containers, content, ref prevOpenIndexDeps, ref prevOpenIndex, ref isInBurst, ref isOpened, collectedDeps, innerMethods, node, graph, queue, rootGraphs);
                        if (queue.Count > 0) {
                            node = queue.First();
                            queue.Remove(node);
                        } else {
                            break;
                        }
                    }

                    if (isOpened == true) {
                        if (collectedDeps.Count - prevOpenIndexDeps > 0) {
                            var data = $", {GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}) {{";
                            innerMethods.Insert(prevOpenIndex, data);
                            var dataDef = $", {GetMethodDeps("ref", collectedDeps, prevOpenIndexDeps, collectedDeps.Count - prevOpenIndexDeps)}";
                            containers.Add(dataDef);
                        }
                        innerMethods.Add("}"); // close last
                        containers.Add(");\n");
                    }
                    
                    foreach (var dep in collectedDeps) {
                        content.Insert(insertIndex, $"Unity.Jobs.JobHandle {dep} = default;");
                    }

                    */
                    
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

        private static string GetDeps(ME.BECS.Extensions.GraphProcessor.BaseNode node, out string scheme) {
            var result = string.Empty;
            scheme = string.Empty;
            if (node.inputPorts.Count == 0) {
                result = "dependsOn";
                scheme = result;
            } else {
                var arr = node.inputPorts[0].GetEdges().Select(x => "dep" + GetIndex(x.outputNode, x.outputNode.graph).ToString()).Distinct().ToArray();
                if (arr.Length == 1) {
                    result = $"{arr[0]}";
                    scheme = result;
                } else {
                    var list = string.Join(", ", arr);
                    result = "Unity.Jobs.JobHandle.CombineDependencies(" + list + ")";
                    scheme = list;
                }
            }

            return result;
        }

        private static string GetDeps(ME.BECS.Extensions.GraphProcessor.BaseNode node, out string scheme, out string[] deps) {
            var result = string.Empty;
            scheme = string.Empty;
            if (node.inputPorts.Count == 0) {
                result = "dependsOn";
                scheme = result;
                deps = System.Array.Empty<string>();
            } else {
                var arr = node.inputPorts[0].GetEdges().Select(x => "dep" + GetIndex(x.outputNode, x.outputNode.graph).ToString()).Distinct().ToArray();
                if (arr.Length == 1) {
                    deps = new[] { arr[0] };
                    result = $"{arr[0]}";
                    scheme = result;
                } else {
                    var list = string.Join(", ", arr);
                    deps = arr;
                    result = "Unity.Jobs.JobHandle.CombineDependencies(" + list + ")";
                    scheme = list;
                }
            }

            return result;
        }

        private static int GetId(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) {
            var id = graph.GetInstanceID();
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
            content.Add($"graphNodes{id}_{generator.GetType().Name} = CollectionHelper.CreateNativeArray<System.IntPtr>({graph.nodes.Count}, Constants.ALLOCATOR_PERSISTENT_ST);");
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
                        content.Add($"TSystemGraph<{systemType}>.index.Data = {id};");
                        content.Add($"graphNodes{id}_{generator.GetType().Name}[{k}] = (System.IntPtr)item{id}_{k};");
                    }
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    InitializeGraph(generator, content, graphNode.graphValue);
                }
                ++k;
            }
        }

        private static string GetDefinition(object system) {

            var result = $"new {GetTypeName(system.GetType())}() {{\n";
            var fields = system.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance/* | System.Reflection.BindingFlags.NonPublic*/);
            foreach (var field in fields) {
                var isSerializable = field.FieldType.GetCustomAttribute<System.SerializableAttribute>() != null;
                if (field.IsInitOnly == true) continue;
                //if (field.IsPublic == false && field.GetCustomAttribute<UnityEngine.SerializeField>() == null) continue;
                if (isSerializable == false) continue;
                if (field.FieldType.IsPrimitive == true) {
                    var val = field.GetValue(system);
                    if (val is double) {
                        result += field.Name + " = " + val.ToString() + "d,\n";
                    } else if (val is float) {
                        result += field.Name + " = " + val.ToString() + "f,\n";
                    } else if (val is bool) {
                        result += field.Name + " = " + val.ToString().ToLower() + ",\n";
                    } else if (val is string) {
                        result += field.Name + " = \"" + (string)val + "\",\n";
                    } else {
                        result += field.Name + " = " + val + ",\n";
                    }
                } else {
                    result += field.Name + " = " + GetDefinition(field.GetValue(system)) + ",\n";
                }
            }
            result += "}\n";
            return result;

        }

    }

}