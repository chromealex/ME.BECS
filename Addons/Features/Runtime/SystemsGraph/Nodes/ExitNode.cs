namespace ME.BECS.FeaturesGraph.Nodes {

    using Extensions.GraphProcessor;
    using g = System.Collections.Generic;
    
    /// <summary>
    /// Defines a exit node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    public partial class ExitNode : FeaturesGraphNode {

        private partial struct ExitSystem : ISystem {}

        /// <summary>
        /// Input nodes used by <c>ExitNode</c>.
        /// </summary>
        [Input(name = "In", allowMultiple = true)]
        public g::List<SystemHandle> inputNodes;

        /// <summary>
        /// Indicates is instance.
        /// </summary>
        [UnityEngine.HideInInspector]
        public bool isInstance;

        /// <summary>
        /// Gets is renamable; this implementation returns <c>this.isInstance</c>.
        /// </summary>
        public override bool isRenamable => this.isInstance;
        /// <summary>
        /// Display or lookup name of this entry.
        /// </summary>
        public override string name => this.isInstance == true ? base.name : "EXIT";
        /// <summary>
        /// Gets is locked; this implementation returns <c>false</c>.
        /// </summary>
        public override bool isLocked => false;
        /// <summary>
        /// Gets deletable; this implementation returns <c>this.isInstance</c>.
        /// </summary>
        public override bool deletable => this.isInstance;
        /// <summary>
        /// Gets is collapsable; this implementation returns <c>false</c>.
        /// </summary>
        public override bool isCollapsable => false;
        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public override UnityEngine.Color color => new UnityEngine.Color(0.3f, 0.06f, 0.14f);
        /// <summary>
        /// Style used by <c>ExitNode</c>.
        /// </summary>
        public override string style => "exit-node";

        /// <summary>
        /// Processes the supplied inputs using this implementation.
        /// </summary>
        protected override void Process() {
            //UnityEngine.Debug.Log("EXIT NODE PLAY");
            if (this.inputNodes == null || this.inputNodes.Count == 0) return;
            var handle = (this.inputNodes.Count == 1 ? this.inputNodes[0] : this.runtimeSystemGroup.Combine(this.inputNodes));
            this.runtimeHandle = this.runtimeSystemGroup.Add<ExitSystem>(handle);
        }

        /// <summary>
        /// Returns inputs.
        /// </summary>
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