namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;

    [UnityEngine.Tooltip("Draw graph in gizmos.")]
    public struct DrawGizmosGraphSystem : IUpdate, IDrawGizmos {

        public bool drawGraph;
        public bool drawPath;
        public bool drawNormals;
        public bool drawNodes;

        private int drawIndex;

        public void OnUpdate(ref SystemContext context) {
            
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.O) == true) {
                ++this.drawIndex;
                if (this.drawIndex >= context.world.parent.GetSystem<BuildGraphSystem>().graphs.Length) {
                    this.drawIndex = 0;
                }

                UnityEngine.Debug.Log("Draw: " + this.drawIndex + "/" + context.world.parent.GetSystem<BuildGraphSystem>().graphs.Length);
            }
            
        }

        public void OnDrawGizmos(ref SystemContext context) {

            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);
            
            if (logicWorld.isCreated == false) return;
            
            Ent drawGraphEnt = default;
            var idx = 0;
            foreach (var graphEnt in logicWorld.GetSystem<BuildGraphSystem>().graphs) {

                if (this.drawIndex == idx) {

                    drawGraphEnt = graphEnt;
                    if (this.drawGraph == true) Graph.DrawGizmos(graphEnt, new Graph.GizmosParameters() { drawNormals = this.drawNormals, drawNodes = this.drawNodes });
                    
                }

                ++idx;

            }

            if (this.drawPath == true) {
                
                var arr = API.Query(in logicWorld, context.dependsOn).With<ME.BECS.Units.CommandGroupComponent>().ToArray();
                foreach (var group in arr) {

                    var groupAspect = group.GetAspect<ME.BECS.Units.UnitCommandGroupAspect>();
                    for (int t = 0; t < groupAspect.readTargets.Length; ++t) {

                        var target = groupAspect.readTargets[t];
                        if (target.IsAlive() == false) continue;
                        
                        var targetComponent = target.Read<TargetComponent>();
                        if (targetComponent.target.IsAlive() == false) continue;
                        if (targetComponent.graphEnt != drawGraphEnt) continue;
                        
                        var path = target.Read<TargetPathComponent>().path;
                        Graph.DrawGizmos(path, new Graph.GizmosParameters() { drawNormals = this.drawNormals, });
                        
                        UnityEngine.Gizmos.color = UnityEngine.Color.yellow;
                        UnityEngine.Gizmos.DrawWireSphere(target.Read<TargetPathComponent>().path.to, math.sqrt(PathUtils.GetGroupRadiusSqr(in groupAspect)));
                        UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                        UnityEngine.Gizmos.DrawWireSphere(target.Read<TargetPathComponent>().path.to, math.sqrt(PathUtils.GetTargetRadiusSqr(in targetComponent)));

                    }

                }

            }

        }

    }

}