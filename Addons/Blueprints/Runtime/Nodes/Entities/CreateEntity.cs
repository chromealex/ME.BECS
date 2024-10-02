namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Create Entity")]
    public class CreateEntity : Graph.Node {

        [gp::Output(name = "Entity", allowMultiple = true, fieldType = typeof(Ent))]
        public string entity;

        public override void Execute(Writer writer) {

            var op = writer.New("ent");
            writer.Add($"var {op} = Ent.New(in jobInfo);");
            this.entity = op;

        }

    }

}