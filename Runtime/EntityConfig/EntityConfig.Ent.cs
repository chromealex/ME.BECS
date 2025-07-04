namespace ME.BECS {

    using Unity.Collections;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public partial struct Ent {

        [INLINE(256)][CodeGeneratorIgnoreVisited]
        public static Ent New(in Config config, in JobInfo jobInfo, in FixedString32Bytes editorName = default) {
            var ent = Ent.New(in jobInfo, editorName);
            config.UnsafeConfig.Apply(in ent);
            return ent;
        }

    }
    
    public static class EntityConfigEntExt {

        [INLINE(256)]
        public static bool TryReadStatic<T>(this in Ent ent, out T component) where T : unmanaged, IConfigComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.TryReadStatic(out component);
            }

            component = default;
            return false;

        }

        [INLINE(256)]
        public static T ReadStatic<T>(this in Ent ent) where T : unmanaged, IConfigComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.ReadStatic<T>();
            }

            return StaticTypes<T>.defaultValue;

        }

        [INLINE(256)]
        public static bool HasStatic<T>(this in Ent ent) where T : unmanaged, IConfigComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.HasStatic<T>();
            }

            return false;

        }

    }

}