namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public class ConfigDrawerAttribute : UnityEngine.PropertyAttribute {}
    
    [System.Serializable]
    public struct Config : System.IEquatable<Config> {

        public enum JoinOptions {
            /// <summary>
            /// Add all components from config onto entity.
            /// If the component exists on the entity, it will be replaced.
            /// </summary>
            FullJoin,
            /// <summary>
            /// Only those components that already exist on the entity are replaced.
            /// If the component doesn't exist on the entity, it will be skipped.
            /// </summary>
            LeftJoin,
            /// <summary>
            /// Only those components that do not exist on the entity are added.
            /// If the component exists on the entity, it will be skipped.
            /// </summary>
            RightJoin,
        }
        
        public uint sourceId;

        public bool IsValid => this.sourceId > 0u;

        public readonly UnsafeEntityConfig UnsafeConfig => this.AsUnsafeConfig();

        [INLINE(256)]
        public readonly bool Apply(in Ent ent, JoinOptions options = JoinOptions.FullJoin) {
            var entityConfig = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
            if (entityConfig.IsValid() == true) {
                entityConfig.Apply(in ent, options);
                return true;
            }

            return false;
        }

        public static bool operator ==(Config a, Config b) {
            return a.sourceId == b.sourceId;
        }

        public static bool operator !=(Config a, Config b) {
            return !(a == b);
        }

        [INLINE(256)]
        public readonly UnsafeEntityConfig AsUnsafeConfig() {
            return EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
        }
        
        [INLINE(256)]
        public readonly EntityConfig Get() {
            return EntityConfigsRegistry.GetEntityConfigBySourceId(this.sourceId);
        }

        public bool Equals(Config other) {
            return this.sourceId == other.sourceId;
        }

        public override bool Equals(object obj) {
            return obj is Config other && this.Equals(other);
        }

        public override int GetHashCode() {
            return (int)this.sourceId;
        }

        public override string ToString() {
            return $"[ Config ] Id: {this.sourceId}";
        }

    }

}