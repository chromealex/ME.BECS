namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;
    
    /// <summary>
    /// Supplies config drawer metadata to annotated declarations.
    /// </summary>
    public class ConfigDrawerAttribute : UnityEngine.PropertyAttribute {}
    
    /// <summary>
    /// References an entity configuration through its registered identity.
    /// </summary>
    [System.Serializable]
    [LAYOUT(System.Runtime.InteropServices.LayoutKind.Sequential, Size = 8)]
    public struct Config : System.IEquatable<Config> {

        /// <summary>
        /// Defines the supported join options values.
        /// </summary>
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
        
        /// <summary>
        /// Source id used to locate the associated entry.
        /// </summary>
        public uint sourceId;
        /// <summary>
        /// Field is used for correct alignment on 32x platforms (like WebGL)
        /// </summary>
        private uint alignment;

        /// <summary>
        /// Indicates is valid.
        /// </summary>
        public bool IsValid => this.sourceId > 0u && this.UnsafeConfig.IsValid() == true;

        /// <summary>
        /// Unsafe config used by <c>Config</c>.
        /// </summary>
        public readonly UnsafeEntityConfig UnsafeConfig => this.AsUnsafeConfig();

        /// <summary>
        /// Applies the supplied data or pending changes to the target state.
        /// </summary>
        [INLINE(256)]
        public readonly bool Apply(in Ent ent, JoinOptions options = JoinOptions.FullJoin) {
            var entityConfig = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
            if (entityConfig.IsValid() == true) {
                entityConfig.Apply(in ent, options);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tests equality of the operands.
        /// </summary>
        public static bool operator ==(Config a, Config b) {
            return a.sourceId == b.sourceId;
        }

        /// <summary>
        /// Tests whether the operands differ.
        /// </summary>
        public static bool operator !=(Config a, Config b) {
            return !(a == b);
        }

        /// <summary>
        /// Resolves the configuration reference to its native configuration data.
        /// </summary>
        [INLINE(256)]
        public readonly UnsafeEntityConfig AsUnsafeConfig() {
            return EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
        }
        
        /// <summary>
        /// Returns the requested entry from config.
        /// </summary>
        [INLINE(256)]
        public readonly EntityConfig Get() {
            return EntityConfigsRegistry.GetEntityConfigBySourceId(this.sourceId);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(Config other) {
            return this.sourceId == other.sourceId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is Config other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return (int)this.sourceId;
        }

        /// <summary>
        /// Formats this value for display or diagnostics.
        /// </summary>
        public override string ToString() {
            return $"[ Config ] Id: {this.sourceId}";
        }

    }

}