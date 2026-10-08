namespace ME.BECS {
    
    /// <summary>
    /// Defines event state and operations.
    /// </summary>
    [System.Serializable]
    public struct Event : System.IEquatable<Event> {
        
        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public uint id;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;
        
        /// <summary>
        /// Creates <c>Event</c> using the supplied creation arguments.
        /// </summary>
        public static Event Create(uint id, ushort visualWorldId) => new Event { id = id, worldId = visualWorldId };

        /// <summary>
        /// Creates <c>Event</c> using the supplied creation arguments.
        /// </summary>
        public static Event Create(uint id, in World visualWorld) => new Event { id = id, worldId = visualWorld.id };
        /// <summary>
        /// Creates <c>Event</c> using the supplied creation arguments.
        /// </summary>
        public static Event Create(uint id) => new Event { id = id, worldId = 0 };

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(Event other) {
            return this.id == other.id && this.worldId == other.worldId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is Event other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return ((int)this.id + 17) ^ this.worldId;
        }

    }

}