namespace ME.BECS {

    [System.Serializable]
    public struct Config : System.IEquatable<Config> {

        public uint sourceId;

        public void Apply(in Ent ent) {
            var entityConfig = EntityConfigsRegistry.GetEntityConfigBySourceId(this.sourceId);
            entityConfig.Apply(in ent);
        }
        
        public UnsafeEntityConfig AsUnsafeConfig() => EntityConfigsRegistry.GetEntityConfigBySourceId(this.sourceId).AsUnsafeConfig();
        
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