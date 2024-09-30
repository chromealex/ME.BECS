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
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false)]
        public IComponent component;
        public string fieldName;

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

        }

        public System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> variables = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.Dictionary<System.Type, ComponentInfo> components = new System.Collections.Generic.Dictionary<System.Type, ComponentInfo>();
        public int variableId;
        public int componentId;
        
        public string New() {

            ++this.variableId;
            var op = "v" + this.variableId;
            this.variables.Add(op);
            return op;

        }
        
        public void Add(string str) {

            this.list.Add(str);

        }
        
        public override string ToString() {
            return string.Join("\n", this.list);
        }

        public ComponentInfo AddGetComponent(System.Type type, string variableName) {
            if (this.components.TryGetValue(type, out var info) == true) {
                if (info.variableName != null && variableName == null) {
                    return info;
                }
                this.components.Remove(type);
                info.variableName = variableName;
            } else {
                info = new ComponentInfo() {
                    variableName = variableName,
                    componentVariableName = $"comp{(++this.componentId)}",
                };
            }

            this.components.Add(type, info);
            return info;
        }

    }

    public class BlueprintGraph : ME.BECS.Extensions.GraphProcessor.BaseGraph {

    }

    public class BlueprintNode : ME.BECS.Extensions.GraphProcessor.BaseNode {

        [ME.BECS.Extensions.GraphProcessor.IsCompatibleWithGraph]
        public static bool IsCompatible(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) => graph is BlueprintGraph;

    }

    [CreateAssetMenu(menuName = "ME.BECS/Blueprints/Graph")]
    public class Graph : BlueprintGraph {

        public enum SystemJobType {

            Single,
            Parallel,

        }
        
        [System.Serializable]
        public abstract class Node : BlueprintNode {

            [HideInInspector]
            public int id;
            public InputData input;
            public OutputData output;

            public override bool isEnableable => false;

            public abstract int InputCount { get; }
            public abstract int OutputCount { get; }
            
            public abstract void Execute(Writer writer);

            public override void InitializePorts() {
                
                base.InitializePorts();
                this.OnCreated();
                
            }

            public void OnCreated() {
                if (this.id == 0 && this.graph is Graph graph) this.id = graph.GetNextId();
            }

            public void Set(Node from, int outputIndex, int inputIndex) {
                this.input.Set(from?.output, outputIndex, inputIndex);
            }

            public void Do(Writer writer) {
                this.Execute(writer);
            }

            public void Reset() {
                this.input = new InputData() { value = new PortData[this.InputCount] };
                this.output = new OutputData(this.OutputCount);
            }

        }

        public SystemJobType systemType = SystemJobType.Parallel;
        public int nextId;
        public Connection[] connections;

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
                n.Reset();
                queue.Enqueue(n);
            }

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
                            node.Set(prevNode, 0, conn.toIndex);
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
            }

            return writer;

        }

    }

}
