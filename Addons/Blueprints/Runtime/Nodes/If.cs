namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Logical/If")]
    public class If : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = false)]
        public float result;

        public override int InputCount => 0;
        public override int OutputCount => 1;
        
        public override void Execute(Writer writer) {

            var op = writer.New();
            writer.Add($"var {op} = this.systemContext.deltaTime;");
            this.output.value[0] = new PortData() {
                value = op,
            };
            
        }

    }

}