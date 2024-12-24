namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public readonly unsafe struct CommandBufferJobParallel {

        public readonly safe_ptr<CommandBuffer> buffer;
        public readonly uint index;
        public uint Count => this.buffer.ptr->count;
        public readonly uint entId;
        public readonly ushort entGen;
        public Ent ent => new Ent(this.entId, this.entGen, this.buffer.ptr->worldId);

        [INLINE(256)]
        public CommandBufferJobParallel(safe_ptr<CommandBuffer> buffer, uint index) {
            this.buffer = buffer;
            this.index = index;
            this.entId = this.buffer.ptr->entities[index];
            this.entGen = Ents.GetGeneration(this.buffer.ptr->state, this.entId);
        }

        [INLINE(256)]
        public ref readonly T Read<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Read<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public ref T Get<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Get<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public bool Set<T>(in T data) where T : unmanaged, IComponent {

            return this.buffer.ptr->Set<T>(this.entId, this.entGen, in data);

        }

        [INLINE(256)]
        public bool Remove<T>() where T : unmanaged, IComponent {

            return this.buffer.ptr->Remove<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public bool Has<T>(bool checkEnabled = true) where T : unmanaged, IComponent {

            return this.buffer.ptr->Has<T>(this.entId, this.entGen, checkEnabled);

        }

    }

    public readonly unsafe struct CommandBufferJobBatch {

        private readonly safe_ptr<CommandBuffer> buffer;
        public readonly uint fromIndex;
        public readonly uint toIndex;
        public uint Count => this.buffer.ptr->count;

        [INLINE(256)]
        public CommandBufferJobBatch(safe_ptr<CommandBuffer> buffer, uint fromIndex, uint toIndex) {
            this.buffer = buffer;
            this.fromIndex = fromIndex;
            this.toIndex = toIndex;
        }

        [INLINE(256)]
        public ref readonly T Read<T>(uint index) where T : unmanaged, IComponent {

            var entId = this.buffer.ptr->entities[index];
            return ref this.buffer.ptr->Read<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        [INLINE(256)]
        public ref T Get<T>(uint index) where T : unmanaged, IComponent {

            var entId = this.buffer.ptr->entities[index];
            return ref this.buffer.ptr->Get<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        [INLINE(256)]
        public bool Set<T>(uint index, in T data) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Set<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId), in data);

        }

        [INLINE(256)]
        public bool Remove<T>(uint index) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Remove<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId));

        }

        [INLINE(256)]
        public bool Has<T>(uint index, bool checkEnabled = true) where T : unmanaged, IComponent {
            
            var entId = this.buffer.ptr->entities[index];
            return this.buffer.ptr->Has<T>(entId, Ents.GetGeneration(this.buffer.ptr->state, entId), checkEnabled);

        }

    }
    
    public readonly unsafe struct CommandBufferJob {

        private readonly uint entId;
        private readonly ushort entGen;
        public readonly safe_ptr<CommandBuffer> buffer;
        public uint Count => this.buffer.ptr->count;
        public Ent ent => new Ent(this.entId, this.entGen, this.buffer.ptr->worldId);
        
        [INLINE(256)]
        public CommandBufferJob(in uint entId, ushort gen, safe_ptr<CommandBuffer> buffer) {
            this.entId = entId;
            this.entGen = gen;
            this.buffer = buffer;
        }

        [INLINE(256)]
        public ref readonly T Read<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Read<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public ref T Get<T>() where T : unmanaged, IComponent {

            return ref this.buffer.ptr->Get<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public bool Set<T>(in T data) where T : unmanaged, IComponent {

            return this.buffer.ptr->Set<T>(this.entId, this.entGen, in data);

        }

        [INLINE(256)]
        public bool Remove<T>() where T : unmanaged, IComponent {

            return this.buffer.ptr->Remove<T>(this.entId, this.entGen);

        }

        [INLINE(256)]
        public bool Has<T>(bool checkEnabled = true) where T : unmanaged, IComponent {

            return this.buffer.ptr->Has<T>(this.entId, this.entGen, checkEnabled);

        }

    }

    [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct CommandBuffer {

        // [!] For some reason ScheduleParallelForDeferArraySize needs ptr first
        // so that's why we need LayoutKind.Sequential and first void* must be here
        // the second must be uint count
        [NativeDisableUnsafePtrRestriction]
        public uint* entities;
        public uint count;
        
        public safe_ptr<State> state;
        public ushort worldId;

        public bool sync;

        [INLINE(256)]
        public void BeginForEachRange(uint fromIndex, uint toIndex) {
            
        }

        [INLINE(256)]
        public void EndForEachRange() {
            
        }

        [INLINE(256)]
        public void Dispose() {

            //if (this.entities != null) _freeArray(this.entities, this.count, Constants.ALLOCATOR_TEMP_ST.ToAllocator);
            this = default;
            
        }

        [INLINE(256)]
        public ref readonly T Read<T>(uint id, ushort gen) where T : unmanaged, IComponent {

            return ref Components.Read<T>(this.state, id, gen);

        }

        [INLINE(256)]
        public ref T Get<T>(uint id, ushort gen) where T : unmanaged, IComponent {
            
            if (this.sync == false && this.Has<T>(id, gen, checkEnabled: true) == false) {
                E.THREAD_CHECK(nameof(this.Get));
            }
            E.IS_IN_TICK(this.state);
            var ent = new Ent(id, gen, this.worldId);
            return ref Components.Get<T>(this.state, ent);

        }

        [INLINE(256)]
        public bool Set<T>(uint id, ushort gen, in T data) where T : unmanaged, IComponent {

            if (this.sync == false && this.Has<T>(id, gen, checkEnabled: true) == false) {
                E.THREAD_CHECK(nameof(this.Set));
                return false;
            }
            E.IS_IN_TICK(this.state);
            var ent = new Ent(id, gen, this.worldId);
            return Components.SetUnknownType(this.state, StaticTypes<T>.typeId, StaticTypes<T>.groupId, in ent, in data);

        }

        [INLINE(256)]
        public bool Remove<T>(uint id, ushort gen) where T : unmanaged, IComponent {

            if (this.sync == false) {
                E.THREAD_CHECK(nameof(this.Remove));
                return false;
            } else {
                E.IS_IN_TICK(this.state);
                var ent = new Ent(id, gen, this.worldId);
                return Components.RemoveUnknownType(this.state, StaticTypes<T>.typeId, StaticTypes<T>.groupId, in ent);
            }

        }

        [INLINE(256)]
        public bool Has<T>(uint id, ushort gen, bool checkEnabled) where T : unmanaged, IComponent {

            return Components.Has<T>(this.state, id, gen, checkEnabled);

        }

    }

}