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
            content.Add($"public static void Graph{methodName}_{GetId(graph)}_{this.GetType().Name}(uint dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {{");
            //content.Add("/*");
            {
                content.Add("// " + graph.name);
                var contentFill = new scg::List<string>();
                var startNodeIndex = 0;
                //UnityEngine.Debug.LogWarning("GRAPH: " + graph.name);
                innerMethods = new System.Collections.Generic.List<string>();
                innerMethods = AddGraph<T>(this, startNodeIndex, methodName, contentFill, graph);
                content.AddRange(contentFill);
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
                            content.Add($"graphNodes{id}_{this.GetType().Name} = CollectionHelper.CreateNativeArray<System.IntPtr>({GetSystemsCount(graph)}, allocator);");
                            InitializeGraph(this, content, graph, id, 0);
                        }
                        content.Add("}");
                    }
                    {
                        this.AddMethod<IAwake>(graph, "OnAwake", out var caller, out var innerMethods);
                        content.AddRange(innerMethods);
                        content.AddRange(caller);
                    }
                    {
                        this.AddMethod<IStart>(graph, "OnStart", out var caller, out var innerMethods);
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
                content.Add("[UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]");
                content.Add("public static void Initialize() {");
                content.Add("CustomModules.RegisterFirstPass(SystemsLoad);");
                content.Add("}");
                content.Add("[UnityEngine.Scripting.PreserveAttribute]");
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
                    content.Add($"SystemsStatic.RegisterStartMethod(GraphOnStart_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterUpdateMethod(GraphOnUpdate_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterDrawGizmosMethod(GraphOnDrawGizmos_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterDestroyMethod(GraphOnDestroy_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add($"SystemsStatic.RegisterGetSystemMethod(GraphGetSystem_{id}_{this.GetType().Name}, {graphId}, false);");
                    content.Add("}");
                }
                content.Add("}");
            }
            
            var newContent = string.Join("\n", content);
            return newContent;
            
        }

        private struct GraphLink {

            public ME.BECS.Extensions.GraphProcessor.BaseGraph graph;
            public int index;
            public int globalIndex;
            public int genericIndex;

            public override string ToString() {
                return $"{GetId(this.graph)}_{(this.index >= 0 ? this.index.ToString() : $"__{(-this.index)}")}_{this.genericIndex}";
            }

            public void AddGeneric() {
                ++this.genericIndex;
            }

        }

        public static scg::List<string> AddGraph<T>(CustomCodeGenerator generator, int startNodeIndex, string method, scg::List<string> content, SystemsGraph graph) where T : class {

            var graphRootId = GetId(graph);
            static void AddNodesArrDefinition(CustomCodeGenerator generator, scg::List<string> content, SystemsGraph graph, scg::List<string> arrMethodDef, int graphRootId) {

                content.Add($"var systems = (System.IntPtr*)graphNodes{graphRootId}_{generator.GetType().Name}.GetUnsafePtr();");
                arrMethodDef.Add($"systems");
                
                /*foreach (var node in graph.nodes) {
                    if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        AddNodesArrDefinition(generator, content, graphNode.graphValue, arrMethodDef, graphRootId);
                    }
                }*/
            }

            var arrMethodDef = new scg::List<string>();
            AddNodesArrDefinition(generator, content, graph, arrMethodDef, graphRootId);

            var insertIndex = content.Count;

            var scheme = new scg::List<string>();
            var innerMethods = new scg::List<string>();
            {
                var startNode = graph.GetStartNode(startNodeIndex);
                if (startNode != null) {

                    var containers = new scg::List<string>();
                    var collectedDeps = new scg::HashSet<string>();
                    string lastDependency = string.Empty;
                    var nodesCount = 0u;
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

                        void AddApply(ME.BECS.Extensions.GraphProcessor.BaseNode node, GraphLink index, ref string schemeDependsOn, string customDep = null) {

                            if (node.syncPoint == false) return;
                            if (customInputDeps.TryGetValue(node, out var parentNode) == true) {
                                while (parentNode != null) {
                                    if (parentNode.syncPoint == false) return;
                                    customInputDeps.TryGetValue(parentNode, out var parentNodeInner);
                                    parentNode = parentNodeInner;
                                }
                            }

                            var indexStr = index.ToString();
                            var resDep = $"dep{indexStr}";
                            scheme.Add($" * {Align("Batches.Apply", 32)} :  {Align($"{schemeDependsOn} => {resDep}", 16 + 32 + 4, true)} [  SYNC   ]");
                            //methodContent.Add($"{resDep} = Batches.Apply({resDep}, in world);");
                            schemeDependsOn = resDep;
                            if (customDep != null) {
                                methodContent.Add($"{customDep} = Batches.Apply({customDep}, world.state);");
                            } else {
                                methodContent.Add($"dep{indexStr} = Batches.Apply(dep{indexStr}, world.state);");
                            }
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
                                globalIndex = GetNodeIndex(graph, n, out _),
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

                                    var customAttr = string.Empty;
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

                                        ++nodesCount;
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
                                            var methodData = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(uint dt, in World world, ref Unity.Jobs.JobHandle dependsOn, {GetMethodDeps("System.IntPtr*", arrMethodDef, 0, arrMethodDef.Count)}, {methodDeps}";
                                            methodContent.Add($"{(isBursted == true ? "[BURST] " : string.Empty)}private static void {methodData}"); // open next
                                            var methodDef = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(dt, in world, ref dependsOn, {GetMethodDeps("", arrMethodDef, 0, arrMethodDef.Count)}, {methodDepsDef}";
                                            containers.Add(methodDef);
                                            prevOpenIndex = methodContent.Count;
                                            prevOpenIndexDeps = collectedDeps.Count;

                                            isOpened = true;
                                            isInBurst = isBursted;
                                        }

                                        if (systemNode.system.GetType().IsGenericType == true) {
                                            var systemType = systemNode.system.GetType();
                                            if (systemType.IsGenericType == true) {
                                                systemType = systemType.GetGenericTypeDefinition();
                                                var genType = EditorUtils.GetFirstInterfaceConstraintType(systemType);
                                                if (genType != null) {
                                                    if (systemType.GetCustomAttribute<SystemGenericParallelModeAttribute>() != null) {
                                                        customAttr = "[ PARALLEL ]";
                                                        // Parallel mode
                                                        var srcDep = index;
                                                        var types = UnityEditor.TypeCache.GetTypesDerivedFrom(genType).OrderBy(x => x.FullName).ToArray();
                                                        methodContent.Add($"var depsGeneric{srcDep.ToString()} = new NativeArray<Unity.Jobs.JobHandle>({types.Length}, Constants.ALLOCATOR_TEMP);");
                                                        foreach (var cType in types) {
                                                            var type = systemType.MakeGenericType(cType);
                                                            var indexStr = index.ToString();
                                                            methodContent.Add("{");
                                                            methodContent.Add($"var input = {dependsOn};");
                                                            methodContent.Add($"var localContext{indexStr} = SystemContext.Create(dt, in world, input);");
                                                            methodContent.Add($"(({EditorUtils.GetTypeName(type)}*)systems[{(index.globalIndex + index.genericIndex)}])->{method}(ref localContext{indexStr});");
                                                            methodContent.Add($"var output = localContext{indexStr}.dependsOn;");
                                                            methodContent.Add($"depsGeneric{srcDep.ToString()}[{index.genericIndex}] = output;");
                                                            methodContent.Add("}");
                                                            if (index.genericIndex == 0) collectedDeps.Add($"dep{indexStr}");
                                                            printedDependencies.Add($"dep{indexStr}");
                                                            index.AddGeneric();
                                                        }

                                                        methodContent.Add($"dep{srcDep.ToString()} = Unity.Jobs.JobHandle.CombineDependencies(depsGeneric{srcDep.ToString()});");
                                                        AddApply(systemNode, srcDep, ref schemeDependsOn);
                                                        index = srcDep;
                                                    } else {
                                                        // One-by-one mode
                                                        customAttr = "[ ONE-BY-ONE ]";
                                                        var srcDep = index;
                                                        var prevIndex = dependsOn;
                                                        var types = UnityEditor.TypeCache.GetTypesDerivedFrom(genType).OrderBy(x => x.FullName).ToArray();
                                                        foreach (var cType in types) {
                                                            var type = systemType.MakeGenericType(cType);
                                                            var indexStr = index.ToString();
                                                            methodContent.Add("{");
                                                            methodContent.Add($"var input = {prevIndex};");
                                                            methodContent.Add($"var localContext{indexStr} = SystemContext.Create(dt, in world, input);");
                                                            methodContent.Add($"(({EditorUtils.GetTypeName(type)}*)systems[{(index.globalIndex + index.genericIndex)}])->{method}(ref localContext{indexStr});");
                                                            methodContent.Add($"dep{indexStr} = localContext{indexStr}.dependsOn;");
                                                            AddApply(systemNode, index, ref schemeDependsOn);
                                                            methodContent.Add("}");
                                                            collectedDeps.Add($"dep{indexStr}");
                                                            printedDependencies.Add($"dep{indexStr}");
                                                            prevIndex = $"dep{indexStr}";
                                                            index.AddGeneric();
                                                        }
                                                        methodContent.Add($"dep{srcDep.ToString()} = {prevIndex};");
                                                        index = srcDep;
                                                    }
                                                }
                                            }

                                        } else {

                                            methodContent.Add("{");
                                            methodContent.Add($"var input = {dependsOn};");
                                            methodContent.Add($"dep{index.ToString()} = input;");
                                            methodContent.Add($"var localContext{index.ToString()} = SystemContext.Create(dt, in world, dep{index.ToString()});");
                                            methodContent.Add($"(({EditorUtils.GetTypeName(systemNode.system.GetType())}*)systems[{index.globalIndex}])->{method}(ref localContext{index.ToString()});");
                                            methodContent.Add($"dep{index.ToString()} = localContext{index.ToString()}.dependsOn;");
                                            AddApply(systemNode, index, ref schemeDependsOn);
                                            methodContent.Add("}");
                                            collectedDeps.Add($"dep{index.ToString()}");

                                        }

                                    }

                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(EditorUtils.GetTypeName(systemNode.system.GetType()), 32, true)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}]{customAttr}{notUsedDescr}");

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

                    if (string.IsNullOrEmpty(lastDependency) == true) {
                        //content.Clear();
                    } else {
                        content.Add($"dependsOn = {lastDependency};");
                    }

                    if (nodesCount == 0u) {
                        content.Clear();
                        content.Add("// All graph's nodes were skipped");
                    }

                    content.Add("// Dependencies scheme:");
                    foreach (var sch in scheme) {
                        content.Add("//" + sch);
                    }

                    content.Add($"// * {Align(lastDependency, 32)} => {Align("dependsOn", 16)}");

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
            if (type.IsGenericType == true) {
                type = type.GetGenericTypeDefinition();
            }
            var isSystemBursted = generator.burstedTypes.Contains(type);
            var methodInfo = type.GetMethod(method);
            var isBursted = generator.burstedTypes.Any(x => x == methodInfo);
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

        private static int GetNodeIndex(SystemsGraph graph, ME.BECS.Extensions.GraphProcessor.BaseNode sysNode, out bool found, int index = 0) {
            found = false;
            foreach (var node in graph.nodes) {
                if (node == sysNode) {
                    found = true;
                    return index;
                } else if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                    if (systemNode.system != null) {
                        if (systemNode.system.GetType().IsGenericType == true) {
                            var typeGen = EditorUtils.GetFirstInterfaceConstraintType(systemNode.system.GetType().GetGenericTypeDefinition());
                            if (typeGen != null) {
                                index += UnityEditor.TypeCache.GetTypesDerivedFrom(typeGen).Count;
                            }
                        } else {
                            ++index;
                        }
                    }
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    index = GetNodeIndex(graphNode.graphValue, sysNode, out found, index);
                    if (found == true) {
                        return index;
                    }
                }
            }

            return index;
        }

        public static int InitializeGraph(CustomCodeGenerator generator, scg::List<string> content, SystemsGraph graph, int rootGraphId, int index) {
            foreach (var node in graph.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                    var system = systemNode.system;
                    if (system == null) {
                        content.Add("// [!] system is null");
                    } else {
                        var systemType = system.GetType();
                        if (systemType.IsGenericType == true) {
                            systemType = systemType.GetGenericTypeDefinition();
                            var genType = EditorUtils.GetFirstInterfaceConstraintType(systemType);
                            if (genType != null) {
                                var types = UnityEditor.TypeCache.GetTypesDerivedFrom(genType).OrderBy(x => x.FullName).ToArray();
                                foreach (var cType in types) {
                                    var type = systemType.MakeGenericType(cType);
                                    var systemTypeStr = EditorUtils.GetTypeName(type);
                                    content.Add("{");
                                    content.Add($"var item = allocator.Allocate(TSize<{systemTypeStr}>.sizeInt, TAlign<{systemTypeStr}>.alignInt);");
                                    content.Add($"*({systemTypeStr}*)item = {GetDefinition(System.Activator.CreateInstance(type), type)};");
                                    content.Add($"TSystemGraph.Register<{systemTypeStr}>({rootGraphId}, item);");
                                    content.Add($"graphNodes{rootGraphId}_{generator.GetType().Name}[{index}] = (System.IntPtr)item;");
                                    content.Add("}");
                                    ++index;
                                }
                            }
                        } else {
                            var systemTypeStr = EditorUtils.GetTypeName(systemType);
                            content.Add("{");
                            content.Add($"var item = allocator.Allocate(TSize<{systemTypeStr}>.sizeInt, TAlign<{systemTypeStr}>.alignInt);");
                            content.Add($"*({systemTypeStr}*)item = {GetDefinition(systemNode.system)};");
                            content.Add($"TSystemGraph.Register<{systemTypeStr}>({rootGraphId}, item);");
                            content.Add($"graphNodes{rootGraphId}_{generator.GetType().Name}[{index}] = (System.IntPtr)item;");
                            content.Add("}");
                            ++index;
                        }
                    }
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    index = InitializeGraph(generator, content, graphNode.graphValue, rootGraphId, index);
                }
            }

            return index;
        }

        public static int GetSystemsCount(SystemsGraph graph) {
            var cnt = 0;
            foreach (var node in graph.nodes) {
                if (node is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode) {
                    if (systemNode.system != null) {
                        if (systemNode.system.GetType().IsGenericType == true) {
                            var typeGen = EditorUtils.GetFirstInterfaceConstraintType(systemNode.system.GetType().GetGenericTypeDefinition());
                            if (typeGen != null) {
                                cnt += UnityEditor.TypeCache.GetTypesDerivedFrom(typeGen).Count;
                            }
                        } else {
                            ++cnt;
                        }
                    }
                } else if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                    cnt += GetSystemsCount(graphNode.graphValue);
                }
            }

            return cnt;
        }

        private static string GetDefinition(object system, System.Type type = null) {

            if (type == null) type = system.GetType();
            var result = new System.Text.StringBuilder(100);
            result.Append("new ");
            result.Append(EditorUtils.GetTypeName(type));
            result.Append(" {\n");
            //var result = $"new {GetTypeName(system.GetType())}() {{\n";
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance/* | System.Reflection.BindingFlags.NonPublic*/);
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