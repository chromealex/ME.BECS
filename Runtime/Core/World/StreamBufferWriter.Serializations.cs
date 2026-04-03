namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public partial struct StreamBufferWriter {

        [INLINE(256)]
        public void Write(ME.BECS.FixedPoint.float2 value) {
            this.Write(value.x.rawValue);
            this.Write(value.y.rawValue);
        }

        [INLINE(256)]
        public void Write(ME.BECS.FixedPoint.float3 value) {
            this.Write(value.x.rawValue);
            this.Write(value.y.rawValue);
            this.Write(value.z.rawValue);
        }

        [INLINE(256)]
        public void Write(ME.BECS.FixedPoint.float4 value) {
            this.Write(value.x.rawValue);
            this.Write(value.y.rawValue);
            this.Write(value.z.rawValue);
            this.Write(value.w.rawValue);
        }

        [INLINE(256)]
        public void Write(ME.BECS.FixedPoint.quaternion value) {
            this.Write(value.value);
        }

        [INLINE(256)]
        public void Write(byte value) {
            const uint size = 1u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(sbyte value) {
            const uint size = 1u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(int value) {
            const uint size = 4u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(uint value) {
            const uint size = 4u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(long value) {
            const uint size = 8u;
            this.WriteBlittable(value, size);
        }
        
        [INLINE(256)]
        public void Write(ulong value) {
            const uint size = 8u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(short value) {
            const uint size = 2u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(ushort value) {
            const uint size = 2u;
            this.WriteBlittable(value, size);
        }

        [INLINE(256)]
        public void Write(LockSpinner value) {
            this.Write(value.value);
        }

        [INLINE(256)]
        public void Write<T>(MemArray<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write<T>(MemArrayAuto<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write<T>(UIntDictionary<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(BitArray value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write<T>(JobThreadStack<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write<T>(List<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write<T>(ListAuto<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }
        
        [INLINE(256)]
        public void Write<T>(MemArrayThreadCacheLine<T> value) where T : unmanaged {
            value.SerializeHeaders(ref this);
        }
        
        [INLINE(256)]
        public void Write(ReadWriteSpinner value) {
            value.SerializeHeaders(ref this);
        }

        [INLINE(256)]
        public void Write(MemPtr value) {
            this.Write(value.zoneId);
            this.Write(value.offset);
        }

        [INLINE(256)]
        public void Write(Ent value) {
            this.Write(value.pack);
        }

    }

}