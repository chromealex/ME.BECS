namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Apply Entity Config")]
    public class ApplyEntityConfig : Graph.Node {

        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        [gp::Input(name = "Config", allowMultiple = false, fieldType = typeof(Config))]
        [ConfigDrawer]
        [UnityEngine.SerializeReference]
        public object config = new Config();

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

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