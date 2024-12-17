namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe struct Task : System.IEquatable<Task>, System.IComparable<Task> {

        public OneShotType type;
        public Ent ent;
        public uint typeId;
        public uint groupId;
        public ushort updateType;
        public MemAllocatorPtr data;

        [INLINE(256)]
        public safe_ptr GetData(safe_ptr<State> state) => state.ptr->allocator.GetUnsafePtr(in this.data.ptr);

        [INLINE(256)]
        public void Dispose(ref MemoryAllocator allocator) {
            if (this.data.IsValid() == true) this.data.Dispose(ref allocator);
        }

        public bool Equals(Task other) {
            return this.ent.Equals(other.ent) && this.typeId == other.typeId;
        }

        public override bool Equals(object obj) {
            return obj is Task other && this.Equals(other);
        }

        public override int GetHashCode() {
            return this.ent.GetHashCode() ^ (int)this.typeId;
        }

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