namespace ME.BECS {

    public static class EntExt {

        public static void Destroy(this in Ent ent, float lifetime) {

            ent.Set(new DestroyWithLifetime() { lifetime = lifetime, });
            
        }

        public static void DestroyEndTick(this in Ent ent) {

            ent.Set(new DestroyWithLifetime() { lifetime = 0f, });
            
        }

    }

}