namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public partial struct StreamBufferReader {

        [INLINE(256)]
        public void Read(ref ME.BECS.FixedPoint.float2 value) {
            this.Read(ref value.x.rawValue);
            this.Read(ref value.y.rawValue);
        }

        [INLINE(256)]
        public void Read(ref ME.BECS.FixedPoint.float3 value) {
            this.Read(ref value.x.rawValue);
            this.Read(ref value.y.rawValue);
            this.Read(ref value.z.rawValue);
        }

        [INLINE(256)]
        public void Read(ref ME.BECS.FixedPoint.float4 value) {
            this.Read(ref value.x.rawValue);
            this.Read(ref value.y.rawValue);
            this.Read(ref value.z.rawValue);
            this.Read(ref value.w.rawValue);
        }

        [INLINE(256)]
        public void Read(ref ME.BECS.FixedPoint.quaternion value) {
            this.Read(ref value.value);
        }

        [INLINE(256)]
        public void Read(ref sbyte value) {
            const uint size = 1u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref byte value) {
            const uint size = 1u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref int value) {
            const uint size = 4u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref uint value) {
            const uint size = 4u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref long value) {
            const uint size = 8u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref ulong value) {
            const uint size = 8u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref short value) {
            const uint size = 2u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref ushort value) {
            const uint size = 2u;
            this.ReadBlittable(ref value, size);
        }

        [INLINE(256)]
        public void Read(ref LockSpinner value) {
            this.Read(ref value.value);
        }

        [INLINE(256)]
        public void Read<T>(ref MemArray<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read<T>(ref MemArrayAuto<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read<T>(ref UIntDictionary<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref BitArray value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read<T>(ref JobThreadStack<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read<T>(ref List<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read<T>(ref ListAuto<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }
        
        [INLINE(256)]
        public void Read<T>(ref MemArrayThreadCacheLine<T> value) where T : unmanaged {
            value.DeserializeHeaders(ref this);
        }
        
        [INLINE(256)]
        public void Read(ref ReadWriteSpinner value) {
            value.DeserializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Read(ref MemPtr value) {
            var zoneId = 0u;
            var offset = 0u;
            this.Read(ref zoneId);
            this.Read(ref offset);
            value = new MemPtr(zoneId, offset);
        }

        [INLINE(256)]
        public void Read(ref Ent value) {
            var pack = 0UL;
            this.Read(ref pack);
            value = new Ent(pack);
        }

    }

}