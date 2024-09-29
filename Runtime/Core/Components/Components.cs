namespace ME.BECS {
    
    using static Cuts;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using System.Runtime.InteropServices;

    public readonly ref struct ComponentsFastTrack {

        public readonly TempBitArray root;
        public readonly uint maxId;
        public readonly uint hash;
        public readonly uint Count;

        [INLINE(256)]
        public static ComponentsFastTrack Create(in BatchList list) {
            return new ComponentsFastTrack(in list);
        }

        [INLINE(256)]
        public ComponentsFastTrack(in BatchList list) {
            if (list.Count == 0u) {
                this = default;
                return;
            }

            this.root = list.list;
            this.hash = list.hash;
            this.maxId = list.maxId;
            this.Count = list.Count;
        }

        [INLINE(256)]
        public bool Contains(uint value) {
            if (this.maxId == 0u || value > this.maxId) return false;
            return this.root.IsSet((int)value);
        }
        
        public override string ToString() {
            return string.Join(", ", new TempBitArrayDebugView(this.root).BitIndexes);
        }

    }
    
    public unsafe partial struct Components {

        [INLINE(256)]
        public static Components Create(State* state, in StateProperties stateProperties) {

            var components = new Components() {
                items = new MemArray<MemAllocatorPtr>(ref state->allocator, StaticTypes.counter + 1u),
            }.InitializeSharedComponents(state, stateProperties);
            
            for (uint i = 1u; i < components.items.Length; ++i) {
                ref var ptr = ref components.items[in state->allocator, i];
                var dataSize = StaticTypes.sizes.Get(i);
                ptr.Set(ref state->allocator, new DataDenseSet(state, dataSize, stateProperties.entitiesCapacity));
            }

            return components;

        }

        [INLINE(256)]
        public void BurstMode(in MemoryAllocator allocator, bool state) {
            this.items.BurstMode(in allocator, state);
            for (uint i = 1u; i < this.items.Length; ++i) {
                ref var ptr = ref this.items[in allocator, i];
                ptr.AsPtr<DataDenseSet>(in allocator)->BurstMode(in allocator, state);
            }
        }

        [INLINE(256)]
        public RefRW<T> GetRW<T>(State* state, ushort worldId) where T : unmanaged, IComponent {
            if (StaticTypes<T>.isTag == true) return default;
            return new RefRW<T>() {
                state = state,
                storage = this.GetUnsafeSparseSetPtr(state, StaticTypes<T>.typeId),
                worldId = worldId,
            };
        }

        [INLINE(256)]
        public RefRO<T> GetRO<T>(State* state) where T : unmanaged, IComponent {
            if (StaticTypes<T>.isTag == true) return default;
            return new RefRO<T>() {
                state = state,
                storage = this.GetUnsafeSparseSetPtr(state, StaticTypes<T>.typeId),
            };
        }

    }

}