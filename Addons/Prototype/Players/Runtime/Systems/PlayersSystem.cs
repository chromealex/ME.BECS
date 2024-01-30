namespace ME.BECS.Players {

    using BURST = Unity.Burst.BurstCompileAttribute;
    
    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Initialize default players")]
    public struct PlayersSystem : IAwake, IDestroy {

        public static PlayersSystem Default => new PlayersSystem() {
            playersCount = 4u,
        };
        
        public uint playersCount;
        
        private Unity.Collections.NativeArray<Ent> players;

        public void OnAwake(ref SystemContext context) {

            this.players = new Unity.Collections.NativeArray<Ent>((int)this.playersCount, Unity.Collections.Allocator.Persistent);
            var teamId = 0u;
            for (uint i = 0u; i < this.players.Length; ++i) {
                this.players[(int)i] = PlayerUtils.CreatePlayer(i, ++teamId);
            }

        }

        public PlayerAspect GetPlayerEntity(uint id) {
            return this.players[(int)id].GetAspect<PlayerAspect>();
        }

        public void OnDestroy(ref SystemContext context) {

            this.players.Dispose();

        }

    }

}