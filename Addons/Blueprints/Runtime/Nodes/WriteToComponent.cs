namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Write to Component")]
    public class WriteToComponent : Graph.Node {

        [gp::Input(name = "Value", allowMultiple = false)]
        public float value;

        public ComponentField component;
        
        public override int InputCount => 1;
        public override int OutputCount => 0;

        public override void Execute(Writer writer) {

            var op = this.input.value[0].value;
            var name = writer.AddGetComponent(this.component.component.GetType(), null);
            if (name.variableName != null) {
                writer.Add($"{name.variableName} = {op};");
            } else {
                writer.Add($"{name.componentVariableName}.{this.component.fieldName} = {op};");
            }

        }

    }

}