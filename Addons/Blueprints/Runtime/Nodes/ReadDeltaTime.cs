namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for read delta time.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Read DeltaTime")]
    public class ReadDeltaTime : Graph.Node {

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        /// <summary>
        /// Processes read delta time using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            var op = writer.New("dt");
            writer.Add($"var {op} = this.systemContext.deltaTime;");
            this.result = op;
            
        }

    }

}