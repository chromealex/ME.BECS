using Unity.Jobs;

namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Collections;
    using static CutsPool;

    public readonly unsafe struct UnsafeEntityConfig {

        private readonly struct SharedData<T> where T : class, IConfigComponentShared {

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* hashes;

            [INLINE(256)]
            public SharedData(T[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                
                this.offsets = _makeArray<uint>(cnt);
                this.typeIds = _makeArray<uint>(cnt);
                this.hashes = _makeArray<uint>(cnt);
                this.count = cnt;

                var offset = 0u;
                var size = 0u;
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    StaticTypesLoadedManaged.loadedSharedTypesCustomHash.TryGetValue(typeId, out var hasCustomHash);
                    E.IS_VALID_TYPE_ID(typeId);
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    size += elemSize;
                    this.offsets[i] = offset;
                    this.typeIds[i] = typeId;
                    this.hashes[i] = hasCustomHash == true ? comp.GetHash() : Components.COMPONENT_SHARED_DEFAULT_HASH;
                    offset += elemSize;
                }
                this.data = (byte*)UnsafeUtility.Malloc(size, 4, Allocator.Persistent);

                for (int i = 0; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    UnsafeUtility.MemCpy(this.data + this.offsets[i], (void*)ptr, elemSize);
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    var groupId = StaticTypes.groups.Get(typeId);
                    var dataSize = StaticTypes.sizes.Get(typeId);
                    var sharedTypeId = StaticTypes.sharedTypeId.Get(typeId);
                    var hash = this.hashes[i];
                    state->batches.SetShared(ent.id, groupId, data, dataSize, typeId, sharedTypeId, state, hash);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                UnsafeUtility.Free(this.data, Allocator.Persistent);
                _freeArray(this.hashes, this.count);
                _freeArray(this.offsets, this.count);
                _freeArray(this.typeIds, this.count);

            }

        }

        private readonly struct Data<T> where T : class {

            [NativeDisableUnsafePtrRestriction]
            private readonly byte* data;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* offsets;
            private readonly uint count;
            [NativeDisableUnsafePtrRestriction]
            private readonly uint* typeIds;

            [INLINE(256)]
            public Data(T[] components) {
                
                var cnt = (uint)components.Length;
                if (cnt == 0u) {
                    this = default;
                    return;
                }
                this.offsets = _makeArray<uint>(cnt);
                this.typeIds = _makeArray<uint>(cnt);
                this.count = cnt;
                
                var offset = 0u;
                var size = 0u;
                for (uint i = 0u; i < components.Length; ++i) {
                    var comp = components[i];
                    StaticTypesLoadedManaged.typeToId.TryGetValue(comp.GetType(), out var typeId);
                    E.IS_VALID_TYPE_ID(typeId);
                    var elemSize = StaticTypes.sizes.Get(typeId);
                    size += elemSize;
                    this.offsets[i] = offset;
                    this.typeIds[i] = typeId;
                    offset += elemSize;
                }
                this.data = (byte*)UnsafeUtility.Malloc(size, 4, Allocator.Persistent);

                for (int i = 0; i < components.Length; ++i) {
                    var comp = components[i];
                    var gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(comp, System.Runtime.InteropServices.GCHandleType.Pinned);
                    var ptr = gcHandle.AddrOfPinnedObject();
                    var elemSize = StaticTypes.sizes.Get(this.typeIds[i]);
                    UnsafeUtility.MemCpy(this.data + this.offsets[i], (void*)ptr, elemSize);
                    gcHandle.Free();
                }

            }

            [INLINE(256)]
            public void Apply(in Ent ent) {

                var state = ent.World.state;
                for (uint i = 0; i < this.count; ++i) {
                    var data = this.data + this.offsets[i];
                    var typeId = this.typeIds[i];
                    state->batches.Set(ent.id, ent.gen, typeId, data, state);
                }

            }

            [INLINE(256)]
            public void Dispose() {

                UnsafeUtility.Free(this.data, Allocator.Persistent);
                _freeArray(this.offsets, this.count);
                _freeArray(this.typeIds, this.count);

            }

            [INLINE(256)]
            public bool TryRead<TComponent>(out TComponent data) where TComponent : unmanaged, IComponentStatic {

                data = default;
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        data = *(TComponent*)(this.data + this.offsets[i]);
                        return true;
                    }
                }

                return false;

            }

            [INLINE(256)]
            public bool Has<TComponent>() where TComponent : unmanaged, IComponentStatic {
                
                var typeId = StaticTypes<TComponent>.typeId;
                for (uint i = 0; i < this.count; ++i) {
                    if (this.typeIds[i] == typeId) {
                        return true;
                    }
                }

                return false;
                
            }

        }

        [NativeDisableUnsafePtrRestriction]
        private readonly UnsafeEntityConfig* baseConfig;
        private readonly Data<IConfigComponent> data;
        private readonly SharedData<IConfigComponentShared> dataShared;
        private readonly Data<IComponentStatic> dataStatic;
        private readonly uint id;

        [INLINE(256)]
        public UnsafeEntityConfig(EntityConfig config, uint id = 0u) {
            
            this.id = id > 0u ? id : EntityConfigRegistry.Register(config, out _);
            this.data = new Data<IConfigComponent>(config.data.components);
            this.dataShared = new SharedData<IConfigComponentShared>(config.sharedData.components);
            this.dataStatic = new Data<IComponentStatic>(config.staticData.components);

            this.baseConfig = null;
            if (config.baseConfig != null) {
                this.baseConfig = _make(new UnsafeEntityConfig(config.baseConfig));
            }

        }

        [INLINE(256)]
        public void Apply(in Ent ent) {

            if (this.IsValid() == false) {
                throw new System.Exception();
            }
            
            ent.Set(new EntityConfigComponent() {
                id = this.id,
            });

            this.data.Apply(ent);
            this.dataShared.Apply(ent);
            
        }

        [BURST]
        private struct ConfigDisposeJob : Unity.Jobs.IJob {

            public UnsafeEntityConfig config;
            public void Execute() {
                this.config.Dispose();
            }

        }

        [INLINE(256)]
        public Unity.Jobs.JobHandle Dispose(Unity.Jobs.JobHandle dependsOn) {
            dependsOn = new ConfigDisposeJob() {
                config = this,
            }.Schedule(dependsOn);
            return dependsOn;
        }
        
        [INLINE(256)]
        public void Dispose() {

            this.data.Dispose();
            this.dataShared.Dispose();
            this.dataStatic.Dispose();
            if (this.baseConfig != null) this.baseConfig->Dispose();

        }

        [INLINE(256)]
        public bool IsValid() {
            return this.id > 0u;
        }

        [INLINE(256)]
        public bool HasStatic<T>() where T : unmanaged, IComponentStatic {
            
            if (this.dataStatic.Has<T>() == true) {
                return true;
            }

            if (this.baseConfig != null) {
                return this.baseConfig->HasStatic<T>();
            }
            
            return false;

        }

        [INLINE(256)]
        public T ReadStatic<T>() where T : unmanaged, IComponentStatic {

            if (this.dataStatic.TryRead<T>(out var data) == true) {
                return data;
            }

            if (this.baseConfig != null) {
                return this.baseConfig->ReadStatic<T>();
            }
            
            return default;

        }

    }

}