namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;
    using scg = System.Collections.Generic;
    using ME.BECS.Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read from Component")]
    public class ReadFromComponent : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        public ComponentField component;

        [CustomPortOutput("result", typeof(Config))]
        public void CustomOutputConfig(scg::List<SerializableEdge> edges, NodePort outputPort) {

            if (this.component.Is<Config>() == true) {
                foreach (var edge in edges) edge.passThroughBuffer = $"{{ent}}.Read<{this.component.GetFullName()}>.{this.component.fieldName}";
            } else {
                foreach (var edge in edges) edge.passThroughBuffer = this.result;
            }
            
        }

        public override void Execute(Writer writer) {

            if (this.component.IsValid() == false) return;

            if (writer.AddNewOp(this.entity, this.component.component.GetType(), out var name) == false) {
                writer.Add($"ref readonly var {name.variableName} = ref {this.ReadInput()}.Read<{this.component.GetFullName()}>.{this.component.fieldName};");
            }
            this.result = name.variableName;
            
        }

    }

}