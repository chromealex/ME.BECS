namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

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
            Batches.Apply(world.id, world.state);
            State.BurstMode(world.state, false, default);
            (*world.state.ptr).Serialize(ref buffer);
            State.BurstMode(world.state, true, default);
            var bytes = buffer.ToArray();
            buffer.Dispose();
            return bytes;

        }

        [INLINE(256)]
        public static void Serialize(this in State state, ref StreamBufferWriter bufferWriter) {

            var src = state;
            var copy = state;
            ++copy.allocator.version;
            copy.allocator.Serialize(ref bufferWriter);
            src.allocator = default;
            // Write all except of allocator pointers
            bufferWriter.Write(src);

        }

        [INLINE(256)]
        public static State Deserialize(this ref State state, byte[] bytes) {

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

    public unsafe struct Patch {

        public uint newLength;
        public uint deltaCount;
        public uint tailLength;
        public StreamBufferWriter data;

        public Patch(byte[] bytes) {
            this.newLength = (uint)bytes.Length;
            this.deltaCount = 0u;
            this.tailLength = 0u;
            this.data = new StreamBufferWriter(this.newLength);
            fixed (byte* ptr = &bytes[0]) {
                this.data.Write(ptr, this.newLength);
            }
        }
        
        public byte[] Serialize() {
            return this.data.ToArray();
        }

        public static void Apply(in Patch patch, safe_ptr<State> state) {

            var stateWriter = new StreamBufferWriter(patch.newLength);
            state.ptr->Serialize(ref stateWriter);
            
            var data = new StreamBufferReader(patch.data.ToArray());
            while (data.Position < data.Length) {
                byte type = default;
                data.Read(ref type);
                uint pos = default;
                data.Read(ref pos);
                stateWriter.MoveTo(pos);
                if (type == 0) {
                    uint count = default;
                    data.Read(ref count);
                    while (count > 0u) {
                        Unity.Burst.Intrinsics.v256 val = default;
                        data.Read(ref val);
                        stateWriter.Write(val);
                        --count;
                    }
                } else {
                    uint count = default;
                    data.Read(ref count);
                    for (int i = 0; i < count; ++i) {
                        byte val = default;
                        data.Read(ref val);
                        stateWriter.Write(val);
                    }
                }
            }

            state.ptr->Deserialize(stateWriter.ToArray());

        }

        [INLINE(256)]
        public static bool Equals(ref Unity.Burst.Intrinsics.v256 a, ref Unity.Burst.Intrinsics.v256 b) {
            if (a.Byte0 != b.Byte0 ||
                a.Byte31 != b.Byte31) {
                // early exit
                return false;
            }
            if (Unity.Burst.Intrinsics.X86.Avx2.IsAvx2Supported) {
                var xor = Unity.Burst.Intrinsics.X86.Avx2.mm256_xor_si256(a, b);
                return Unity.Burst.Intrinsics.X86.Avx.mm256_testz_si256(xor, xor) != 0;
            }
            if (Unity.Burst.Intrinsics.Arm.Neon.IsNeonSupported) {
                var aLo = a.Lo128;
                var aHi = a.Hi128;
                var bLo = b.Lo128;
                var bHi = b.Hi128;
                var cmpLo = Unity.Burst.Intrinsics.Arm.Neon.vceqq_u32(aLo, bLo);
                var cmpHi = Unity.Burst.Intrinsics.Arm.Neon.vceqq_u32(aHi, bHi);
                ulong maskLo = Unity.Burst.Intrinsics.Arm.Neon.vmaxvq_u32(cmpLo);
                ulong maskHi = Unity.Burst.Intrinsics.Arm.Neon.vmaxvq_u32(cmpHi);
                return maskLo == uint.MaxValue && maskHi == uint.MaxValue;
            }

            unsafe {
                var pa = (byte*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref a);
                var pb = (byte*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AddressOf(ref b);
                for (var i = 0; i < 32; ++i) {
                    if (pa[i] != pb[i]) {
                        return false;
                    }
                }
            }

            return true;
        }

        [BURST]
        public static void GetDiff(StreamBufferReader source, StreamBufferReader dest, ref Patch patch) {
            patch = GetDiff(source, dest);
        }

        public static Patch GetDiff(StreamBufferReader source, StreamBufferReader dest) {

            var packSize = (uint)sizeof(Unity.Burst.Intrinsics.v256);

            var patch = new Patch();
            var length = source.Length;
            patch.newLength = length;
            var stream = new StreamBufferWriter(length);
            var headerPos = 0u;
            var isBreak = true;
            var currentCount = 0u;
            while (length >= packSize) {
                Unity.Burst.Intrinsics.v256 valSource = default;
                Unity.Burst.Intrinsics.v256 valDest = default;
                var pos = source.Position;
                if (source.Position + packSize <= source.Length) source.Read(ref valSource);
                if (dest.Position + packSize <= dest.Length) dest.Read(ref valDest);
                if (Equals(ref valSource, ref valDest) == false) {
                    if (isBreak == true) {
                        // if break found - write offset
                        stream.Write((byte)0);
                        stream.Write(pos);
                        headerPos = stream.Position;
                        stream.Write(1u);
                        ++patch.deltaCount;
                    }
                    // write delta
                    stream.Write(valSource);
                    ++currentCount;
                    isBreak = false;
                } else {
                    // write header count
                    if (currentCount > 0u) {
                        pos = stream.Position;
                        stream.MoveTo(headerPos);
                        stream.Write(currentCount);
                        stream.MoveTo(pos);
                    }
                    currentCount = 0u;
                    isBreak = true;
                }
                length -= packSize;
            }

            if (length > 0u) {
                patch.tailLength = length;
                stream.Write((byte)1);
                stream.Write(source.Position);
                stream.Write(length);
                for (uint i = 0u; i < length; ++i) {
                    byte b = default;
                    dest.Read(ref b);
                    stream.Write(b);
                }
            }

            patch.data = stream;
            return patch;

        }

        public void Dispose() {
            this.data.Dispose();
        }
        
        public override string ToString() {

            var data = new StreamBufferReader(this.data.ToArray());
            var str = new System.Text.StringBuilder((int)data.Length);
            var deltaCount = 0;
            str.AppendLine();
            str.Append("Length:");
            str.Append(data.Length);
            str.AppendLine();
            while (data.Position < data.Length) {
                byte type = default;
                data.Read(ref type);
                uint pos = default;
                data.Read(ref pos);
                ++deltaCount;
                str.Append("Offset:");
                str.Append(pos);
                str.AppendLine();
                if (type == 0) {
                    uint count = default;
                    data.Read(ref count);
                    while (count > 0u) {
                        Unity.Burst.Intrinsics.v256 val = default;
                        data.Read(ref val);
                        str.Append("Data:");
                        str.Append(val);
                        str.AppendLine();
                        --count;
                    }
                } else {
                    uint count = default;
                    data.Read(ref count);
                    for (int i = 0; i < count; ++i) {
                        byte val = default;
                        data.Read(ref val);
                        str.Append("Data Tail:");
                        str.Append(val);
                        str.AppendLine();
                    }
                }
            }
            
            str.Insert(0, deltaCount);
            str.Insert(0, "Delta:");
            return str.ToString();
            
        }

    }

    public unsafe struct StreamBufferReader {

        private readonly safe_ptr<byte> arr;
        private readonly uint arrSize;
        private uint position;
        
        public uint Length => this.arrSize;
        public uint Position => this.position;

        [INLINE(256)]
        public StreamBufferReader(StreamBufferWriter writer) {
            
            writer.MoveTo(0u);
            this.arr = _makeArray<byte>(writer.Length);
            _memcpy(writer.GetPointer(), this.arr, writer.Length);
            this.arrSize = writer.Length;
            this.position = 0u;
            
        }

        [INLINE(256)]
        public StreamBufferReader(byte[] bytes) {

            this = default;
            if (bytes.Length == 0) return;
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

        public uint Length => this.arrSize;
        public uint Position => this.position;

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
        public readonly byte[] ToArray() {
            
            if (this.position == 0u) return System.Array.Empty<byte>();

            var bytes = new byte[this.position];
            fixed (byte* ptr = &bytes[0]) {
                _memcpy(this.arr, (safe_ptr)ptr, this.position);
            }
            return bytes;

        }

        [INLINE(256)]
        public void SetCapacity(uint size) {
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

        [INLINE(256)]
        public void MoveTo(uint position) {
            this.position = position;
        }

    }

}