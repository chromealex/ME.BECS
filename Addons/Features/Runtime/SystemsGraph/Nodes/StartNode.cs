namespace ME.BECS.FeaturesGraph.Nodes {

    using Extensions.GraphProcessor;
    using g = System.Collections.Generic;
    
    /// <summary>
    /// Defines a start node entry in the associated graph.
    /// </summary>
    [System.Serializable]
    public partial class StartNode : FeaturesGraphNode {

        /// <summary>
        /// Root depends on used by <c>StartNode</c>.
        /// </summary>
        public SystemHandle rootDependsOn;
        
        private partial struct RootSystem : ISystem {}

        /// <summary>
        /// Output used by <c>StartNode</c>.
        /// </summary>
        [Output(name = "Out", allowMultiple = true)]
        public g::List<SystemHandle> output;

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
        public override string name => this.isInstance == true ? base.name : "START";
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
        public override UnityEngine.Color color => new UnityEngine.Color(0.06f, 0.3f, 0.14f);
        /// <summary>
        /// Style used by <c>StartNode</c>.
        /// </summary>
        public override string style => "start-node";

        /// <summary>
        /// Processes the supplied inputs using this implementation.
        /// </summary>
        protected override void Process() {
            //UnityEngine.Debug.Log("ROOT NODE PLAY");
            this.runtimeHandle = this.runtimeSystemGroup.Add<RootSystem>(this.rootDependsOn);
        }

    }

}