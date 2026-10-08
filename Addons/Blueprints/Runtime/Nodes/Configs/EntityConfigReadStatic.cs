namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;
    using scg = System.Collections.Generic;
    using ME.BECS.Extensions.GraphProcessor;

    /// <summary>
    /// Defines the blueprint operation for entity config read static.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("EntityConfig/Read Static")]
    public class EntityConfigReadStatic : Graph.Node {

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

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

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public StaticComponentField component;

        private string ReadInput() {
            return string.IsNullOrEmpty(this.entity) == false ? this.entity : "ent";
        }

        /// <summary>
        /// Processes entity config read static using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.config is Config config) {

                if (config.IsValid == false) return;

                var op = writer.New(this.component.fieldName);
                writer.Add($"var {op} = {this.config}.ReadStatic<{this.component.GetFullName()}>().{this.component.fieldName};");
                this.result = op;

            } else if (this.config is string configStr) {
                
                var op = writer.New(this.component.fieldName);
                writer.Add($"var {op} = {configStr.Replace("{ent}", this.ReadInput())}.ReadStatic<{this.component.GetFullName()}>().{this.component.fieldName};");
                this.result = op;
                
            } else {
                
                writer.AddWarning(this, $"{this.config} is not a valid config");
                
            }
            
        }

    }

}