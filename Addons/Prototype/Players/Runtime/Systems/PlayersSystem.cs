namespace ME.BECS.Players {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    
    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Initialize default players")]
    public struct PlayersSystem : IAwake, IDestroy {

        public static PlayersSystem Default => new PlayersSystem() {
            playersCount = 4u,
        };
        
        public uint playersCount;
        
        private Unity.Collections.NativeArray<Ent> players;
        private Unity.Collections.NativeArray<Ent> teams;
        private uint activePlayer;

        public void OnAwake(ref SystemContext context) {

            this.players = new Unity.Collections.NativeArray<Ent>((int)this.playersCount, Unity.Collections.Allocator.Persistent);
            this.teams = new Unity.Collections.NativeArray<Ent>((int)this.playersCount, Unity.Collections.Allocator.Persistent);
            for (uint i = 0u; i < this.players.Length; ++i) {
                var team = Ent.New(in context.world);
                team.Set(new TeamComponent() {
                    id = i + 1u,
                });
                this.teams[(int)i] = team;
                //UnityEngine.Debug.Log("Team: " + team);
            }

            for (uint i = 0u; i < this.players.Length; ++i) {
                this.players[(int)i] = PlayerUtils.CreatePlayer(i, this.teams[(int)i], JobInfo.Create(context.world.id));
            }
            
            this.UpdateTeams();
            
            //UnityEngine.Debug.Log("Players Ready");

        }

        public void OnDestroy(ref SystemContext context) {

            this.players.Dispose();
            this.teams.Dispose();

        }
        
        public Unity.Collections.NativeArray<Ent> GetTeams() => this.teams;

        /// <summary>
        /// Call this method every time you changed player's team
        /// </summary>
        [INLINE(256)]
        public void UpdateTeams() {
            
            for (int i = 0; i < this.teams.Length; ++i) {
                var team = this.teams[i].GetAspect<TeamAspect>();
                team.unitsTreeMask = this.GetAllPlayersTreeMask(team.teamId);
                team.unitsOthersTreeMask = this.GetAllPlayersTreeMask() & ~team.unitsTreeMask;
            }

        }

        [INLINE(256)]
        public int GetAllPlayersTreeMask() {

            var mask = 0;
            for (uint i = 0u; i < this.players.Length; ++i) {
                var player = this.players[(int)i].GetAspect<PlayerAspect>();
                mask |= 1 << player.unitsTreeIndex;
            }
            return mask;

        }

        [INLINE(256)]
        public int GetAllPlayersTreeMask(uint teamId) {

            var mask = 0;
            for (uint i = 0u; i < this.players.Length; ++i) {
                var player = this.players[(int)i].GetAspect<PlayerAspect>();
                if (player.team.GetAspect<TeamAspect>().teamId == teamId) {
                    mask |= 1 << player.unitsTreeIndex;
                }
            }
            return mask;

        }

        [INLINE(256)]
        public int GetPlayerTreeMask(in PlayerAspect playerAspect) {

            return (1 << playerAspect.unitsTreeIndex);

        }

        [INLINE(256)]
        public int GetPlayerOthersTreeMask(in PlayerAspect playerAspect) {

            var mask = this.GetAllPlayersTreeMask();
            return (mask & ~(1 << playerAspect.unitsTreeIndex));

        }

        [INLINE(256)]
        public unsafe PlayerAspect SetActivePlayer(uint index) {
            E.IS_NOT_IN_TICK(Context.world.state);
            this.activePlayer = index;
            return this.GetActivePlayer();
        }

        [INLINE(256)]
        public unsafe PlayerAspect GetActivePlayer() {
            E.IS_NOT_IN_TICK(Context.world.state);
            return this.GetPlayerEntity(this.activePlayer);
        }

        [INLINE(256)]
        public PlayerAspect GetPlayerEntity(uint index) {
            return this.players[(int)index].GetAspect<PlayerAspect>();
        }

    }

}