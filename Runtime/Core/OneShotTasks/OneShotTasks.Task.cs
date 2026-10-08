namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    
    /// <summary>
    /// Defines task state and operations.
    /// </summary>
    public unsafe struct Task : System.IEquatable<Task>, System.IComparable<Task> {

        /// <summary>
        /// Type descriptor used by the associated operation.
        /// </summary>
        public OneShotType type;
        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent;
        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public uint typeId;
        /// <summary>
        /// Lifecycle phase in which the update runs.
        /// </summary>
        public ushort updateType;
        /// <summary>
        /// Data consumed or produced by the containing operation.
        /// </summary>
        public MemAllocatorPtr data;

        /// <summary>
        /// Returns data.
        /// </summary>
        [INLINE(256)]
        public safe_ptr GetData(safe_ptr<State> state) => state.ptr->allocator.GetUnsafePtr(in this.data.ptr);

        /// <summary>
        /// Releases the resources owned by this task instance.
        /// </summary>
        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            if (this.data.IsValid() == true) this.data.Dispose(ref allocator);
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public bool Equals(Task other) {
            return this.ent.Equals(other.ent) && this.typeId == other.typeId;
        }

        /// <summary>
        /// Tests equality using the identity or value comparison defined by this type.
        /// </summary>
        public override bool Equals(object obj) {
            return obj is Task other && this.Equals(other);
        }

        /// <summary>
        /// Returns a hash code consistent with this type's equality comparison.
        /// </summary>
        public override int GetHashCode() {
            return this.ent.GetHashCode() ^ (int)this.typeId;
        }

        /// <summary>
        /// Compares this value with the supplied value for sorting.
        /// </summary>
        [INLINE(256)]
        public int CompareTo(Task other) {
            var entComparison = this.ent.CompareTo(other.ent);
            if (entComparison != 0) {
                return entComparison;
            }

            return this.typeId.CompareTo(other.typeId);
        }

    }

}