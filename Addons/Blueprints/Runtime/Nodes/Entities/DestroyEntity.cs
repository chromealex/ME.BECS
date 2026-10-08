namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for destroy entity.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Destroy Entity")]
    public class DestroyEntity : Graph.Node {

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        /// <summary>
        /// Whether destroy hierarchy behavior or state is selected.
        /// </summary>
        public bool destroyHierarchy;
        
        /// <summary>
        /// Processes destroy entity using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.destroyHierarchy == true) {
                writer.Add($"{this.ReadInput()}.DestroyHierarchy();");
            } else {
                writer.Add($"{this.ReadInput()}.Destroy();");
            }

        }

    }

}