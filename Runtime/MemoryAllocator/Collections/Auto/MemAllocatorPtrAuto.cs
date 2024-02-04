using Unity.Jobs;
using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

namespace ME.BECS {

    [System.Serializable]
    public unsafe struct MemAllocatorPtrAuto<T> : IIsCreated where T : unmanaged {
        
        public static readonly MemAllocatorPtrAuto<T> Empty = new MemAllocatorPtrAuto<T>() {
            ptr = MemPtr.Invalid,
            ent = Ent.Null,
        };

        internal MemPtr ptr;
        public Ent ent;
        
        public readonly bool isCreated {
            [INLINE(256)]
            get => this.ptr.IsValid();
        }

        [INLINE(256)]
        public long AsLong() => this.ptr.AsLong();

        [INLINE(256)]
        public bool IsValid() {
            return this.ptr.IsValid();
        }

        public MemAllocatorPtrAuto(in Ent ent, in T obj) {

            this.ent = ent;
            this.ptr = MemPtr.Invalid;
            this.Set(obj);

        }
        
        [INLINE(256)]
        public void ReplaceWith(in MemAllocatorPtrAuto<T> other) {
            
            if (other.ptr == this.ptr) {
                return;
            }
            
            this.Dispose();
            this = other;
            
        }

        [INLINE(256)]
        public readonly ref T As() {

            var state = this.ent.World.state;
            return ref state->allocator.Ref<T>(this.ptr);

        }

        [INLINE(256)]
        public readonly T* AsPtr(uint offset = 0u) {

            return (T*)this.GetUnsafePtr(offset);

        }

        public readonly void* GetUnsafePtr(uint offset = 0u) {

            var state = this.ent.World.state;
            return (T*)MemoryAllocatorExt.GetUnsafePtr(in state->allocator, this.ptr, offset);

        }

        [INLINE(256)]
        public void Set(in T data) {

            E.IS_ALREADY_CREATED(this);
            var state = this.ent.World.state;
            this.ptr = state->allocator.Alloc<T>();
            state->allocator.Ref<T>(this.ptr) = data;
            state->collectionsRegistry.Add(state, in ent, in this.ptr);

        }

        [INLINE(256)]
        public void Set(void* data, uint dataSize) {

            E.IS_ALREADY_CREATED(this);
            var state = this.ent.World.state;
            this.ptr = MemoryAllocatorExt.Alloc(ref state->allocator, dataSize, out var ptr);
            if (data != null) {
                Cuts._memcpy(data, ptr, dataSize);
            } else {
                Cuts._memclear(ptr, dataSize);
            }
            state->collectionsRegistry.Add(state, in ent, in this.ptr);

        }
        
        [INLINE(256)]
        public void Dispose() {

            var state = this.ent.World.state;
            state->collectionsRegistry.Remove(state, in this.ent, in this.ptr);
            if (this.ptr.IsValid() == true) {
                state->allocator.Free(this.ptr);
            }
            this = default;

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle inputDeps) {
            
            var jobHandle = new DisposeAutoJob() {
                ptr = this.ptr,
                ent = this.ent,
                worldId = this.ent.World.id,
            }.Schedule(inputDeps);

            this = default;

            return jobHandle;

        }

    }

}
