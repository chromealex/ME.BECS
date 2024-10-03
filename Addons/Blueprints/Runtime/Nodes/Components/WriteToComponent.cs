namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Write to Component")]
    public class WriteToComponent : Graph.Node {

        [gp::Input(name = "Value", allowMultiple = false)]
        public string value;

        [gp::Input(name = "Entity", allowMultiple = false, optional = true, fieldType = typeof(Ent))]
        public string entity = "ent";

        private string ReadInput(out bool result) {
            result = string.IsNullOrEmpty(this.entity) == true || this.entity == "ent";
            if (result == false) return this.entity;
            return string.Empty;
        }

        public ComponentField component;
        
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