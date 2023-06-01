namespace ME.BECS {

    public static class EntityConfigEntExt {

        public static T ReadStatic<T>(this in Ent ent) where T : unmanaged, IStaticComponent {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config != null) {
                return config.ReadStatic<T>();
            }

            return default;

        }

        public static bool HasStatic<T>(this in Ent ent) where T : unmanaged, IStaticComponent {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config != null) {
                return config.HasStatic<T>();
            }

            return default;

        }

    }

}