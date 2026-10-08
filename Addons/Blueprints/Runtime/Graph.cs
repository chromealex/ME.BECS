using System.Linq;

namespace ME.BECS.Blueprints {

    using UnityEngine;
    using g = System.Collections.Generic;
    using Extensions.GraphProcessor;

    /// <summary>
    /// Stores port data for the associated blueprints API.
    /// </summary>
    public class PortData {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public string value;

    }
    
    /// <summary>
    /// Stores input data for the associated blueprints API.
    /// </summary>
    public class InputData {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public PortData[] value;

        /// <summary>
        /// Initializes <c>InputData</c> from the supplied defaults.
        /// </summary>
        public InputData() { }

        /// <summary>
        /// Stores the supplied value in input data.
        /// </summary>
        public void Set(OutputData output, int outputIndex, int index) {
            if (output == null) return;
            this.value[index] = output.value[outputIndex];
        }

    }

    /// <summary>
    /// Stores output data for the associated blueprints API.
    /// </summary>
    public class OutputData {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public PortData[] value;
        
        /// <summary>
        /// Initializes <c>OutputData</c> from the supplied count.
        /// </summary>
        public OutputData(int count) {
            this.value = new PortData[count];
        }

    }

    /// <summary>
    /// Defines base component field data used by entity processing.
    /// </summary>
    public abstract class BaseComponentField {

        /// <summary>
        /// Field name used by <c>BaseComponentField</c>.
        /// </summary>
        public string fieldName;

        /// <summary>
        /// Returns component.
        /// </summary>
        public abstract IComponentBase GetComponent();

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            
            return $"{this.GetFullName()}, field: {this.fieldName}";
            
        }

        /// <summary>
        /// Tests whether the context is valid.
        /// </summary>
        public bool IsValid() {
            if (this.GetComponent() == null || string.IsNullOrEmpty(this.fieldName) == true || (this.GetComponent().GetType().GetField(this.fieldName) == null && this.GetComponent().GetType().GetProperty(this.fieldName) == null)) return false;
            return true;
        }

        /// <summary>
        /// Tests whether the context is ref.
        /// </summary>
        public bool IsRef() {
            if (this.GetComponent().GetType().GetField(this.fieldName) != null) return true;
            var prop = this.GetComponent().GetType().GetProperty(this.fieldName);
            if (prop == null) return false;
            return prop.GetGetMethod().ReturnType.IsByRef;
        }

        /// <summary>
        /// Tests whether the value matches the requested type or condition.
        /// </summary>
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

        /// <summary>
        /// Returns full name.
        /// </summary>
        public string GetFullName() {
            var name = this.GetComponent().GetType().FullName;
            name = name.Replace("+", ".");
            return name;
        }


    }
    
    /// <summary>
    /// Defines component field data used by entity processing.
    /// </summary>
    [System.Serializable]
    public class ComponentField : BaseComponentField {

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IComponent component;

        /// <summary>
        /// Returns component.
        /// </summary>
        public override IComponentBase GetComponent() => this.component;

    }

    /// <summary>
    /// Defines static component field data used by entity processing.
    /// </summary>
    [System.Serializable]
    public class StaticComponentField : BaseComponentField {

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        [SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: true, showContent: false, runtimeAssembliesOnly: true)]
        public IConfigComponentStatic component;
        
        /// <summary>
        /// Returns component.
        /// </summary>
        public override IComponentBase GetComponent() => this.component;

    }

    /// <summary>
    /// Defines connection state and operations.
    /// </summary>
    [System.Serializable]
    public struct Connection {

        /// <summary>
        /// Starting value or source of the associated operation.
        /// </summary>
        public int from;
        /// <summary>
        /// From index used to locate the associated entry.
        /// </summary>
        public int fromIndex;
        /// <summary>
        /// Ending value or destination of the associated operation.
        /// </summary>
        public int to;
        /// <summary>
        /// To index used to locate the associated entry.
        /// </summary>
        public int toIndex;

    }

    /// <summary>
    /// Defines writer state and operations.
    /// </summary>
    public class Writer {

        /// <summary>
        /// Defines component info data used by entity processing.
        /// </summary>
        public struct ComponentInfo : System.IEquatable<ComponentInfo> {

            /// <summary>
            /// Variable name used by <c>Writer.ComponentInfo</c>.
            /// </summary>
            public string variableName;
            /// <summary>
            /// Component variable name used by <c>Writer.ComponentInfo</c>.
            /// </summary>
            public string componentVariableName;
            /// <summary>
            /// Indicates is static.
            /// </summary>
            public bool isStatic;

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public bool Equals(ComponentInfo other) {
                return this.componentVariableName == other.componentVariableName;
            }

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public override bool Equals(object obj) {
                return obj is ComponentInfo other && this.Equals(other);
            }

            /// <summary>
            /// Returns a hash code consistent with this type's equality comparison.
            /// </summary>
            public override int GetHashCode() {
                return (this.componentVariableName != null ? this.componentVariableName.GetHashCode() : 0);
            }

        }

        /// <summary>
        /// List storage used by this instance.
        /// </summary>
        public System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
        /// <summary>
        /// Variables used by <c>Writer</c>.
        /// </summary>
        public System.Collections.Generic.HashSet<string> variables = new System.Collections.Generic.HashSet<string>();
        /// <summary>
        /// Component storage or descriptors used by this operation.
        /// </summary>
        public System.Collections.Generic.Dictionary<KV, ComponentInfo> components = new System.Collections.Generic.Dictionary<KV, ComponentInfo>();
        /// <summary>
        /// Variable id used to locate the associated entry.
        /// </summary>
        public int variableId;
        /// <summary>
        /// Component id used to locate the associated entry.
        /// </summary>
        public int componentId;
        /// <summary>
        /// Warnings used by <c>Writer</c>.
        /// </summary>
        public System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();
        
        /// <summary>
        /// Adds the supplied entry to writer.
        /// </summary>
        public void Add(string str) {

            this.list.Add(str);

        }
        
        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return string.Join("\n", this.list);
        }

        /// <summary>
        /// Defines kv state and operations for <c>Writer</c>.
        /// </summary>
        public struct KV : System.IEquatable<KV> {

            /// <summary>
            /// Entity processed or represented by this value.
            /// </summary>
            public string entity;
            /// <summary>
            /// Type descriptor used by the associated operation.
            /// </summary>
            public System.Type type;

            /// <summary>
            /// Initializes <c>KV</c> from the supplied entity, type.
            /// </summary>
            public KV(string entity, System.Type type) {
                this.entity = entity;
                this.type = type;
            }

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public bool Equals(KV other) {
                return this.entity == other.entity && Equals(this.type, other.type);
            }

            /// <summary>
            /// Tests equality using the identity or value comparison defined by this type.
            /// </summary>
            public override bool Equals(object obj) {
                return obj is KV other && this.Equals(other);
            }

            /// <summary>
            /// Returns a hash code consistent with this type's equality comparison.
            /// </summary>
            public override int GetHashCode() {
                var hash = 34;
                if (string.IsNullOrEmpty(this.entity) == false) hash ^= this.entity.GetHashCode() + 17;
                hash ^= this.type.GetHashCode();
                return hash;
            }

        }

        /// <summary>
        /// Creates <c>string</c> using the supplied creation arguments.
        /// </summary>
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
        
        /// <summary>
        /// Adds get component.
        /// </summary>
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

        /// <summary>
        /// Adds new op.
        /// </summary>
        public bool AddNewOp(string entity, BaseComponentField componentField, out ComponentInfo componentInfo, bool isStatic = false) {
            var component = componentField.GetComponent().GetType();
            if (entity == "ent") entity = null;
            return this.AddNewOp(entity, component, out componentInfo, isStatic, componentField.fieldName);
        }

        /// <summary>
        /// Adds new op.
        /// </summary>
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

        /// <summary>
        /// Adds warning.
        /// </summary>
        public void AddWarning(Graph.Node node, string text) {
            this.warnings.Add($"[ {node} ] {text}");
        }

        /// <summary>
        /// Tests whether the context has default component.
        /// </summary>
        public bool HasDefaultComponent(ComponentField component, out ComponentInfo componentInfo) {
            if (this.components.TryGetValue(new KV(null, component.component.GetType()), out componentInfo) == true) {
                return true;
            }
            componentInfo = default;
            return false;
        }

    }

    /// <summary>
    /// Defines the graph structure used for blueprint graph.
    /// </summary>
    public class BlueprintGraph : ME.BECS.Extensions.GraphProcessor.BaseGraph {

    }

    /// <summary>
    /// Defines a blueprint node entry in the associated graph.
    /// </summary>
    public class BlueprintNode : ME.BECS.Extensions.GraphProcessor.BaseNode {

        /// <summary>
        /// Tests whether the context is compatible.
        /// </summary>
        [ME.BECS.Extensions.GraphProcessor.IsCompatibleWithGraph]
        public static bool IsCompatible(ME.BECS.Extensions.GraphProcessor.BaseGraph graph) => graph is BlueprintGraph;

    }

    /// <summary>
    /// Provides processor operations for the associated BECS data.
    /// </summary>
    public class Processor : ProcessGraphProcessor {

        /// <summary>
        /// Whether complete behavior or state is selected.
        /// </summary>
        public System.Collections.Generic.HashSet<int> complete;
        /// <summary>
        /// Writer used by <c>Processor</c>.
        /// </summary>
        public Writer writer;

        /// <summary>
        /// Initializes <c>Processor</c> from the supplied graph.
        /// </summary>
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
        
        /// <summary>
        /// Executes the configured work using the supplied inputs.
        /// </summary>
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

    /// <summary>
    /// Defines the graph structure used for graph.
    /// </summary>
    [CreateAssetMenu(menuName = "ME.BECS/Blueprints/Graph")]
    public class Graph : BlueprintGraph {

        /// <summary>
        /// Defines the supported system job type values.
        /// </summary>
        public enum SystemJobType {

            /// <summary>
            /// Single option for <c>Graph.SystemJobType</c>.
            /// </summary>
            Single,
            /// <summary>
            /// Parallel option for <c>Graph.SystemJobType</c>.
            /// </summary>
            Parallel,

        }
        
        /// <summary>
        /// Defines a blueprint graph node entry in the associated graph.
        /// </summary>
        [System.Serializable]
        public abstract class BlueprintGraphNode {}
        
        /// <summary>
        /// Defines the blueprint operation for node.
        /// </summary>
        [System.Serializable]
        public abstract class Node : BlueprintNode {

            /// <summary>
            /// Defines node link state and operations for <c>Graph.Node</c>.
            /// </summary>
            public class NodeLink { }

            /// <summary>
            /// Node input used by <c>Graph.Node</c>.
            /// </summary>
            [Input(name = "Node", allowMultiple = false, optional = true)]
            public NodeLink nodeInput;
            
            /// <summary>
            /// Node output used by <c>Graph.Node</c>.
            /// </summary>
            [Output(name = "Node", allowMultiple = false, optional = true)]
            public NodeLink nodeOutput;

            /// <summary>
            /// Identifier used to address this entry within its containing registry.
            /// </summary>
            [HideInInspector]
            public int id;

            /// <summary>
            /// Writer used by <c>Graph.Node</c>.
            /// </summary>
            public Writer writer;
            /// <summary>
            /// Whether complete behavior or state is selected.
            /// </summary>
            public System.Collections.Generic.HashSet<int> complete;
            
            /// <summary>
            /// Gets is enableable; this implementation returns <c>false</c>.
            /// </summary>
            public override bool isEnableable => false;

            /// <summary>
            /// Processes the supplied inputs using this implementation.
            /// </summary>
            protected override void Process() {
                
                base.Process();
                
                this.Execute(this.writer);
                this.complete.Add(this.id);

            }

            /// <summary>
            /// Processes node using the supplied job inputs.
            /// </summary>
            public abstract void Execute(Writer writer);

            /// <summary>
            /// Initializes ports.
            /// </summary>
            public override void InitializePorts() {
                
                base.InitializePorts();
                this.OnCreated();
                
            }

            /// <summary>
            /// Handles the created callback.
            /// </summary>
            public void OnCreated() {
                if (this.id == 0 && this.graph is Graph graph) this.id = graph.GetNextId();
            }

            /// <summary>
            /// Emits this blueprint node's operation into the supplied writer.
            /// </summary>
            public void Do(Writer writer) {
                this.Execute(writer);
            }

        }

        /// <summary>
        /// Defines line state and operations for <c>Graph</c>.
        /// </summary>
        [System.Serializable]
        public struct Line {

            /// <summary>
            /// Node processed or represented by this entry.
            /// </summary>
            [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(unmanagedTypes: false, runtimeAssembliesOnly: true, showLabel: false)]
            [SerializeReference]
            public BlueprintGraphNode node;

        }
        
        /// <summary>
        /// Lines used by <c>Graph</c>.
        /// </summary>
        public Line[] lines;
        /// <summary>
        /// System type used by <c>Graph</c>.
        /// </summary>
        public SystemJobType systemType = SystemJobType.Parallel;
        /// <summary>
        /// Next id used to locate the associated entry.
        /// </summary>
        public int nextId;
        /// <summary>
        /// Connections used by <c>Graph</c>.
        /// </summary>
        public Connection[] connections;
        /// <summary>
        /// Whether debug behavior or state is selected.
        /// </summary>
        public bool debug;

        /// <summary>
        /// Returns next ID.
        /// </summary>
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

        /// <summary>
        /// Generates output from the supplied source data.
        /// </summary>
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
