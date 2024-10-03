using System.Linq;

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
    
    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Logical/If")]
    public class If : Graph.Node {

        public override string style => "math-operation";

        [gp::Input(name = "A", allowMultiple = false)]
        public string a;
        [gp::Input(name = "B", allowMultiple = false)]
        public string b;

        public OpIf operation;

        public string groupGuid;

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

    [System.Serializable]
    [Extensions.GraphProcessor.NodeMenuItem("Logical/If Close")]
    public class IfClose : Graph.Node {

        public virtual int InputCount => 2;
        public virtual int OutputCount => 0;
        
        public override void Execute(Writer writer) {

            writer.Add($"}}");

        }

    }

}