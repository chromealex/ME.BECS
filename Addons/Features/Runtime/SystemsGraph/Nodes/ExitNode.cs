namespace ME.BECS.FeaturesGraph.Nodes {

    using Extensions.GraphProcessor;
    using g = System.Collections.Generic;
    
    [System.Serializable]
    public class ExitNode : FeaturesGraphNode {

        private struct ExitSystem : ISystem {}

        [Input(name = "In", allowMultiple = true)]
        public g::List<SystemHandle> inputNodes;

        [UnityEngine.HideInInspector]
        public bool isInstance;

        public override bool isRenamable => this.isInstance;
        public override string name => this.isInstance == true ? base.name : "EXIT";
        public override bool isLocked => false;
        public override bool deletable => this.isInstance;
        public override bool isCollapsable => false;
        public override UnityEngine.Color color => new UnityEngine.Color(0.3f, 0.06f, 0.14f);
        public override string style => "exit-node";

        protected override void Process() {
            //UnityEngine.Debug.Log("EXIT NODE PLAY");
            if (this.inputNodes == null || this.inputNodes.Count == 0) return;
            var handle = (this.inputNodes.Count == 1 ? this.inputNodes[0] : this.runtimeSystemGroup.Combine(this.inputNodes));
            this.runtimeHandle = this.runtimeSystemGroup.Add<ExitSystem>(handle);
        }

        [CustomPortInput(nameof(ExitNode.inputNodes), typeof(SystemHandle), allowCast = true)]
        public void GetInputs(g::List<SerializableEdge> edges) {
            var list = new System.Collections.Generic.List<SystemHandle>(edges.Count);
            foreach (var input in edges) {
                var handle = ((FeaturesGraphNode)input.outputNode).runtimeHandle;
                list.Add(handle);
            }

            this.inputNodes = list;
        }

    }

}