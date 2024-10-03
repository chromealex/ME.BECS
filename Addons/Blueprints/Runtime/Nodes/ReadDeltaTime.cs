namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read DeltaTime")]
    public class ReadDeltaTime : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        public override void Execute(Writer writer) {

            var op = writer.New("dt");
            writer.Add($"var {op} = this.systemContext.deltaTime;");
            this.result = op;
            
        }

    }

}