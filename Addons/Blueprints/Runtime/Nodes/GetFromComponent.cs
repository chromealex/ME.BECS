namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    public enum OpIf : byte {

        [UnityEngine.HeaderAttribute(">")]
        Greater,
        [UnityEngine.HeaderAttribute(">=")]
        GreaterOrEqual,
        [UnityEngine.HeaderAttribute("<")]
        Less,
        [UnityEngine.HeaderAttribute("<=")]
        LessOrEqual,
        [UnityEngine.HeaderAttribute("=")]
        Equal,

    }

    /*[System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Get from Component")]
    public class GetFromComponent : Graph.Node {

        [gp::Input(name = "Value", allowMultiple = false)]
        public float value;

        [gp::Output(name = "Result", allowMultiple = false)]
        public float result;

        public OpIf operation;
        
        public override int InputCount => 1;
        public override int OutputCount => 1;

        public override void Execute(Writer writer) {

            var op = writer.New();
            var name = writer.AddGetComponent(this.component.component.GetType(), op);
            writer.Add($"ref var {op} = ref {name.componentVariableName}.{this.component.fieldName};");
            this.output.value[0] = new PortData() {
                value = op,
            };
            
        }

    }*/

}