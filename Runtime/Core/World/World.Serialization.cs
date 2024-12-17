namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public unsafe partial struct World {
        
        [INLINE(256)]
        public static World Create(byte[] bytes, bool useSerializedWorldId = false) {
            var statePtr = State.Create(bytes);
            var world = new World() {
                state = statePtr,
            };
            Context.Switch(world);
            Worlds.AddWorld(ref world, useSerializedWorldId == true ? world.id : (ushort)0);
            return world;
        }

    }

    public static unsafe class WorldSerializationExt {

        [INLINE(256)]
        public static byte[] Serialize(this in World world) {

            E.IS_CREATED(world);
            var buffer = new StreamBufferWriter();
            Batches.Apply(world.state);
            State.BurstMode(world.state, false, default);
            (*world.state.ptr).Serialize(ref buffer);
            State.BurstMode(world.state, true, default);
            var bytes = buffer.ToArray();
            buffer.Dispose();
            return bytes;

        }

        [INLINE(256)]
        private static void Serialize(this in State state, ref StreamBufferWriter bufferWriter) {

            var src = state;
            var copy = state;
            ++copy.allocator.version;
            copy.allocator.Serialize(ref bufferWriter);
            src.allocator = default;
            // Write all except of allocator pointers
            bufferWriter.Write(src);

        }

        [INLINE(256)]
        internal static State Deserialize(this ref State state, byte[] bytes) {

            var buffer = new StreamBufferReader(bytes);
            // Read allocator data
            var copy = state;
            copy.allocator.Deserialize(ref buffer);
            // Read all state
            buffer.Read(ref state);
            state.allocator = copy.allocator;
            
            return state;

        }

    }

    public unsafe struct StreamBufferReader {

        private readonly safe_ptr<byte> arr;
        private readonly uint arrSize;
        private uint position;

        [INLINE(256)]
        public StreamBufferReader(byte[] bytes) {

            this = default;
            var size = (uint)bytes.Length;
            this.arr = _makeArray<byte>(size);
            fixed (byte* ptr = &bytes[0]) {
                _memcpy((safe_ptr)ptr, this.arr, size);
            }

            this.arrSize = size;
            this.position = 0u;

        }

        [INLINE(256)]
        public StreamBufferReader(safe_ptr<byte> bytes, uint size) {

            this = default;
            this.arr = bytes;
            this.arrSize = size;
            this.position = 0u;

        }

        [INLINE(256)]
        public void Dispose() {
            if (this.arr.ptr != null) _free(this.arr);
        }

        [INLINE(256)]
        public void ReadBlittable<T>(ref T value, uint size) where T : unmanaged {

            var ptr = this.GetPointerAndMove(size);
            value = *(T*)ptr.ptr;

        }

        [INLINE(256)]
        public safe_ptr<byte> GetPointer() => this.arr + this.position;

        [INLINE(256)]
        public safe_ptr<byte> GetPointerAndMove(uint size) {
            
            if (this.position + size > this.arrSize) throw new System.Exception();
            
            var pos = this.position;
            this.position += size;
            return this.arr + pos;
            
        }

        [INLINE(256)]
        public void Read(ref byte* value, uint length) {

            var ptr = this.GetPointerAndMove(length);
            _memcpy(ptr, (safe_ptr)value, length);
            
        }

        [INLINE(256)]
        public void Read<T>(ref T* value, uint length) where T : unmanaged {

            var size = TSize<T>.size * length;
            var ptr = this.GetPointerAndMove(size);
            _memcpy(ptr, (safe_ptr)(byte*)value, size);
            
        }

        [INLINE(256)]
        public void Read<T>(ref T value) where T : unmanaged {
            
            this.ReadBlittable(ref value, TSize<T>.size);
            
        }
        
        [INLINE(256)]
        public void Read(ref int value) {
            
            const uint size = 4u;
            this.ReadBlittable(ref value, size);

        }

        [INLINE(256)]
        public void Read(ref long value) {
            
            const uint size = 8u;
            this.ReadBlittable(ref value, size);
            
        }

    }

    public unsafe struct StreamBufferWriter {

        private safe_ptr<byte> arr;
        private uint arrSize;
        private uint position;

        [INLINE(256)]
        public StreamBufferWriter(uint capacity) {

            this.arr = default;
            this.arrSize = 0u;
            this.position = 0u;

            if (capacity > 0u) {
                this.SetCapacity(capacity);
            }

        }

        [INLINE(256)]
        public void Dispose() {
            if (this.arr.ptr != null) _free(this.arr);
            this = default;
        }
        
        [INLINE(256)]
        public void Reset() {
            this.position = 0u;
        }

        [INLINE(256)]
        public byte[] ToArray() {

            var bytes = new byte[this.position];
            fixed (byte* ptr = &bytes[0]) {
                _memcpy(this.arr, (safe_ptr)ptr, this.position);
            }
            return bytes;

        }

        [INLINE(256)]
        private void SetCapacity(uint size) {

            if (size >= this.arrSize) {

                _resizeArray(ref this.arr, ref this.arrSize, size);

            }
            
        }

        [INLINE(256)]
        public safe_ptr<byte> GetPointer() => this.arr + this.position;

        [INLINE(256)]
        public safe_ptr<byte> GetPointerAndMove(uint size) {
            
            var pos = this.position;
            this.SetCapacity(this.position + size);
            this.position += size;
            return this.arr + pos;
            
        }

        [INLINE(256)]
        public void WriteBlittable<T>(T value, uint size) where T : unmanaged {

            var ptr = this.GetPointerAndMove(size);
            *(T*)ptr.ptr = value;

        }

        [INLINE(256)]
        public void Write(byte* arrBytes, uint length) {

            var ptr = this.GetPointerAndMove(length);
            _memcpy((safe_ptr)arrBytes, ptr, length);

        }

        [INLINE(256)]
        public void Write<T>(T* arrBytes, uint length) where T : unmanaged {

            var size = TSize<T>.size * length;
            var ptr = this.GetPointerAndMove(size);
            _memcpy((safe_ptr)(byte*)arrBytes, ptr, size);

        }

        [INLINE(256)]
        public void Write<T>(T value) where T : unmanaged {
            
            this.WriteBlittable(value, TSize<T>.size);
            
        }

        [INLINE(256)]
        public void Write(int value) {

            const uint size = 4u;
            this.WriteBlittable(value, size);

        }

        [INLINE(256)]
        public void Write(long value) {

            const uint size = 8u;
            this.WriteBlittable(value, size);

        }

    }

}