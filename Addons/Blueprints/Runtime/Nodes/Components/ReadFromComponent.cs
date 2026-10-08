namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;
    using scg = System.Collections.Generic;
    using ME.BECS.Extensions.GraphProcessor;

    /// <summary>
    /// Defines ent read state and operations.
    /// </summary>
    [System.Serializable]
    public class EntRead : Graph.BlueprintGraphNode {

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public ComponentField component;

    }

    /// <summary>
    /// Defines read from component data used by entity processing.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read from Component")]
    public class ReadFromComponent : Graph.Node {

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput(out bool isDefaultEnt) {
            isDefaultEnt = false;
            if (string.IsNullOrEmpty(this.entity) == false) return this.entity;
            isDefaultEnt = true;
            return "ent";
        }

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public ComponentField component;

        /// <summary>
        /// Describes the output ports exposed by this graph node.
        /// </summary>
        [CustomPortOutput("result", typeof(Config))]
        public void CustomOutputConfig(scg::List<SerializableEdge> edges, NodePort outputPort) {

            if (this.component.Is<Config>() == true) {
                foreach (var edge in edges) edge.passThroughBuffer = $"{{ent}}.Read<{this.component.GetFullName()}>().{this.component.fieldName}";
            } else {
                foreach (var edge in edges) edge.passThroughBuffer = this.result;
            }
            
        }

        /// <summary>
        /// Processes read from component using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.component.IsValid() == false) {
                writer.AddWarning(this, $"Component is not valid ({this.component.ToString()})");
                return;
            }

            if (writer.AddNewOp(this.entity, this.component, out var name) == false) {
                var ent = this.ReadInput(out var isDefaultEnt);
                var compName = $"{ent}.Read<{this.component.GetFullName()}>()";
                if (isDefaultEnt == true) {
                    if (writer.HasDefaultComponent(this.component, out var compInfo) == true) {
                        compName = $"{compInfo.componentVariableName}";
                    }
                }

                if (this.component.IsRef() == true) {
                    writer.Add($"ref readonly var {name.variableName} = ref {compName}.{this.component.fieldName};");
                } else {
                    writer.Add($"var {name.variableName} = {compName}.{this.component.fieldName};");
                }
            }
            this.result = name.variableName;
            
        }

    }

}