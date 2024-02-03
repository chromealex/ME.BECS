namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Mathematics;
    using Unity.Collections;

    [UnityEngine.Tooltip("Draw graph in gizmos.")]
    public struct DrawGizmosGraphSystem : IDrawGizmos {

        public bool drawGraph;
        public bool drawPath;
        public bool drawNormals;

        public void OnDrawGizmos(ref SystemContext context) {

            foreach (var graphEnt in context.world.GetSystem<BuildGraphSystem>().graphs) {

                if (this.drawGraph == true) Graph.DrawGizmos(graphEnt, new Graph.GizmosParameters() { drawNormals = this.drawNormals });

            }

            if (this.drawPath == true) {

                var arr = API.Query(in context).With<ME.BECS.Units.CommandGroupComponent>().ToArray();
                foreach (var group in arr) {

                    var groupAspect = group.GetAspect<ME.BECS.Units.UnitCommandGroupAspect>();
                    for (int t = 0; t < groupAspect.readTargets.Length; ++t) {

                        var target = groupAspect.readTargets[t];
                        if (target.IsAlive() == false) continue;
                        
                        var targetComponent = target.Read<TargetComponent>();
                        if (targetComponent.target.IsAlive() == false) continue;

                        var path = target.Read<TargetPathComponent>().path;
                        Graph.DrawGizmos(path, new Graph.GizmosParameters() { drawNormals = this.drawNormals });
                        
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