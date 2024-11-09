using System.Linq;

namespace ME.BECS.Blueprints {

    using UnityEngine;
    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    public class PortData {

        public string value;

    }
    
    public class InputData {

        public PortData[] value;

        public InputData() { }

        public void Set(OutputData output, int outputIndex, int index) {
            if (output == null) return;
            this.value[index] = output.value[outputIndex];
        }

    }

    public class OutputData {

        public PortData[] value;
        
        public OutputData(int count) {
            this.value = new PortData[count];
        }

    }

    public abstract class BaseComponentField {

        public string fieldName;

        public abstract IComponentBase GetComponent();

        public override string ToString() {
            
            return $"{this.GetFullName()}, field: {this.fieldName}";
            
        }

        public bool IsValid() {
            if (this.GetComponent() == null || string.IsNullOrEmpty(this.fieldName) == true || (this.GetComponent().GetType().GetField(this.fieldName) == null && this.GetComponent().GetType().GetProperty(this.fieldName) == null)) return false;
            return true;
        }

        public bool IsRef() {
            if (this.GetComponent().GetType().GetField(this.fieldName) != null) return true;
            var prop = this.GetComponent().GetType().GetProperty(this.fieldName);
            if (prop == null) return false;
            return prop.GetGetMethod().ReturnType.IsByRef;
        }

        public bool Is<T>() {
            if (this.IsValid() == false) return false;
            var type = this.GetComponent().GetType();
            System.Type t = null;
            if (type.GetField(this.fieldName) != null) {
                t = type.GetField(this.fieldName)?.FieldType;
            }
            if (type.GetProperty(this.fieldName) != null) {
                t = type.GetProperty(this.fieldName)?.PropertyType;
            }
            if (t == null) return false;
            return typeof(T).IsAssignableFrom(t);
        }

        public string GetFullName() {
            var name = this.GetComponent().GetType().FullName;
            name = name.Replace("+", ".");
            return name;
        }


    }
    
    [System.Serializable]
    public class ComponentField : BaseComponentField {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IComponent component;

        public override IComponentBase GetComponent() => this.component;

    }

    [System.Serializable]
    public class StaticComponentField : BaseComponentField {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IConfigComponentStatic component;
        
        public override IComponentBase GetComponent() => this.component;

    }

    [System.Serializable]
    public struct Connection {

        public int from;
        public int fromIndex;
        public int to;
        public int toIndex;

    }

    public class Writer {

        public struct ComponentInfo : System.IEquatable<ComponentInfo> {

            public string variableName;
            public string componentVariableName;
            public bool isStatic;

            public bool Equals(ComponentInfo other) {
                return this.componentVariableName == other.componentVariableName;
            }

            public override bool Equals(object obj) {
                return obj is ComponentInfo other && this.Equals(other);
            }

            public override int GetHashCode() {
                return (this.componentVariableName != null ? this.componentVariableName.GetHashCode() : 0);
            }

        }

        public System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.HashSet<string> variables = new System.Collections.Generic.HashSet<string>();
        public System.Collections.Generic.Dictionary<KV, ComponentInfo> components = new System.Collections.Generic.Dictionary<KV, ComponentInfo>();
        public int variableId;
        public int componentId;
        public System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();
        
        public void Add(string str) {

            this.list.Add(str);

        }
        
        public override string ToString() {
            return string.Join("\n", this.list);
        }

        public struct KV : System.IEquatable<KV> {

            public string entity;
            public System.Type type;

            public KV(string entity, System.Type type) {
                this.entity = entity;
                this.type = type;
            }

            public bool Equals(KV other) {
                return this.entity == other.entity && Equals(this.type, other.type);
            }

            public override bool Equals(object obj) {
                return obj is KV other && this.Equals(other);
            }

            public override int GetHashCode() {
                var hash = 34;
                if (string.IsNullOrEmpty(this.entity) == false) hash ^= this.entity.GetHashCode() + 17;
                hash ^= this.type.GetHashCode();
                return hash;
            }

        }

        public string New(string prefix = "v") {

            if (string.IsNullOrEmpty(prefix) == true) prefix = "v";
            prefix = char.ToLower(prefix[0]) + prefix.Substring(1);
            
            // Try add var without index
            var op = prefix;
            if (this.variables.Add(op) == true) {
                return op;
            }
            
            ++this.variableId;
            op = prefix + this.variableId;
            this.variables.Add(op);
            return op;

        }

        private string GetNextComponentName(System.Type type) {

            var componentVariableName = type.Name;
            componentVariableName = char.ToLower(componentVariableName[0]) + componentVariableName.Substring(1);

            if (this.components.ContainsValue(new ComponentInfo() {
                    componentVariableName = componentVariableName,
                }) == true) {

                componentVariableName += (++this.componentId);

            }

            return componentVariableName;

        }
        
        public ComponentInfo AddGetComponent(string entity, System.Type type, string variableName, bool isStatic) {
            if (string.IsNullOrEmpty(entity) == true) entity = null;
            if (this.components.TryGetValue(new KV(entity, type), out var info) == true) {
                if (info.variableName != null && variableName == null) {
                    return info;
                }
                this.components.Remove(new KV(entity, type));
                info.variableName = variableName;
                info.isStatic = isStatic;
            } else {
                info = new ComponentInfo() {
                    variableName = variableName,
                    componentVariableName = this.GetNextComponentName(type),//$"comp{(++this.componentId)}",
                    isStatic = isStatic,
                };
            }

            this.components.Add(new KV(entity, type), info);
            return info;
        }

        public bool AddNewOp(string entity, BaseComponentField componentField, out ComponentInfo componentInfo, bool isStatic = false) {
            var component = componentField.GetComponent().GetType();
            if (entity == "ent") entity = null;
            return this.AddNewOp(entity, component, out componentInfo, isStatic, componentField.fieldName);
        }

        public bool AddNewOp(string entity, System.Type type, out ComponentInfo componentInfo, bool isStatic = false, string customFieldName = null) {
            if (entity == "ent") entity = null;
            componentInfo = this.AddGetComponent(entity, type, null, isStatic);
            if (componentInfo.variableName == null) {
                var op = this.New(customFieldName ?? entity);
                componentInfo = this.AddGetComponent(entity, type, op, isStatic);
                return false;
            }
            return true;
        }

        public void AddWarning(Graph.Node node, string text) {
            this.warnings.Add($"[ {node} ] {text}");
        }

        public bool HasDefaultComponent(ComponentField component, out ComponentInfo componentInfo) {
            if (this.components.TryGetValue(new KV(null, component.component.GetType()), out componentInfo) == true) {
                return true;
            }
            componentInfo = default;
            return false;
        }

    }

    public class BlueprintGraph : ME.BECS.Extensions.GraphProcessor.BaseGraph {

    }

    public class BlueprintNode : ME.BECS.Extensions.GraphProcessor.BaseNode {

        [ME.BECS.Extensions.GraphProcessor.IsCompatibleWithGraph]
        public static bool IsCompatible(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) => graph is BlueprintGraph;

    }

    public class Processor : ProcessGraphProcessor {

        public System.Collections.Generic.HashSet<int> complete;
        public Writer writer;

        public Processor(BaseGraph graph) : base(graph) { }

        /*public override void UpdateComputeOrder() {

            foreach (var node in this.graph.nodes) {
                node.computeOrder = (int)node.position.x;
            }

            this.processList = this.graph.nodes.OrderBy(n => n.computeOrder).ToList();
                
        }*/

        private bool IsAllComplete(Graph.Node node) {
            var group = node.graph.groups.FirstOrDefault(x => x.GUID == node.groupGUID);
            var allComplete = true;
            foreach (var nodeGuid in group.innerNodeGUIDs) {
                var n = (Graph.Node)this.graph.nodesPerGUID[nodeGuid];
                if (n is ME.BECS.Blueprints.Nodes.If ifNode) {
                    if (string.IsNullOrEmpty(ifNode.groupGuid) == false) {
                        if (this.IsAllComplete(ifNode.groupGuid) == false) return false;
                    }
                }
                if (this.complete.Contains(n.id) == false) {
                    allComplete = false;
                    break;
                }
            }
            return allComplete;
        }

        private bool IsAllComplete(string groupGuid) {
            var group = this.graph.groups.FirstOrDefault(x => x.GUID == groupGuid);
            var allComplete = true;
            foreach (var nodeGuid in group.innerNodeGUIDs) {
                var n = (Graph.Node)this.graph.nodesPerGUID[nodeGuid];
                if (n is ME.BECS.Blueprints.Nodes.If ifNode) {
                    if (string.IsNullOrEmpty(ifNode.groupGuid) == false) {
                        if (this.IsAllComplete(ifNode.groupGuid) == false) return false;
                    }
                }
                if (this.complete.Contains(n.id) == false) {
                    allComplete = false;
                    break;
                }
            }
            return allComplete;
        }
        
        public override void Run() {
            
            int count = this.processList.Count;

            var openedGroups = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < count; i++) {
                var node = this.processList[i];
                node.OnProcess();
                if (string.IsNullOrEmpty(node.groupGUID) == false) {
                    if (this.IsAllComplete(node.groupGUID) == true) {
                        openedGroups.Remove(node.groupGUID);
                        this.writer.Add("}");
                    } else {
                        openedGroups.Add(node.groupGUID);
                    }
                }
            }

            for (int i = 0; i < openedGroups.Count; ++i) {
                this.writer.Add("}");
            }

        }

    }

    [CreateAssetMenu(menuName = "ME.BECS/Blueprints/Graph")]
    public class Graph : BlueprintGraph {

        public enum SystemJobType {

            Single,
            Parallel,

        }
        
        [System.Serializable]
        public abstract class BlueprintGraphNode {}
        
        [System.Serializable]
        public abstract class Node : BlueprintNode {

            public class NodeLink { }

            [Input(name = "Node", allowMultiple = false, optional = true)]
            public NodeLink nodeInput;
            
            [Output(name = "Node", allowMultiple = false, optional = true)]
            public NodeLink nodeOutput;

            [HideInInspector]
            public int id;

            public Writer writer;
            public System.Collections.Generic.HashSet<int> complete;
            
            public override bool isEnableable => false;

            protected override void Process() {
                
                base.Process();
                
                this.Execute(this.writer);
                this.complete.Add(this.id);

            }

            public abstract void Execute(Writer writer);

            public override void InitializePorts() {
                
                base.InitializePorts();
                this.OnCreated();
                
            }

            public void OnCreated() {
                if (this.id == 0 && this.graph is Graph graph) this.id = graph.GetNextId();
            }

            public void Do(Writer writer) {
                this.Execute(writer);
            }

        }

        [System.Serializable]
        public struct Line {

            [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: false, runtimeAssembliesOnly: true, showLabel: false)]
            [SerializeReference]
            public BlueprintGraphNode node;

        }
        
        public Line[] lines;
        public SystemJobType systemType = SystemJobType.Parallel;
        public int nextId;
        public Connection[] connections;
        public bool debug;

        public int GetNextId() {
            return ++this.nextId;
        }

        private Node GetNode(int id) {
            return (Node)this.nodes.FirstOrDefault(x => ((Node)x).id == id);
        }
        
        private Connection[] GetConnectionsTo(int id) {
            return this.connections.Where(x => x.to == id).ToArray();
        }

        private Connection[] GetConnectionsFrom(int id) {
            return this.connections.Where(x => x.from == id).ToArray();
        }

        private Connection GetConnection(Node prevNode, Node node) {
            return this.connections.FirstOrDefault(x => (prevNode != null ? x.from == prevNode.id : true) && x.to == node.id);
        }

        public Writer Generate() {
            
            var writer = new Writer();
            var completeNodes = new System.Collections.Generic.HashSet<int>();
            //var lookup = new System.Collections.Generic.HashSet<int>();
            //var queue = new System.Collections.Generic.Queue<Node>();
            foreach (var node in this.nodes) {
                if (node is Node n) {
                    n.writer = writer;
                    n.complete = completeNodes;
                }
                //queue.Enqueue(n);
            }

            var proc = new Processor(this);
            proc.complete = completeNodes;
            proc.writer = writer;
            proc.UpdateComputeOrder();
            proc.Run();
            if (writer.warnings.Count > 0) {
                Debug.LogWarning("Some warnings were occured while generating graph:\n" + string.Join("\n", writer.warnings));
            }
            
            /*
            var max = 100_000;
            while (queue.Count > 0) {
                if (--max == 0) return writer;
                var node = queue.Dequeue();
                {
                    var connections = this.GetConnectionsTo(node.id);
                    var isComplete = true;
                    foreach (var conn in connections) {
                        if (completeNodes.Contains(conn.from) == false) {
                            isComplete = false;
                            break;
                        }
                    }

                    if (isComplete == true && connections.Length == 0) {
                        // if no connections - we need to be sure that all elements at the left are complete
                        var currentPos = node.position.x;
                        foreach (var n in this.nodes) {
                            if (n.position.x < currentPos && completeNodes.Contains(((Node)n).id) == false) {
                                isComplete = false;
                                break;
                            }
                        }
                    }

                    if (isComplete == false) {
                        queue.Enqueue(node);
                        continue;
                    }
                    completeNodes.Add(node.id);
                }
                {
                    {
                        var connections = this.GetConnectionsTo(node.id);
                        for (int i = 0; i < connections.Length; ++i) {
                            var conn = connections[i];
                            if (lookup.Contains(conn.to) == true) continue;
                            var prevNode = this.GetNode(conn.from);
                            try {
                                node.Set(prevNode, 0, conn.toIndex);
                            } catch (System.Exception ex) {
                                Debug.LogError("Node: " + node + ", prevNode: " + prevNode);
                                throw ex;
                            }
                        }
                        if (lookup.Contains(node.id) == false) node.Do(writer);
                        lookup.Add(node.id);
                    }
                    {
                        var connections = this.GetConnectionsFrom(node.id);
                        foreach (var conn in connections) {
                            if (lookup.Contains(conn.to) == true) continue;
                            queue.Enqueue(this.GetNode(conn.to));
                        }
                    }
                }
                {
                    // End scope if all items complete in this group
                    if (string.IsNullOrEmpty(node.groupGUID) == false) {
                        var group = node.graph.groups.FirstOrDefault(x => x.GUID == node.groupGUID);
                        var allComplete = true;
                        foreach (var nodeGuid in group.innerNodeGUIDs) {
                            var n = (Node)node.graph.nodesPerGUID[nodeGuid];
                            if (completeNodes.Contains(n.id) == false) {
                                allComplete = false;
                                break;
                            }
                        }
                        if (allComplete == true) {
                            writer.Add("}");
                        }
                    }
                }
            }*/

            return writer;

        }

    }

}
