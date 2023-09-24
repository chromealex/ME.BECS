namespace ME.BECS {

    public static class EntityConfigEntExt {

        public static T ReadStatic<T>(this in Ent ent) where T : unmanaged, IComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.ReadStatic<T>();
            }

            return StaticTypes<T>.defaultValue;

        }

        public static bool HasStatic<T>(this in Ent ent) where T : unmanaged, IComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.HasStatic<T>();
            }

            return false;

        }

    }

}