namespace ME.BECS.Players {

    using ME.BECS.Network.Markers;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Network;
    
    /// <summary>
    /// Coordinates players during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Initialize default players")]
    public partial struct PlayersSystem : IAwake {

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static PlayersSystem Default => new PlayersSystem() {
            playersCount = 4u,
        };
        
        /// <summary>
        /// Players count for the associated storage.
        /// </summary>
        public uint playersCount;
        
        private MemArrayAuto<Ent> players;
        private MemArrayAuto<Ent> teams;
        private uint activePlayer;

        /// <summary>
        /// Initializes players system state from the supplied context.
        /// </summary>
        public void OnAwake(ref SystemContext context) {

            context.dependsOn.Complete();
            
            var ent = Ent.New(in context);
            this.players = new MemArrayAuto<Ent>(in ent, this.playersCount);
            this.teams = new MemArrayAuto<Ent>(in ent, this.playersCount);
            for (uint i = 0u; i < this.players.Length; ++i) {
                var id = i + 1u;
                var team = Ent.New<TeamEntityType>(in context, editorName: $"Team#{id}");
                team.Set(new TeamComponent() {
                    id = id,
                });
                this.teams[(int)i] = team;
            }

            for (uint i = 0u; i < this.players.Length; ++i) {
                this.players[(int)i] = PlayerUtils.CreatePlayer(i, this.teams[(int)i], JobInfo.Create(context.world.id));
            }
            
            this.UpdateTeams();
            
            //UnityEngine.Debug.Log("Players Ready");

        }

        /// <summary>
        /// Returns players.
        /// </summary>
        public readonly MemArrayAuto<Ent> GetPlayers() => this.players;
        /// <summary>
        /// Returns teams.
        /// </summary>
        public readonly MemArrayAuto<Ent> GetTeams() => this.teams;

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

        /// <summary>
        /// Returns all players tree mask.
        /// </summary>
        [INLINE(256)]
        public int GetAllPlayersTreeMask() {

            var mask = 0;
            for (uint i = 0u; i < this.players.Length; ++i) {
                var player = this.players[(int)i].GetAspect<PlayerAspect>();
                mask |= 1 << player.unitsTreeIndex;
            }
            return mask;

        }

        /// <summary>
        /// Returns all players tree mask.
        /// </summary>
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

        /// <summary>
        /// Returns player tree mask.
        /// </summary>
        [INLINE(256)]
        public int GetPlayerTreeMask(in PlayerAspect playerAspect) {

            return (1 << playerAspect.unitsTreeIndex);

        }

        /// <summary>
        /// Returns player others tree mask.
        /// </summary>
        [INLINE(256)]
        public int GetPlayerOthersTreeMask(in PlayerAspect playerAspect) {

            var mask = this.GetAllPlayersTreeMask();
            return (mask & ~(1 << playerAspect.unitsTreeIndex));

        }

        /// <summary>
        /// Selects the player used by active-player lookups.
        /// </summary>
        [INLINE(256)]
        public unsafe PlayerAspect SetActivePlayer(uint index) {
            E.IS_NOT_IN_TICK(Context.world.state);
            this.activePlayer = index;
            var player = this.GetActivePlayer();
            PlayerUtils.SetActivePlayer(in player);
            return player;
        }

        /// <summary>
        /// Returns the player selected as active for the current context.
        /// </summary>
        [INLINE(256)]
        public unsafe PlayerAspect GetActivePlayer() {
            E.IS_NOT_IN_TICK(Context.world.state);
            return this.GetPlayerEntity(this.activePlayer);
        }

        /// <summary>
        /// Returns player entity.
        /// </summary>
        [INLINE(256)]
        public readonly PlayerAspect GetPlayerEntity(uint index) {
            return this.players[(int)index].GetAspect<PlayerAspect>();
        }

        /// <summary>
        /// Returns first player by team ID.
        /// </summary>
        [INLINE(256)]
        public PlayerAspect GetFirstPlayerByTeamId(uint teamId) {
            for (uint i = 0u; i < this.players.Length; ++i) {
                var player = this.players[(int)i].GetAspect<PlayerAspect>();
                if (player.readTeam == this.teams[(int)(teamId - 1u)]) {
                    return player;
                }
            }
            return default;
        }
        
        /// <summary>
        /// Sets active player defeat.
        /// </summary>
        [INLINE(256)]
        public void SetActivePlayerDefeat() {
            this.GetActivePlayer().ent.World.SendNetworkEvent(new SetDefeatData(), OnSetDefeatReceived);
        }

        /// <summary>
        /// Handles the set defeat received callback.
        /// </summary>
        [NetworkMethod]
        [AOT.MonoPInvokeCallback(typeof(NetworkMethodDelegate))]
        public static void OnSetDefeatReceived(in InputData data, ref SystemContext context) {
            
            context.dependsOn.Complete();
            var player = context.world.GetSystem<PlayersSystem>().GetPlayerEntity(data.PlayerId);
            player.SetDefeat();

        }

        private struct SetDefeatData : ME.BECS.Network.IPackageData {

            private byte someData;

            public void Serialize(ref StreamBufferWriter writer) {
                writer.Write(this.someData);
            }

            public void Deserialize(ref StreamBufferReader reader) {
                reader.Read(ref this.someData);
            }

        }

    }

}