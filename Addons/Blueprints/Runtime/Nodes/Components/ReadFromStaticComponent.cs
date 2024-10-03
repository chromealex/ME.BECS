namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;
    using scg = System.Collections.Generic;
    using ME.BECS.Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read from Static Component")]
    public class ReadFromStaticComponent : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        public StaticComponentField component;

        [CustomPortOutput("result", typeof(Config))]
        public void CustomOutputConfig(scg::List<SerializableEdge> edges, NodePort outputPort) {

            if (this.component.Is<Config>() == true) {
                foreach (var edge in edges) edge.passThroughBuffer = $"{{ent}}.ReadStatic<{this.component.GetFullName()}>().{this.component.fieldName}";
            } else {
                foreach (var edge in edges) edge.passThroughBuffer = this.result;
            }
            
        }

        public override void Execute(Writer writer) {

            if (this.component.IsValid() == false) {
                writer.AddWarning(this, $"Component is not valid ({this.component.ToString()})");
                return;
            }

            if (writer.AddNewOp(this.entity, this.component, out var name, isStatic: true) == false) {
                writer.Add($"ref readonly var {name.variableName} = ref {this.ReadInput()}.ReadStatic<{this.component.GetFullName()}>().{this.component.fieldName};");
            }
            this.result = name.variableName;
            
        }

    }

}