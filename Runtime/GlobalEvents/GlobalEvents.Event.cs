namespace ME.BECS {
    
    [System.Serializable]
    public struct Event : System.IEquatable<Event> {
        
        public uint id;
        public ushort worldId;
        
        public static Event Create(uint id, ushort worldId) => new Event { id = id, worldId = worldId };

        public static Event Create(uint id, in World world) => new Event { id = id, worldId = world.id };

        public bool Equals(Event other) {
            return this.id == other.id && this.worldId == other.worldId;
        }

        public override bool Equals(object obj) {
            return obj is Event other && this.Equals(other);
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(this.id, this.worldId);
        }

    }

}