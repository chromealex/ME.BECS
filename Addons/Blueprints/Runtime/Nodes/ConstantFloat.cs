namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for constant float.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Constant Float")]
    public class ConstantFloat : Graph.Node {

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public float value;

        /// <summary>
        /// Processes constant float using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            var op = writer.New();
            writer.Add($"var {op} = {this.value.ToString(System.Globalization.CultureInfo.InvariantCulture)};");
            this.result = op;
            
        }

    }

}