namespace ME.BECS {

    public static class EntExt {

        public static void Destroy(this in Ent ent, float lifetime) {

            ent.Set(new DestroyWithLifetime() { lifetime = lifetime, });
            
        }

        public static void DestroyEndTick(this in Ent ent) {

            ent.Set(new DestroyWithTicks() { ticks = 0, });
            
        }

        public static void Destroy(this in Ent ent, int ticks) {

            ent.Set(new DestroyWithTicks() { ticks = ticks, });

        }

    }

}