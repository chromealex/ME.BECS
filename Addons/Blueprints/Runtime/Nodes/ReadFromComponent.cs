namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read from Component")]
    public class ReadFromComponent : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = false)]
        public float result;

        public ComponentField component;

        public override int InputCount => 0;
        public override int OutputCount => 1;

        public override void Execute(Writer writer) {

            var op = writer.New();
            writer.Add($"ref readonly var {op} = ref entity.Read<{this.component.component.GetType().FullName}>.{this.component.fieldName};");
            this.output.value[0] = new PortData() {
                value = op,
            };
            
        }

    }

}