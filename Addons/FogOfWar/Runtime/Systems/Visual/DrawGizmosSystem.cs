namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using ME.BECS.Players;
    using ME.BECS.Pathfinding;

    [BURST(CompileSynchronously = true)]
    public struct DrawGizmosSystem : IDrawGizmos {

        public bool drawGizmos;
        
        public void OnDrawGizmos(ref SystemContext context) {

            if (this.drawGizmos == false) return;
            
            var playersSystem = context.world.GetSystem<PlayersSystem>();
            var activePlayer = playersSystem.GetActivePlayer();
            var fow = activePlayer.readTeam.Read<FogOfWarComponent>();
            var props = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();
            var notExplored = UnityEngine.Color.black;
            notExplored.a = 0.8f;
            var explored = UnityEngine.Color.black;
            explored.a = 0.3f;
            for (uint x = 0u; x < props.size.x; ++x) {
                for (uint y = 0u; y < props.size.y; ++y) {
                    var worldPos = FogOfWarUtils.FogMapToWorldPosition(in props, new uint2(x, y));
                    var isVisible = FogOfWarUtils.IsVisible(in props, in fow, x, y) == true;
                    var isExplored = FogOfWarUtils.IsExplored(in props, in fow, x, y) == true;
                    UnityEngine.Gizmos.color = UnityEngine.Color.Lerp(UnityEngine.Color.Lerp(UnityEngine.Color.clear, explored, isExplored ? 1f: 0f), notExplored, isVisible ? 0f : 1f);
                    UnityEngine.Gizmos.DrawCube(worldPos, new float3(0.5f, 0.5f, 0.5f));
                }
            }

        }

    }

}