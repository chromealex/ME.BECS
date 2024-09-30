namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    public enum OpType : byte {

        [UnityEngine.HeaderAttribute("+")]
        Add,
        [UnityEngine.HeaderAttribute("\u2212")]
        Subtract,
        [UnityEngine.HeaderAttribute("*")]
        Multiply,
        [UnityEngine.HeaderAttribute("\u00F7")]
        Divide,
        [UnityEngine.HeaderAttribute("^")]
        Power,

    }

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Math/Operation")]
    public class Operation : Graph.Node {

        public override string style => "math-operation";

        [gp::Input(name = "X", allowMultiple = false)]
        public float x;
        [gp::Input(name = "Y", allowMultiple = false)]
        public float y;

        [gp::Output(name = "Result", allowMultiple = false)]
        public float result;

        public OpType operationType;
        
        public override int InputCount => 2;
        public override int OutputCount => 1;

        public override void Execute(Writer writer) {

            var op = writer.New();
            writer.Add($"var {op} = {this.GetOp(this.input.value[0].value, this.input.value[1].value)};");
            this.output.value[0] = new ME.BECS.Blueprints.PortData() {
                value = op,
            };
            
        }

        private string GetOp(string v1, string v2) {

            if (this.operationType == OpType.Add) {
                return $"{v1} + {v2}";
            } else if (this.operationType == OpType.Subtract) {
                return $"{v1} - {v2}";
            } else if (this.operationType == OpType.Multiply) {
                return $"{v1} * {v2}";
            } else if (this.operationType == OpType.Divide) {
                return $"{v1} / {v2}";
            } else if (this.operationType == OpType.Power) {
                return $"math.pow({v1}, {v2})";
            }

            return string.Empty;

        }

    }

}