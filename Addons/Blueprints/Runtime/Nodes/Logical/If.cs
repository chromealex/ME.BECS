using System.Linq;

namespace ME.BECS.Blueprints.Nodes {

    using gp = Extensions.GraphProcessor;

    /// <summary>
    /// Defines the supported op if values.
    /// </summary>
    public enum OpIf : byte {

        /// <summary>
        /// Greater option for <c>OpIf</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute(">")]
        Greater,
        /// <summary>
        /// Greater or equal option for <c>OpIf</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute(">=")]
        GreaterOrEqual,
        /// <summary>
        /// Less option for <c>OpIf</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("<")]
        Less,
        /// <summary>
        /// Less or equal option for <c>OpIf</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("<=")]
        LessOrEqual,
        /// <summary>
        /// Equal option for <c>OpIf</c>.
        /// </summary>
        [UnityEngine.HeaderAttribute("=")]
        Equal,

    }
    
    /// <summary>
    /// Defines the blueprint operation for if.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Logical/If")]
    public class If : Graph.Node {

        /// <summary>
        /// Style used by <c>If</c>.
        /// </summary>
        public override string style => "math-operation";

        /// <summary>
        /// Endpoint or vertex <c>a</c> of the represented geometry.
        /// </summary>
        [gp::Input(name = "A", allowMultiple = false)]
        public string a;
        /// <summary>
        /// Endpoint or vertex <c>b</c> of the represented geometry.
        /// </summary>
        [gp::Input(name = "B", allowMultiple = false)]
        public string b;

        /// <summary>
        /// Operation used by <c>If</c>.
        /// </summary>
        public OpIf operation;

        /// <summary>
        /// Group guid used by <c>If</c>.
        /// </summary>
        public string groupGuid;

        /// <summary>
        /// Handles the position changed callback.
        /// </summary>
        public override bool OnPositionChanged() {
            
            var pos = new UnityEngine.Vector2(this.position.xMax, this.position.yMin);
            if (this.groupGuid != null) {
                var group = this.graph.groups.FirstOrDefault(x => x.GUID == this.groupGuid);
                if (group != null) {
                    var newRect = new UnityEngine.Rect(pos, group.position.size);
                    if (group.position != newRect) {
                        group.position = newRect;
                        return true;
                    }
                }
            }

            return false;

        }

        /// <summary>
        /// Processes if using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            writer.Add($"if ({this.GetOp(this.a, this.b)}) {{");

        }

        private string GetOp(string a, string b) {

            return this.operation switch {
                OpIf.Equal => $"{a} == {b}",
                OpIf.Greater => $"{a} > {b}",
                OpIf.GreaterOrEqual => $"{a} >= {b}",
                OpIf.Less => $"{a} < {b}",
                OpIf.LessOrEqual => $"{a} <= {b}",
                _ => string.Empty
            };

        }

    }

    /// <summary>
    /// Defines the blueprint operation for if close.
    /// </summary>
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Logical/If Close")]
    public class IfClose : Graph.Node {

        /// <summary>
        /// Input count for the associated storage.
        /// </summary>
        public virtual int InputCount => 2;
        /// <summary>
        /// Gets output count; this implementation returns <c>0</c>.
        /// </summary>
        public virtual int OutputCount => 0;
        
        /// <summary>
        /// Processes if close using the supplied job inputs.
        /// </summary>
        public override void Execute(Writer writer) {

            writer.Add($"}}");

        }

    }

}