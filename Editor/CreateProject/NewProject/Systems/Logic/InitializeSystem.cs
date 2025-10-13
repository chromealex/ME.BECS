using UnityEngine;
using ME.BECS;
using ME.BECS.Network;
using ME.BECS.Players;
using ME.BECS.Trees;
using Unity.Burst;
using ME.BECS.FixedPoint;

namespace NewProject {
    
    public struct InitializeSystem : IAwake, IStart {

        public static InitializeSystem Default => new InitializeSystem() {
            mapPosition = new float2(0f, 0f),
            mapSize = new float2(200f, 200f),
        };
        
        private float2 mapPosition;
        private float2 mapSize;

        public void OnAwake(ref SystemContext context) {
            
            // we need to complete dependencies to be sure all previous jobs are done.
            // in this case it doesn't required, but we leave it here because of the future steps.
            context.dependsOn.Complete();
            
            var world = context.world;
            ref var qt = ref world.GetSystem<QuadTreeInsertSystem>();
            qt.mapPosition = this.mapPosition;
            qt.mapSize = this.mapSize;

        }

        public void OnStart(ref SystemContext context) {
            
            // we need to complete dependencies to be sure all previous jobs are done
            context.dependsOn.Complete();
            
            var world = context.world;
            ref var playersSystem = ref world.GetSystem<PlayersSystem>();
            ref var qt = ref world.GetSystem<QuadTreeInsertSystem>();

            qt.mapPosition = this.mapPosition;
            qt.mapSize = this.mapSize;

            // keep in mind that first player (index = 0) is always neutral player (or map player)
            for (uint i = 0u; i < playersSystem.playersCount; ++i) {
                var player = playersSystem.GetPlayerEntity(i);
                var treeIndex = qt.AddTree();
                player.unitsTreeIndex = treeIndex;
            }
            
            // update targets mask
            for (uint pid = 0u; pid < playersSystem.playersCount; ++pid) {
                var player = playersSystem.GetPlayerEntity(pid);
                player.unitsOthersTreeMask = playersSystem.GetPlayerOthersTreeMask(in player);
            }

            playersSystem.UpdateTeams();
            
        }

    }
    
}