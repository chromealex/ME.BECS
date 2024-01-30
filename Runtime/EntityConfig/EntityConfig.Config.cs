namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    [System.Serializable]
    public struct Config : System.IEquatable<Config> {

        public uint sourceId;

        [INLINE(256)]
        public readonly bool Apply(in Ent ent) {
            var entityConfig = EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
            if (entityConfig.IsValid() == true) {
                entityConfig.Apply(in ent);
                return true;
            }

            return false;
        }
        
        [INLINE(256)]
        public readonly UnsafeEntityConfig AsUnsafeConfig() {
            return EntityConfigsRegistry.GetUnsafeEntityConfigBySourceId(this.sourceId);
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