#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;

    [BURST(CompileSynchronously = true)]
    public struct DrawGizmosSystem : IDrawGizmos {

        public bool drawGizmos;
        
        public void OnDrawGizmos(ref SystemContext context) {

            if (this.drawGizmos == false) return;
            
            var logicWorld = context.world.parent;
            E.IS_CREATED(logicWorld);

            var playersSystem = logicWorld.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            var fow = activePlayer.readTeam.Read<FogOfWarComponent>();
            var props = logicWorld.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();
            var notExplored = UnityEngine.Color.black;
            notExplored.a = 0.8f;
            var explored = UnityEngine.Color.black;
            explored.a = 0.3f;
            for (uint x = 0u; x < props.size.x; ++x) {
                for (uint y = 0u; y < props.size.y; ++y) {
                    var cubeSize = new float3(props.worldSize.x / props.size.x, 0.2f, props.worldSize.x / props.size.x) * 0.8f;
                    var worldPos = FogOfWarUtils.FogMapToWorldPosition(in props, new uint2(x, y));
                    var isVisible = FogOfWarUtils.IsVisible(in props, in fow, x, y) == true;
                    var isExplored = FogOfWarUtils.IsExplored(in props, in fow, x, y) == true;
                    UnityEngine.Gizmos.color = UnityEngine.Color.Lerp(UnityEngine.Color.Lerp(UnityEngine.Color.clear, explored, isVisible == true ? 0f: 1f), notExplored, isExplored == true ? 0f : 1f);
                    UnityEngine.Gizmos.DrawCube((UnityEngine.Vector3)worldPos, (UnityEngine.Vector3)cubeSize);
                }
            }

        }

    }

}