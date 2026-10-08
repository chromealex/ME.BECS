namespace ME.BECS {

    using Unity.Collections;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Identifies an entity by its world, slot and generation; the handle does not keep the entity alive.
    /// </summary>
    public partial struct Ent {

        /// <summary>
        /// Creates <c>Ent</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)][CodeGeneratorIgnoreVisited]
        public static Ent New(in Config config, in JobInfo jobInfo, in FixedString32Bytes editorName = default) {
            return New<DefaultEntityType>(in config, in jobInfo, in editorName);
        }

        /// <summary>
        /// Creates <c>Ent</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)][CodeGeneratorIgnoreVisited]
        public static Ent New<T>(in Config config, in JobInfo jobInfo, in FixedString32Bytes editorName = default) where T : unmanaged, IEntityType {
            var ent = Ent.New<T>(in jobInfo, editorName);
            config.UnsafeConfig.Apply(in ent);
            return ent;
        }

    }
    
    /// <summary>
    /// Provides helper operations for entity config ent.
    /// </summary>
    public static class EntityConfigEntExt {

        /// <summary>
        /// Attempts to read static and reports whether the operation succeeded.
        /// </summary>
        [INLINE(256)]
        public static bool TryReadStatic<T>(this in Ent ent, out T component) where T : unmanaged, IConfigComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.TryReadStatic(out component);
            }

            component = default;
            return false;

        }

        /// <summary>
        /// Reads static.
        /// </summary>
        [INLINE(256)]
        public static T ReadStatic<T>(this in Ent ent) where T : unmanaged, IConfigComponentStatic {

            var config = ent.Read<EntityConfigComponent>().EntityConfig;
            if (config.IsValid() == true) {
                return config.ReadStatic<T>();
            }

            return StaticTypes<T>.defaultValue;

        }

        /// <summary>
        /// Tests whether the context has static.
        /// </summary>
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