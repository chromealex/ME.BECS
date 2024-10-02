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

    [System.Serializable]
    public class ComponentField {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IComponent component;
        public string fieldName;

        public bool IsValid() {
            if (this.component == null || string.IsNullOrEmpty(this.fieldName) == true || this.component.GetType().GetField(this.fieldName) == null) return false;
            return true;
        }

        public bool Is<T>() {
            if (this.IsValid() == false) return false;
            return typeof(T).IsAssignableFrom(this.component.GetType().GetField(this.fieldName).FieldType);
        }

        public string GetFullName() {
            var name = this.component.GetType().FullName;
            name = name.Replace("+", ".");
            return name;
        }

    }

    [System.Serializable]
    public class StaticComponentField {

        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IConfigComponentStatic component;
        public string fieldName;

        public bool IsValid() {
            if (this.component == null || string.IsNullOrEmpty(this.fieldName) == true || this.component.GetType().GetField(this.fieldName) == null) return false;
            return true;
        }

        public bool Is<T>() {
            if (this.IsValid() == false) return false;
            return typeof(T).IsAssignableFrom(this.component.GetType().GetField(this.fieldName).FieldType);
        }

        public string GetFullName() {
            var name = this.component.GetType().FullName;
            name = name.Replace("+", ".");
            return name;
        }

    }

    [System.Serializable]
    public struct Connection {

        public int from;
        public int fromIndex;
        public int to;
        public int toIndex;

    }

    public class Writer {

        public struct ComponentInfo {

            public string variableName;
            public string componentVariableName;
            public bool isStatic;

        }

        public System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> variables = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.Dictionary<KV, ComponentInfo> components = new System.Collections.Generic.Dictionary<KV, ComponentInfo>();
        public int variableId;
        public int componentId;
        public System.Collections.Generic.List<BaseNode> scopes = new System.Collections.Generic.List<BaseNode>();
        public System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();
        
        public string New(string prefix = "v") {

            if (string.IsNullOrEmpty(prefix) == true) prefix = "v";
            
            ++this.variableId;
            var op = prefix + this.variableId;
            this.variables.Add(op);
            return op;

        }
        
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
                    componentVariableName = $"comp{(++this.componentId)}",
                    isStatic = isStatic,
                };
            }

            this.components.Add(new KV(entity, type), info);
            return info;
        }

        public void AddScope(BaseNode node, string groupGuid) {
            this.scopes.Add(node);
        }

        public bool AddNewOp(string entity, System.Type type, out ComponentInfo componentInfo, bool isStatic = false) {
            if (entity == "ent") entity = null;
            componentInfo = this.AddGetComponent(entity, type, null, isStatic);
            if (componentInfo.variableName == null) {
                var op = this.New(entity);
                componentInfo = this.AddGetComponent(entity, type, op, isStatic);
                return false;
            }
            return true;
        }

        public void AddWarning(Graph.Node node, string text) {
            this.warnings.Add($"[ {node} ] {text}");
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

        public override void Run() {
            
            int count = this.processList.Count;

            for (int i = 0; i < count; i++) {
                var node = this.processList[i];
                node.OnProcess();
                if (string.IsNullOrEmpty(node.groupGUID) == false) {
                    var group = node.graph.groups.FirstOrDefault(x => x.GUID == node.groupGUID);
                    var allComplete = true;
                    foreach (var nodeGuid in group.innerNodeGUIDs) {
                        var n = (Graph.Node)node.graph.nodesPerGUID[nodeGuid];
                        if (this.complete.Contains(n.id) == false) {
                            allComplete = false;
                            break;
                        }
                    }
                    if (allComplete == true) {
                        this.writer.Add("}");
                    }
                }

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
            var lookup = new System.Collections.Generic.HashSet<int>();
            var queue = new System.Collections.Generic.Queue<Node>();
            foreach (var node in this.nodes) {
                var n = (Node)node;
                n.writer = writer;
                n.complete = completeNodes;
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
