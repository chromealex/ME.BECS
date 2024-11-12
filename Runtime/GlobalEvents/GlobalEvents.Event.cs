namespace ME.BECS {
    
    [System.Serializable]
    public struct Event : System.IEquatable<Event> {
        
        public uint id;
        public ushort worldId;
        
        public static Event Create(uint id, ushort visualWorldId) => new Event { id = id, worldId = visualWorldId };

        public static Event Create(uint id, in World visualWorld) => new Event { id = id, worldId = visualWorld.id };
        public static Event Create(uint id) => new Event { id = id, worldId = 0 };

        public bool Equals(Event other) {
            return this.id == other.id && this.worldId == other.worldId;
        }

        public override bool Equals(object obj) {
            return obj is Event other && this.Equals(other);
        }

        public override int GetHashCode() {
            return ((int)this.id + 17) ^ this.worldId;
        }

    }

}