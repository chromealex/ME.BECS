namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;
    using scg = System.Collections.Generic;
    using ME.BECS.Extensions.GraphProcessor;

    /// <summary>
    /// Defines get from component data used by entity processing.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Get from Component")]
    public class GetFromComponent : Graph.Node {

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

        private string ReadInput(out bool result) {
            result = string.IsNullOrEmpty(this.entity) == true || this.entity == "ent";
            if (result == false) return this.entity;
            return string.Empty;
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
        /// Processes get from component using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.component.IsValid() == false) {
                writer.AddWarning(this, $"Component is not valid ({this.component.ToString()})");
                return;
            }

            if (writer.AddNewOp(this.entity, this.component, out var name) == false) {
                var ent = this.ReadInput(out var res);
                if (res == false) {
                    writer.Add($"ref var {name.variableName} = ref {ent}.Get<{this.component.GetFullName()}>().{this.component.fieldName};");
                } else {
                    writer.Add($"ref var {name.variableName} = ref {name.componentVariableName}.{this.component.fieldName};");
                }
            }
            this.result = name.variableName;
            
        }

    }

}