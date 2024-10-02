namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Constant Float")]
    public class ConstantFloat : Graph.Node {

        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        public float value;

        public override void Execute(Writer writer) {

            var op = writer.New();
            writer.Add($"var {op} = {this.value.ToString(System.Globalization.CultureInfo.InvariantCulture)};");
            this.result = op;
            
        }

    }

}