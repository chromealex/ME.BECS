namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for apply entity config.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Apply Entity Config")]
    public class ApplyEntityConfig : Graph.Node {

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        /// <summary>
        /// Configuration supplying values for this instance.
        /// </summary>
        [gp::Input(name = "Config", allowMultiple = false, fieldType = typeof(Config))]
        [ConfigDrawer]
        [UnityEngine.SerializeReference]
        public object config = new Config();

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        /// <summary>
        /// Processes apply entity config using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.config is Config config) {

                if (config.IsValid == false) return;

                var ent = this.ReadInput();
                writer.Add($"EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId({config.sourceId}).Apply(in {ent});");

            } else if (this.config is string configStr) {
                
                var ent = this.ReadInput();
                writer.Add($"EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId({configStr.Replace("{ent}", ent)}).Apply(in {ent});");
                
            } else {
                
                writer.AddWarning(this, $"{this.config} is not a valid config");
                
            }

        }

    }

}