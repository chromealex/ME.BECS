namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for create entity.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Create Entity")]
    public class CreateEntity : Graph.Node {

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        [gp::Output(name = "Entity", allowMultiple = true, fieldType = typeof(Ent))]
        public string entity;

        /// <summary>
        /// Processes create entity using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            var op = writer.New("ent");
            writer.Add($"var {op} = Ent.New(in jobInfo);");
            this.entity = op;

        }

    }

}