#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections;

    [UnityEngine.Tooltip("Draw graph in gizmos.")]
    public struct DrawGizmosGraphSystem : IUpdate, IDrawGizmos {

        public bool drawGraph;
        public bool drawPath;
        public bool drawNormals;
        public bool drawNodes;
        public bool drawPortals;

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
            for (uint i = 0u; i < logicWorld.GetSystem<BuildGraphSystem>().graphs.Length; ++i) {
                
                Ent graphEnt = logicWorld.GetSystem<BuildGraphSystem>().graphs[logicWorld.state, i];
                if (this.drawIndex == idx) {

                    drawGraphEnt = graphEnt;
                    if (this.drawGraph == true) Graph.DrawGizmos(graphEnt, new Graph.GizmosParameters() { drawNormals = this.drawNormals, drawNodes = this.drawNodes, drawPortals = this.drawPortals, });
                    
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
                        var pathTarget = target.Read<TargetPathComponent>().path.to;
                        if (pathTarget.type == Path.Target.TargetType.Point) {
                            UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)pathTarget.center, (float)math.sqrt(PathUtils.GetGroupRadiusSqr(in groupAspect)));
                            UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                            UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)pathTarget.center, (float)math.sqrt(PathUtils.GetTargetRadiusSqr(in targetComponent)));
                        } else if (pathTarget.type == Path.Target.TargetType.Radius) {
                            UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)pathTarget.center, (float)math.sqrt(PathUtils.GetGroupRadiusSqr(in groupAspect)) + (float)pathTarget.radius);
                            UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                            UnityEngine.Gizmos.DrawWireSphere((UnityEngine.Vector3)pathTarget.center, (float)math.sqrt(PathUtils.GetTargetRadiusSqr(in targetComponent)) + (float)pathTarget.radius);
                        } else if (pathTarget.type == Path.Target.TargetType.Rect) {
                            {
                                var r = math.sqrt(PathUtils.GetGroupRadiusSqr(in groupAspect));
                                UnityEngine.Gizmos.DrawWireCube((UnityEngine.Vector3)pathTarget.center, (UnityEngine.Vector3)new float3(pathTarget.size.x + r, 0f, pathTarget.size.y + r));
                            }
                            UnityEngine.Gizmos.color = UnityEngine.Color.cyan;
                            {
                                var r = math.sqrt(PathUtils.GetTargetRadiusSqr(in targetComponent));
                                UnityEngine.Gizmos.DrawWireCube((UnityEngine.Vector3)pathTarget.center, (UnityEngine.Vector3)new float3(pathTarget.size.x + r, 0f, pathTarget.size.y + r));
                            }
                        }

                    }

                }

            }

        }

    }

}