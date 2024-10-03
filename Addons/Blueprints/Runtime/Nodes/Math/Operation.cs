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
        public string x;
        [gp::Input(name = "Y", allowMultiple = false)]
        public string y;

        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        public OpType operation;
        
        public override void Execute(Writer writer) {

            var op = writer.New(this.operation.ToString().Substring(0, 3));
            writer.Add($"var {op} = {this.GetOp(this.x, this.y)};");
            this.result = op;
            
        }

        private string GetOp(string v1, string v2) {

            return this.operation switch {
                OpType.Add => $"{v1} + {v2}",
                OpType.Subtract => $"{v1} - {v2}",
                OpType.Multiply => $"{v1} * {v2}",
                OpType.Divide => $"{v1} / {v2}",
                OpType.Power => $"math.pow({v1}, {v2})",
                _ => string.Empty
            };

        }

    }

}