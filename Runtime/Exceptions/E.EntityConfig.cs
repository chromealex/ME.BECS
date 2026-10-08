namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using static Cuts;

    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public partial class E {

        /// <summary>
        /// Reports a violation of the entity config invariant.
        /// </summary>
        public class EntityConfigException : System.Exception {

            /// <summary>
            /// Throws the null-value diagnostic for the supplied argument.
            /// </summary>
            [HIDE_CALLSTACK]
            public static void ThrowNull(EntityConfig config) {
                throw new AddrException($"Config {config} has null component. This is not supported and will always fail on unsafe config initialization. Please check it in the inspector.");
            }

        }

    }
    
    /// <summary>
    /// Provides conditional runtime assertions and diagnostic exceptions for ECS invariants.
    /// </summary>
    public static partial class E {

        /// <summary>
        /// Validates config.
        /// </summary>
        [Conditional(COND.EDITOR)]
        [HIDE_CALLSTACK]
        public static void ValidateConfig(EntityConfig entityConfig) {

            #if UNITY_EDITOR
            Validate(entityConfig, entityConfig.data);
            Validate(entityConfig, entityConfig.sharedData);
            Validate(entityConfig, entityConfig.staticData);
            Validate(entityConfig, entityConfig.aspects);
            #endif

        }

        private static void Validate<T>(EntityConfig entityConfig, ComponentsStorage<T> data) where T : class {
            for (int i = 0; i < data.components.Length; ++i) {
                if (data.components[i] == null) {
                    EntityConfigException.ThrowNull(entityConfig);
                }
            }
        }

    }

}