namespace ME.BECS.Players {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public class PlayerStatic {

        public static readonly Unity.Burst.SharedStatic<Ent> activePlayer = Unity.Burst.SharedStatic<Ent>.GetOrCreate<PlayerStatic>();

    }
    
    public static class PlayerUtils {

        [INLINE(256)]
        public static PlayerAspect GetActivePlayer() {
            if (PlayerStatic.activePlayer.Data.IsAlive() == false) return default;
            return PlayerStatic.activePlayer.Data.GetAspect<PlayerAspect>();
        }

        [INLINE(256)]
        public static void SetActivePlayer(in PlayerAspect playerAspect) => PlayerStatic.activePlayer.Data = playerAspect.ent;

        [INLINE(256)]
        public static Ent CreatePlayer(uint index, in Ent team, JobInfo jobInfo) {
            var ent = Ent.New(jobInfo);
            var aspect = ent.GetOrCreateAspect<PlayerAspect>();
            aspect.index = index;
            aspect.ent.Get<PlayerComponent>().team = team;
            return ent;
        }

        [INLINE(256)]
        public static PlayerAspect GetOwner(in Ent entity) {
            E.REQUIRED<OwnerComponent>(in entity);
            return entity.Read<OwnerComponent>().ent.GetAspect<PlayerAspect>();
        }

        [INLINE(256)]
        public static PlayerAspect GetOwner(in EntRO entity) {
            E.REQUIRED<OwnerComponent>(entity.GetEntity());
            return entity.Read<OwnerComponent>().ent.GetAspect<PlayerAspect>();
        }

        [INLINE(256)]
        public static void SetOwner(in Ent entity, in PlayerAspect player) {
            E.REQUIRED<PlayerComponent>(player.ent);
            entity.Get<OwnerComponent>().ent = player.ent;
        }

        [INLINE(256)]
        public static uint GetPlayerId(in PlayerAspect player) {
            return player.readIndex;
        }

        [INLINE(256)]
        public static Ent GetTeam(in PlayerAspect player) {
            return player.readTeam;
        }

    }

}