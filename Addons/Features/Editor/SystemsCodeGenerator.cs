using System.Linq;
using System.Reflection;
using ME.BECS.Mono.Reflection;
using Unity.Collections;

namespace ME.BECS.Editor.Systems {

    using scg = System.Collections.Generic;
    using ME.BECS.FeaturesGraph;
    
    public class SystemsCodeGenerator : CustomCodeGenerator {


        public override FileContent[] AddFileContent(System.Collections.Generic.List<System.Type> references) {

            var content = new scg::List<FileContent>();
            this.AddContent(content);

            return content.ToArray();

        }

        public override string AddPublicContent() {
            
            // Runtime graph registration and its load hook are emitted by Roslyn.
            return string.Empty;
            
        }

        public string AddContent(scg::List<FileContent> filesContent) {

            var content = new scg::List<string>();
            if (this.editorAssembly == false) {
                var graphs = UnityEditor.AssetDatabase.FindAssets("t:SystemsGraph");
                if (filesContent != null) {
                    foreach (var guid in graphs) {
                        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                        var graph = UnityEditor.AssetDatabase.LoadAssetAtPath<SystemsGraph>(path);
                        if (graph.isInnerGraph == true) continue;
                        var id = GetId(graph);
                        var baseName = $"Graph{EditorUtils.GetCodeName(graph.name)}";
                        var graphInitialize = new FileContent() {
                            filename = $"{baseName}.Initialize",
                        };
                        var graphAwake = new FileContent() {
                            filename = $"{baseName}.Awake",
                        };
                        var graphStart = new FileContent() {
                            filename = $"{baseName}.Start",
                        };
                        var graphUpdate = new FileContent() {
                            filename = $"{baseName}.Update",
                        };
                        var graphDestroy = new FileContent() {
                            filename = $"{baseName}.Destroy",
                        };
                        var graphDrawGizmos = new FileContent() {
                            filename = $"{baseName}.DrawGizmos",
                        };
                        var graphInitializeContent = new scg::List<string>();
                        graphInitializeContent.Add($"[BURST] public unsafe partial class Graph{baseName}Initialize {{");
                        var graphAwakeContent = new scg::List<string>();
                        graphAwakeContent.Add($"public static unsafe partial class Graph{baseName}Awake {{");
                        var graphStartContent = new scg::List<string>();
                        graphStartContent.Add($"public static unsafe partial class Graph{baseName}Start {{");
                        var graphUpdateContent = new scg::List<string>();
                        graphUpdateContent.Add($"public static unsafe partial class Graph{baseName}Update {{");
                        var graphDestroyContent = new scg::List<string>();
                        graphDestroyContent.Add($"public static unsafe partial class Graph{baseName}Destroy {{");
                        var graphDrawGizmosContent = new scg::List<string>();
                        graphDrawGizmosContent.Add($"public static unsafe partial class Graph{baseName}DrawGizmos {{");
                        
                        //var name = System.Text.RegularExpressions.Regex.Replace(graph.name, @"(\s+|@|&|'|\(|\)|<|>|#|-)", "_");

                        { // initialize method
                            if (!SourceGeneratorInputManifest.TryGetGraphApplyPlan(graph, out _)) {
                                throw new System.InvalidOperationException("Source injection plan unavailable for graph " + graph.name +
                                    ". Export Injection Coverage and resolve unsupported fields/jobs; legacy injection emission is disabled.");
                            }
                        }
                        // Lifecycle bodies and entry points are owned by the source generator.
                        // Keep empty phase files in the export set so normal regeneration
                        // replaces previously emitted legacy bodies without manual deletion.
                        // AddGraph remains only as the independent diagnostic oracle.

                        graphInitializeContent.Add("}");
                        graphAwakeContent.Add("}");
                        graphStartContent.Add("}");
                        graphUpdateContent.Add("}");
                        graphDestroyContent.Add("}");
                        graphDrawGizmosContent.Add("}");
                        
                        graphInitialize.content = string.Join("\n", graphInitializeContent);
                        graphAwake.content = string.Join("\n", graphAwakeContent);
                        graphStart.content = string.Join("\n", graphStartContent);
                        graphUpdate.content = string.Join("\n", graphUpdateContent);
                        graphDestroy.content = string.Join("\n", graphDestroyContent);
                        graphDrawGizmos.content = string.Join("\n", graphDrawGizmosContent);
                        filesContent.Add(graphInitialize);
                        filesContent.Add(graphAwake);
                        filesContent.Add(graphStart);
                        filesContent.Add(graphUpdate);
                        filesContent.Add(graphDestroy);
                        filesContent.Add(graphDrawGizmos);
                    }
                }
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

        public class CollectedDeps {

            public scg::HashSet<string> deps;
            public scg::Dictionary<string, int> keyToIndex;
            public int index;

            public int Count => this.deps.Count;

            public CollectedDeps() {
                this.deps = new scg::HashSet<string>();
                this.keyToIndex = new scg::Dictionary<string, int>();
            }

            public void Add(string dep) {
                this.deps.Add(dep);
                if (this.keyToIndex.TryAdd(dep, this.index) == true) {
                    ++this.index;
                }
            }

            public string[] ToArray() {
                return this.deps.ToArray();
            }

            public string GetDefinitionString() {
                return $"var dependencies = _makeArray<Unity.Jobs.JobHandle>({this.Count}, Constants.ALLOCATOR_TEMP, false);";
            }

            public string GetCallString() {
                return "dependencies";
            }

            public string GetArgString() {
                return "safe_ptr<Unity.Jobs.JobHandle> dependencies";
            }

            public string GetWriteOpString(string key) {
                var index = this.keyToIndex[key];
                return $"dependencies[{index}] = {key};";
            }

            public string GetReadOpString(string key) {
                var index = this.keyToIndex[key];
                return $"dependencies[{index}]";
            }

        }
        
        public static scg::List<string> AddGraph<T>(CustomCodeGenerator generator, string baseName, int startNodeIndex, string method, Method methodEnum, scg::List<string> content, SystemsGraph graph, scg::List<string> lifecycleTrace = null, LifecycleDependencyTrace dependencyTrace = null) where T : class {

            var graphRootId = GetId(graph);
            static void AddNodesArrDefinition(CustomCodeGenerator generator, string baseName, scg::List<string> content, SystemsGraph graph, scg::List<string> arrMethodDef, int graphRootId) {

                content.Add($"var systems = (System.IntPtr*)Graph{baseName}Initialize.graphNodes{graphRootId}_{generator.GetType().Name}.GetUnsafePtr();");
                arrMethodDef.Add($"systems");
                
                /*foreach (var node in graph.nodes) {
                    if (node is ME.BECS.FeaturesGraph.Nodes.GraphNode graphNode) {
                        AddNodesArrDefinition(generator, content, graphNode.graphValue, arrMethodDef, graphRootId);
                    }
                }*/
            }

            var arrMethodDef = new scg::List<string>();
            AddNodesArrDefinition(generator, baseName, content, graph, arrMethodDef, graphRootId);

            var insertIndex = content.Count;

            var scheme = new scg::List<string>();
            var innerMethods = new scg::List<string>();
            {
                var startNode = graph.GetStartNode(startNodeIndex);
                if (startNode != null) {

                    var containers = new scg::List<string>();
                    var collectedDeps = new CollectedDeps();
                    string lastDependency = string.Empty;
                    string[] lastDependencyInputs = System.Array.Empty<string>();
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

                        void TraceInputs(string expression, string[] dependencies) {
                            if (dependencyTrace == null || (dependencies.Length == 0 && expression == "dependsOn")) return;
                            dependencyTrace.Merge(expression, dependencies.Select(collectedDeps.GetReadOpString));
                        }

                        string AddPreApply(ME.BECS.Extensions.GraphProcessor.BaseNode node, GraphLink index, ref string schemeDependsOn, string dependsOn) {
                            if (node.inputPorts.Count > 0) {
                                if (node.GetSyncPoint(methodEnum).syncPoint == true) {
                                    var edges = node.inputPorts[0].GetEdges();
                                    if (edges.Count > 1) {
                                        // we have current sync point, but previous sync point was not exist - add pre apply
                                        var v = $"preBatch{index}";
                                        schemeDependsOn = v;
                                        scheme.Add($" * {Align("Batches.Apply (Pre)", 32)} :  {Align($"{schemeDependsOn} => {v}", 16 + 32 + 4, CutType.Start)} [  SYNC   ]");
                                        methodContent.Add($"var {v} = Batches.Apply({dependsOn}, in world);");
                                        dependencyTrace?.Apply(v, dependsOn);
                                        return v;
                                    }
                                }
                            }
                            return dependsOn;
                        }
                        
                        bool RequiresApply(ME.BECS.Extensions.GraphProcessor.BaseNode node) {
                            if (node is not ME.BECS.FeaturesGraph.Nodes.ExitNode && !node.GetSyncPoint(methodEnum).syncPoint) return false;
                            #if !ENABLE_BECS_FLAT_QUERIES
                            if (customInputDeps.TryGetValue(node, out var parent)) {
                                while (parent != null) {
                                    if (!parent.GetSyncPoint(methodEnum).syncPoint) return false;
                                    customInputDeps.TryGetValue(parent, out parent);
                                }
                            }
                            #endif
                            return true;
                        }

                        void AddApply(ME.BECS.Extensions.GraphProcessor.BaseNode node, GraphLink index, ref string schemeDependsOn, string customDep = null, string customOutputDep = null) {

                            var indexStr = index.ToString();
                            if (customDep == null) customDep = $"dep{indexStr}";
                            if (customOutputDep == null) customOutputDep = $"dep{indexStr}";
                            if (!RequiresApply(node)) {
                                dependencyTrace?.Copy(customOutputDep, customDep);
                                methodContent.Add($"{customOutputDep} = {customDep};");
                                return;
                            }
                            #if ENABLE_BECS_FLAT_QUERIES
                            var tag = "[   SET   ]";
                            #else
                            var tag = "[  SYNC   ]";

                            #endif
                            
                            var resDep = $"dep{indexStr}";
                            scheme.Add($" * {Align("Batches.Apply", 32)} :  {Align($"{schemeDependsOn} => {resDep}", 16 + 32 + 4, CutType.Start)} {tag}");
                            //methodContent.Add($"{resDep} = Batches.Apply({resDep}, in world);");
                            schemeDependsOn = resDep;
                            methodContent.Add($"{customOutputDep} = Batches.Apply({customDep}, in world);");
                            dependencyTrace?.Apply(customOutputDep, customDep);
                        }
                        
                        while (q.Count > 0) {

                            if (--maxIter == 0) {
                                throw new System.InvalidOperationException($"[ME.BECS] Graph '{graph.name}', phase {methodEnum}: dependency traversal exceeded 10000 iterations. Check cycles or unresolved input dependencies. Refusing to emit an incomplete lifecycle body.");
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

                            TraceInputs(dependsOn, deps);
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
                                    TraceInputs(dependsOnExit, depsExit);
                                    scheme.Add($" * EXIT dep{dep} = {schemeDependsOnExit};");
                                    collectedDeps.Add($"dep{dep}");
                                    methodContent.Add($"{collectedDeps.GetReadOpString($"dep{dep}")} = {dependsOnExit};");
                                    dependencyTrace?.Copy(collectedDeps.GetReadOpString($"dep{dep}"), dependsOnExit);

                                } else {

                                    var dependsOnExit = GetDeps(startNodeIndex, exitNode, out var schemeDependsOnExit, out var depsExit, collectedDeps);
                                    TraceInputs(dependsOnExit, depsExit);
                                    scheme.Add($" * EXIT dependsOn = {schemeDependsOnExit};");
                                    methodContent.Add($"dependsOn = {dependsOnExit};");
                                    dependencyTrace?.Copy("dependsOn", dependsOnExit);
                                    //collectedDeps.Add(dependsOnExit);
                                    lastDependency = dependsOnExit;
                                    lastDependencyInputs = depsExit;

                                }

                            } else if (
                                n is ME.BECS.FeaturesGraph.Nodes.SystemNode ||
                                n is ME.BECS.Extensions.GraphProcessor.RelayNode ||
                                n is ME.BECS.FeaturesGraph.Nodes.StartNode) {

                                if (n is ME.BECS.FeaturesGraph.Nodes.SystemNode systemNode && systemNode.system != null &&
                                    n.enabled == true && n.IsGroupEnabled() == true) {

                                    var customAttr = string.Empty;
                                    var notUsedDescr = " - Empty Node";
                                    var hasMethod = HasMethod(n, method);
                                    if (hasMethod == false ||
                                        n.enabled == false || n.IsGroupEnabled() == false) {
                                        
                                        collectedDeps.Add($"dep{index.ToString()}");
                                        methodContent.Add($"{collectedDeps.GetReadOpString($"dep{index.ToString()}")} = {dependsOn};");
                                        dependencyTrace?.Copy(collectedDeps.GetReadOpString($"dep{index.ToString()}"), dependsOn);

                                        if (hasMethod == false) {
                                            notUsedDescr = $" - Method {typeof(T)} was not found. Node skipped.";
                                        }

                                    } else {

                                        ++nodesCount;
                                        notUsedDescr = string.Empty;

                                        var isBursted = IsBursted(generator, n, method);
                                        if (lifecycleTrace != null) {
                                            var tracedType = systemNode.system.GetType();
                                            var count = 1;
                                            var mode = "ordinary";
                                            if (tracedType.IsGenericType) {
                                                var definition = tracedType.GetGenericTypeDefinition();
                                                var constraint = EditorUtils.GetFirstInterfaceConstraintType(definition);
                                                count = constraint == null ? 0 : EditorUtils.GetTypesDerivedFrom(constraint, definition).Length;
                                                mode = definition.GetCustomAttribute<SystemGenericParallelModeAttribute>() != null ? "parallel" : "sequential";
                                            }
                                            var preApply = systemNode.GetSyncPoint(methodEnum).syncPoint && systemNode.inputPorts.Count > 0 && systemNode.inputPorts[0].GetEdges().Count > 1;
                                            lifecycleTrace.Add(index.globalIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                                                count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + mode + "\t" +
                                                (preApply ? "1" : "0") + "\t" + (RequiresApply(systemNode) ? "1" : "0") + "\t" + (isBursted ? "1" : "0"));
                                        }
                                        if (isOpened == false || isInBurst != isBursted) {
                                            if (isOpened == true) {
                                                // close
                                                if (isInBurst == true) {
                                                    methodContent.Add("// BURST ENABLE CLOSE");
                                                } else {
                                                    methodContent.Add("// BURST DISABLE CLOSE");
                                                }

                                                if (collectedDeps.Count - prevOpenIndexDeps > 0) {
                                                    var data = $") {{";
                                                    methodContent.Insert(prevOpenIndex, data);
                                                    var dataDef = $"";
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

                                            var methodDeps = collectedDeps.GetArgString();//GetMethodDeps("ref Unity.Jobs.JobHandle", collectedDeps, 0, collectedDeps.Count);
                                            var methodDepsDef = collectedDeps.GetCallString();//GetMethodDeps("ref", collectedDeps, 0, collectedDeps.Count);
                                            var methodData = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(uint dt, in World world, ref Unity.Jobs.JobHandle dependsOn, {GetMethodDeps("System.IntPtr*", arrMethodDef, 0, arrMethodDef.Count)}, {methodDeps}";
                                            methodContent.Add($"{(isBursted == true ? "[BURST] " : string.Empty)}private static void {methodData}"); // open next
                                            var methodDef = $"InnerMethod{method}_{containers.Count}_{GetId(graph)}_{generator.GetType().Name}_{(isBursted == true ? "Burst" : "NotBurst")}(dt, in world, ref dependsOn, {GetMethodDeps("", arrMethodDef, 0, arrMethodDef.Count)}, {methodDepsDef}";
                                            containers.Add(methodDef);
                                            prevOpenIndex = methodContent.Count;
                                            prevOpenIndexDeps = collectedDeps.Count;
                                            methodContent.Add("SystemContext systemContext = default;");

                                            isOpened = true;
                                            isInBurst = isBursted;
                                        }

                                        dependsOn = AddPreApply(systemNode, index, ref schemeDependsOn, dependsOn);
                                        
                                        if (systemNode.system.GetType().IsGenericType == true) {
                                            var systemType = systemNode.system.GetType();
                                            if (systemType.IsGenericType == true) {
                                                systemType = systemType.GetGenericTypeDefinition();
                                                var genType = EditorUtils.GetFirstInterfaceConstraintType(systemType);
                                                if (genType != null) {
                                                    collectedDeps.Add($"dep{index}");
                                                    var types = EditorUtils.GetTypesDerivedFrom(genType, systemType);
                                                    if (systemType.GetCustomAttribute<SystemGenericParallelModeAttribute>() != null) {
                                                        customAttr = "[ PARALLEL ]";
                                                        // Parallel mode
                                                        var srcDep = index;
                                                        var combinedGenericDependency = $"combinedGeneric{srcDep}";
                                                        methodContent.Add($"var {combinedGenericDependency} = InvokeParallel_{index.globalIndex}_{types.Length}(dt, in world, {dependsOn}, systems);");
                                                        dependencyTrace?.Generic(combinedGenericDependency, dependsOn, index.globalIndex, types.Length, true, false);
                                                        AddApply(systemNode, srcDep, ref schemeDependsOn, combinedGenericDependency, collectedDeps.GetReadOpString($"dep{srcDep.ToString()}"));
                                                        index = srcDep;
                                                    } else {
                                                        // One-by-one mode
                                                        customAttr = "[ ONE-BY-ONE ]";
                                                        var srcDep = index;
                                                        var prevIndex = $"sequentialGeneric{srcDep}";
                                                        methodContent.Add($"var {prevIndex} = InvokeSequential_{index.globalIndex}_{types.Length}(dt, in world, {dependsOn}, systems, {(RequiresApply(systemNode) ? "true" : "false")});");
                                                        dependencyTrace?.Generic(prevIndex, dependsOn, index.globalIndex, types.Length, false, RequiresApply(systemNode));
                                                        AddApply(systemNode, srcDep, ref schemeDependsOn, prevIndex, collectedDeps.GetReadOpString($"dep{srcDep.ToString()}"));

                                                        index = srcDep;
                                                    }
                                                }
                                            }

                                        } else {

                                            collectedDeps.Add($"dep{index.ToString()}");
                                            methodContent.Add("{");
                                            methodContent.Add($"systemContext = SystemContext.Create(dt, in world, {dependsOn});");
                                            methodContent.Add($"InvokeSystem_{index.globalIndex}(systems[{index.globalIndex}], ref systemContext);");
                                            dependencyTrace?.Invoke("systemContext.dependsOn", dependsOn, index.globalIndex);

                                            AddApply(systemNode, index, ref schemeDependsOn, "systemContext.dependsOn", collectedDeps.GetReadOpString($"dep{index.ToString()}"));
                                            methodContent.Add("}");
                                            
                                        }
                                        
                                    }

                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(EditorUtils.GetTypeName(systemNode.system.GetType()), 32, CutType.End)} [{(isInBurst == true ? "  BURST  " : "NOT BURST")}]{customAttr}{notUsedDescr}");

                                } else {

                                    collectedDeps.Add($"dep{index.ToString()}");
                                    AddApply(n, index, ref schemeDependsOn, dependsOn, collectedDeps.GetReadOpString($"dep{index.ToString()}"));
                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, CutType.End)} [ SKIPPED ]");

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

                                    collectedDeps.Add($"dep{index.ToString()}");
                                    methodContent.Add($"{collectedDeps.GetReadOpString($"dep{index.ToString()}")} = {dependsOn};");
                                    dependencyTrace?.Copy(collectedDeps.GetReadOpString($"dep{index.ToString()}"), dependsOn);
                                    scheme.Add($" * {Align(schemeDependsOn, 32)} => dep{Align(index.ToString(), 16)} {Align(n.name, 32, CutType.End)} [ SKIPPED ]");
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
                                var data = $") {{";
                                innerMethods.Insert(prevOpenIndex, data);
                                var dataDef = $"";
                                containers.Add(dataDef);
                            }

                            innerMethods.Add("}"); // close last
                            containers.Add(");\n");
                        }

                    }

                    content.Insert(insertIndex, collectedDeps.GetDefinitionString());
                    /*foreach (var dep in collectedDeps) {
                        content.Insert(insertIndex, $"Unity.Jobs.JobHandle {dep} = default;");
                    }*/

                    content.AddRange(containers);

                    if (string.IsNullOrEmpty(lastDependency) == true) {
                        //content.Clear();
                    } else {
                        content.Add($"dependsOn = {lastDependency};");
                        if (dependencyTrace != null && lastDependency != "dependsOn")
                            dependencyTrace.Merge(lastDependency, lastDependencyInputs.Select(collectedDeps.GetReadOpString));
                        dependencyTrace?.Copy("dependsOn", lastDependency);
                    }

                    if (nodesCount == 0u) {
                        dependencyTrace?.Reset();
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

        public enum CutType {
            None,
            Start,
            End,
        }
        
        private static string Align(string str, int align, CutType cut = CutType.None) {

            if (str.Length <= align) {

                var builder = new System.Text.StringBuilder(str);
                builder.Append(' ', align - str.Length);
                return builder.ToString();

            } else if (cut == CutType.Start) {

                str = str.Substring(0, align - 3);
                str += "...";

            } else if (cut == CutType.End) {

                str = str.Substring(str.Length - align + 3, align - 3);
                str = $"...{str}";

            }

            return str;

        }

        private static string GetMethodDeps(string prefix, CollectedDeps collectedDeps, int i, int collectedDepsCount) {
            return collectedDeps.GetArgString();
            //return collectedDeps.Count > 0 ? (prefix + " " + string.Join($", {prefix} ", collectedDeps.ToArray(), i, collectedDepsCount)) : string.Empty;
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
                for (var index = 0; index < arr.Length; ++index) {
                    var item = arr[index];
                    collectedDeps.Add(item);
                }

                if (arr.Length == 1) {
                    result = $"{arr[0]}";
                    scheme = result;
                } else {
                    var list = string.Join(", ", arr);
                    result = $"JobsExt.CombineDependencies({list})";
                    scheme = list;
                }
            }

            return result;
        }

        private static string GetDeps(int startNodeIndex, ME.BECS.Extensions.GraphProcessor.BaseNode node, out string scheme, out string[] deps, CollectedDeps collectedDeps) {
            var result = string.Empty;
            scheme = string.Empty;
            if (node.inputPorts.Count == 0) {
                result = "dependsOn";
                collectedDeps.Add(result);
                scheme = result;
                deps = System.Array.Empty<string>();
            } else {
                var arr = node.inputPorts[0].GetEdges().Where(x => x.outputNode.graph != null)
                              .Where(x => ((SystemsGraph)node.graph).IsValidStartNodeOrOther(x.outputNode, startNodeIndex))
                              .Select(x => "dep" + GetIndex(x.outputNode, x.outputNode.graph).ToString()).Distinct().ToArray();
                var outputArr = new string[arr.Length];
                for (var index = 0; index < arr.Length; ++index) {
                    var item = arr[index];
                    collectedDeps.Add(item);
                    outputArr[index] = collectedDeps.GetReadOpString(item);
                }
                if (arr.Length == 1) {
                    deps = new[] { arr[0] };
                    result = outputArr[0];
                    scheme = arr[0];
                } else {
                    var list = string.Join(", ", outputArr);
                    deps = arr;
                    result = $"JobsExt.CombineDependencies({list})";
                    scheme = string.Join(", ", arr);
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
            if (SourceGeneratorSystemLifecycle.TryGet(type, method, out var present, out var burst, out var discarded))
                return present && !discarded && (isSystemBursted || burst);
            var methodInfo = SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(type, method);
            if (methodInfo == null) return false;
            var isBursted = System.Attribute.IsDefined(methodInfo, typeof(Unity.Burst.BurstCompileAttribute));
            var isDiscarded = System.Attribute.IsDefined(methodInfo, typeof(WithoutBurstAttribute));
            if (isDiscarded) return false;
            if (isSystemBursted == false && isBursted == false) return false;
            return true;
        }

        private static bool HasMethod(ME.BECS.Extensions.GraphProcessor.BaseNode node, string method) {
            if (node == null) return false;
            var sysNode = node as ME.BECS.FeaturesGraph.Nodes.SystemNode;
            if (sysNode == null) return true;
            if (sysNode.system == null) return false;
            var type = sysNode.system.GetType();
            if (SourceGeneratorSystemLifecycle.TryGet(type, method, out var present, out _, out _)) return present;
            return SourceGeneratorScheduledJobsValidation.GetLifecycleMethod(type, method) != null;
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
                                index += EditorUtils.GetTypesDerivedFrom(typeGen, systemNode.system.GetType()).Length;
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

        public static int GetSystemsCount(SystemsGraph graph) {
            return SourceGeneratorInputManifest.GetSystemsCount(graph);
        }

    }

}
