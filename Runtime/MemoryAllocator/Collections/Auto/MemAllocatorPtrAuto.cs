using Unity.Jobs;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

namespace ME.BECS {

    [System.Serializable]
    public unsafe struct MemAllocatorPtrAuto<T> : IIsCreated where T : unmanaged {
        
        public static readonly MemAllocatorPtrAuto<T> Empty = new MemAllocatorPtrAuto<T>() {
            beginPtr = MemPtr.Invalid,
            dataPtr = MemPtr.Invalid,
            alignment = 0,
            ent = Ent.Null,
        };

        internal MemPtr beginPtr; // 8
        private MemPtr dataPtr; // 8
        private uint alignment; // 4
        public Ent ent; // 8
        // 28 bytes total

        public MemPtr memBeginPtr => this.beginPtr;
        public MemPtr memDataPtr => this.dataPtr;
        
        public readonly bool isCreated {
            [INLINE(256)]
            get => this.beginPtr.IsValid();
        }

        [INLINE(256)]
        public bool IsValid() {
            return this.ent.IsAlive() && this.beginPtr.IsValid();
        }
        
        internal uint AlignSize(uint size) => this.alignment > 0 ? size + (this.alignment - 1) : size;

        internal void* AlignPtr(void* ptr) => this.alignment > 0 ? (void*)(((ulong)ptr + (this.alignment - 1)) & ~((ulong)this.alignment - 1)) : ptr;

        public MemAllocatorPtrAuto(in Ent ent, in T obj, uint alignment = 0) {

            this.ent = ent;
            this.beginPtr = MemPtr.Invalid;
            this.dataPtr = MemPtr.Invalid;
            this.alignment = alignment;
            this.Set(obj);

        }
        
        public MemAllocatorPtrAuto(in Ent ent, void* data, uint dataSize, uint alignment = 0) {

            this.ent = ent;
            this.beginPtr = MemPtr.Invalid;
            this.dataPtr = MemPtr.Invalid;
            this.alignment = alignment;
            this.Set(data, dataSize);

        }
        
        [INLINE(256)]
        public void ReplaceWith(in MemAllocatorPtrAuto<T> other) {
            
            if (other.beginPtr == this.beginPtr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        [INLINE(256)]
        public readonly ref T As() {

            var state = this.ent.World.state;
            return ref state->allocator.Ref<T>(this.dataPtr);

        }

        [INLINE(256)]
        public readonly T* AsPtr(uint offset = 0u) {

            return (T*)this.GetUnsafePtr(offset);

        }

        public readonly void* GetUnsafePtr(uint offset = 0u) {

            var state = this.ent.World.state;
            return (T*)MemoryAllocatorExt.GetUnsafePtr(in state->allocator, this.dataPtr, offset);

        }

        [INLINE(256)]
        private void Set(in T data) {

            E.IS_ALREADY_CREATED(this);
            var state = this.ent.World.state;
            this.beginPtr = MemoryAllocatorExt.Alloc(ref state->allocator, this.AlignSize((uint)sizeof(T)), out var ptr);
            ptr = this.AlignPtr(ptr);
            this.dataPtr = state->allocator.GetSafePtr(ptr, this.beginPtr.zoneId);
            state->allocator.Ref<T>(this.dataPtr) = data;
            state->collectionsRegistry.Add(state, in ent, in this.beginPtr);

        }

        [INLINE(256)]
        private void Set(void* data, uint dataSize) {

            E.IS_ALREADY_CREATED(this);
            var state = this.ent.World.state;
            var alignedSize = this.AlignSize(dataSize);
            this.beginPtr = MemoryAllocatorExt.Alloc(ref state->allocator, alignedSize, out var outPtr);
            var alignedPtr = this.AlignPtr(outPtr);
            if (data != null) {
                Cuts._memcpy(data, alignedPtr, dataSize);
            } else {
                Cuts._memclear(alignedPtr, dataSize);
            }
            this.dataPtr = state->allocator.GetSafePtr(alignedPtr, this.beginPtr.zoneId);
            state->collectionsRegistry.Add(state, in ent, in this.beginPtr);

        }
        
        [INLINE(256)]
        public void Dispose() {

            var state = this.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.beginPtr);
            if (this.beginPtr.IsValid() == true) {
                state->allocator.Free(this.beginPtr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.beginPtr,
                ent = this.ent,
                worldId = this.ent.World.id,
            }.Schedule(inputDeps);

            this = default;

            return jobHandle;

        }

    }

}
