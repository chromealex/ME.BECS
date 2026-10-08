namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines write to component data used by entity processing.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Write to Component")]
    public class WriteToComponent : Graph.Node {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        [gp::Input(name = "Value", allowMultiple = false)]
        public string value;

        /// <summary>
        /// Entity processed or represented by this value.
        /// </summary>
        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput(out bool result) {
            result = string.IsNullOrEmpty(this.entity) == true || this.entity == "ent";
            if (result == false) return this.entity;
            return string.Empty;
        }

        /// <summary>
        /// Component data accessed by this instance.
        /// </summary>
        public ComponentField component;
        
        /// <summary>
        /// Processes write to component using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            if (this.component.IsValid() == false) {
                writer.AddWarning(this, $"Component is not valid ({this.component.ToString()})");
                return;
            }

            var ent = this.ReadInput(out var res);
            if (res == false) {
                var op = this.value;
                var name = writer.AddGetComponent(null, this.component.component.GetType(), null, isStatic: false);
                if (name.variableName != null) {
                    writer.Add($"{name.variableName} = {op};");
                } else {
                    writer.Add($"{name.componentVariableName}.{this.component.fieldName} = {op};");
                }
            } else {
                var op = this.value;
                var name = writer.AddGetComponent(ent, this.component.component.GetType(), null, isStatic: false);
                if (name.variableName != null) {
                    writer.Add($"{name.variableName} = {op};");
                } else {
                    writer.Add($"ent.Get<{this.component.GetFullName()}>().{this.component.fieldName} = {op};");
                }
            }

        }

    }

}