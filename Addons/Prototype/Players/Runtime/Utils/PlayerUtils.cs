namespace ME.BECS.Players {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public static class PlayerUtils {

        [INLINE(256)]
        public static Ent CreatePlayer(uint index, uint teamId) {
            var ent = Ent.New();
            ent.Set<PlayerAspect>();
            var aspect = ent.GetAspect<PlayerAspect>();
            aspect.index = index;
            aspect.teamId = teamId;
            return ent;
        }

        [INLINE(256)]
        public static PlayerAspect GetOwner(in Ent entity) {
            E.REQUIRED<OwnerComponent>(in entity);
            return entity.Read<OwnerComponent>().ent.GetAspect<PlayerAspect>();
        }

        [INLINE(256)]
        public static void SetOwner(in Ent entity, in PlayerAspect player) {
            E.REQUIRED<PlayerComponent>(player.ent);
            E.REQUIRED<OwnerComponent>(in entity);
            entity.Get<OwnerComponent>().ent = player.ent;
        }

        [INLINE(256)]
        public static uint GetPlayerId(in PlayerAspect player) {
            return player.index;
        }

        [INLINE(256)]
        public static uint GetTeam(in PlayerAspect player) {
            return player.teamId;
        }

    }

}