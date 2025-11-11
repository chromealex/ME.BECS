namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    [System.AttributeUsageAttribute(System.AttributeTargets.Field)]
    public class QueryWithAttribute : System.Attribute {}
    
    public interface IAspectData {}

    [IgnoreProfiler]
    public unsafe struct AspectDataPtr<T> : IAspectData where T : unmanaged, IComponent {

        public RefRW<T> value;
        public RefRO<T> valueRO;
        
        public AspectDataPtr(in World world) {
            if (StaticTypes<T>.typeId == 0u) {
                StaticTypes<T>.Validate(isTag: false);
            }
            this.value = world.state.ptr->components.GetRW<T>(world.state, world.id);
            this.valueRO = world.state.ptr->components.GetRO<T>(world.state, world.id);
        }

        [INLINE(256)]
        [SafetyCheck(RefOp.ReadWrite)] public readonly ref T Get(uint entId, ushort gen) {
            return ref this.value.Get(entId, gen);
        }

        [INLINE(256)]
        [SafetyCheck(RefOp.ReadOnly)] public readonly ref readonly T Read(uint entId, ushort gen) {
            return ref this.valueRO.Read(entId, gen);
        }

    }
    
    [IgnoreProfiler]
    public struct AspectTypeInfoLoadedManaged {

        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();

    }
    
    [IgnoreProfiler]
    public struct AspectTypeInfo {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfo>();
        public static ref uint counter => ref counterBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10402);
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10401);
        public static ref ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>> with => ref withBurst.Data;
        
    }

    [IgnoreProfiler]
    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }
    
    [IgnoreProfiler]
    public struct AspectTypeInfo<T> where T : unmanaged, IAspect {

        public static ref uint typeId => ref AspectTypeInfoId<T>.typeIdBurst.Data;

        [INLINE(256)]
        public static void Validate() {

            if (typeId == 0u) {
                typeId = ++AspectTypeInfo.counter;
                AspectTypeInfoLoadedManaged.loadedTypes.Add(typeId, typeof(T));
                AspectTypeInfoLoadedManaged.typeToId.Add(typeof(T), typeId);
            }
            
            AspectTypeInfo.sizes.Resize(typeId + 1u);
            AspectTypeInfo.sizes.Get(typeId) = TSize<T>.size;

            AspectTypeInfo.with.Resize(typeId + 1u);

        }

    }

    [IgnoreProfiler]
    public struct WorldAspectStorage {

        public static readonly Unity.Burst.SharedStatic<Internal.Array<UnsafeAspectsStorage>> storage = Unity.Burst.SharedStatic<Internal.Array<UnsafeAspectsStorage>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldAspectStorage>(TAlign<Internal.Array<UnsafeAspectsStorage>>.align, 109L);

        [INLINE(256)]
        public static void AddWorld(in World world) {
            
            storage.Data.Resize(world.id + 1u);
            storage.Data.Get(world.id) = UnsafeAspectsStorage.Create();

        }

        [INLINE(256)]
        public static void DisposeWorld(in World world) {

            if (world.id >= storage.Data.Length) return;
            storage.Data.Get(world.id).Dispose();

        }

        [INLINE(256)]
        public static ref T Initialize<T>(ushort worldId) where T : unmanaged, IAspect {
            
            return ref storage.Data.Get(worldId).Initialize<T>();

        }

        [INLINE(256)]
        public static safe_ptr Initialize(ushort worldId, uint typeId, uint size) {
            
            return storage.Data.Get(worldId).Initialize(typeId, size);

        }

    }

    [IgnoreProfiler]
    public unsafe struct UnsafeAspectsStorage {

        public struct Aspect {

            public safe_ptr constructedAspect;
            public LockSpinner lockSpinner;

            [INLINE(256)]
            public void Dispose() {
                _free(this.constructedAspect);
            }

        }

        public Unity.Collections.NativeArray<Aspect> list;
        
        [INLINE(256)]
        public static UnsafeAspectsStorage Create() {
            var aspectsCount = AspectTypeInfo.counter + 1u;
            var storage = new UnsafeAspectsStorage() {
                list = new Unity.Collections.NativeArray<Aspect>((int)aspectsCount, Constants.ALLOCATOR_DOMAIN),
            };
            return storage;
        }

        [INLINE(256)]
        public void Dispose() {
            foreach (var item in this.list) {
                item.Dispose();
            }
            this.list.Dispose();
        }

        [INLINE(256)]
        public ref T Initialize<T>() where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            return ref *(T*)this.Initialize(typeId, TSize<T>.size).ptr;
            
        }

        [INLINE(256)]
        public safe_ptr Initialize(uint typeId, uint size) {
            
            var item = (Aspect*)((byte*)this.list.GetUnsafePtr() + TSize<Aspect>.size * typeId);
            if (item->constructedAspect.ptr == null) {
                item->lockSpinner.Lock();
                if (item->constructedAspect.ptr == null) {
                    item->constructedAspect = _make(size, TAlign<byte>.alignInt, Constants.ALLOCATOR_DOMAIN);
                }
                item->lockSpinner.Unlock();
            }

            return item->constructedAspect;

        }

        [INLINE(256)]
        public static void SetAspect(safe_ptr<State> state, in Ent ent, uint aspectTypeId) {

            for (uint i = 0u; i < AspectTypeInfo.with.Get(aspectTypeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(aspectTypeId).Get(i);
                var has = Components.HasUnknownType(state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    Components.SetUnknownType(state, typeId, StaticTypes.tracker.Get(typeId), in ent, (void*)StaticTypes.defaultValues.Get(typeId));
                    Batches.Set_INTERNAL(typeId, in ent);
                }

            }

        }

    }

}