namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Destroy Entity")]
    public class DestroyEntity : Graph.Node {

        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        public bool destroyHierarchy;
        
        public override void Execute(Writer writer) {

            if (this.destroyHierarchy == true) {
                writer.Add($"{this.ReadInput()}.DestroyHierarchy();");
            } else {
                writer.Add($"{this.ReadInput()}.Destroy();");
            }

        }

    }

}