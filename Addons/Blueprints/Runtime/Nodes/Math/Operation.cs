namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the supported op type values.
    /// </summary>
    public enum OpType : byte {

        /// <summary>
        /// Add option for <c>OpType</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("+")]
        Add,
        /// <summary>
        /// Subtract option for <c>OpType</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("\u2212")]
        Subtract,
        /// <summary>
        /// Multiply option for <c>OpType</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("*")]
        Multiply,
        /// <summary>
        /// Divide option for <c>OpType</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("\u00F7")]
        Divide,
        /// <summary>
        /// Power option for <c>OpType</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("^")]
        Power,

    }

    /// <summary>
    /// Defines the blueprint operation for operation.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Math/Operation")]
    public class Operation : Graph.Node {

        /// <summary>
        /// Style used by <c>Operation</c>.
        /// </summary>
        public override string style => "math-operation";

        /// <summary>
        /// X coordinate of the represented value.
        /// </summary>
        [gp::Input(name = "X", allowMultiple = false)]
        public string x;
        /// <summary>
        /// Y coordinate of the represented value.
        /// </summary>
        [gp::Input(name = "Y", allowMultiple = false)]
        public string y;

        /// <summary>
        /// Result produced by the associated operation.
        /// </summary>
        [gp::Output(name = "Result", allowMultiple = true)]
        public string result;

        /// <summary>
        /// Operation used by <c>Operation</c>.
        /// </summary>
        public OpType operation;
        
        /// <summary>
        /// Processes operation using the supplied job inputs.
        /// </summary>
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