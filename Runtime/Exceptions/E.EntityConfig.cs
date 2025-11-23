namespace ME.BECS {
    
    using System.Diagnostics;
    using BURST_DISCARD = Unity.Burst.BurstDiscardAttribute;
    using HIDE_CALLSTACK = UnityEngine.HideInCallstackAttribute;
    using static Cuts;

    public partial class E {

        public class EntityConfigException : System.Exception {

            [HIDE_CALLSTACK]
            public static void ThrowNull(EntityConfig config) {
                throw new AddrException($"Config {config} has null component. This is not supported and will always fail on unsafe config initialization. Please check it in the inspector.");
            }

        }

    }
    
    public static partial class E {

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