namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;

    public class QueryWithAttribute : System.Attribute {}
    
    public interface IAspectData {}

    public unsafe struct AspectDataPtr<T> : IAspectData where T : unmanaged, IComponent {

        public RefRW<T> value;
        public RefRO<T> valueRO;
        
        public AspectDataPtr(in World world) {
            this.value = world.state.ptr->components.GetRW<T>(world.state, world.id);
            this.valueRO = world.state.ptr->components.GetRO<T>(world.state, world.id);
        }

        [INLINE(256)]
        public readonly ref T Get(uint entId, ushort gen) {
            return ref this.value.Get(entId, gen);
        }

        [INLINE(256)]
        public readonly ref readonly T Read(uint entId, ushort gen) {
            return ref this.valueRO.Read(entId, gen);
        }

    }
    public struct AspectTypeInfoLoadedManaged {

        public static readonly System.Collections.Generic.Dictionary<uint, System.Type> loadedTypes = new System.Collections.Generic.Dictionary<uint, System.Type>();
        public static readonly System.Collections.Generic.Dictionary<System.Type, uint> typeToId = new System.Collections.Generic.Dictionary<System.Type, uint>();

    }
    
    public struct AspectTypeInfo {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfo>();
        public static ref uint counter => ref counterBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> sizesBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10402);
        public static ref ME.BECS.Internal.Array<uint> sizes => ref sizesBurst.Data;

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfo>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10401);
        public static ref ME.BECS.Internal.Array<ME.BECS.Internal.Array<uint>> with => ref withBurst.Data;
        
    }

    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }

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

    public unsafe struct AspectsStorage {

        public struct Aspect {

            public MemPtr constructedAspect;
            public LockSpinner lockSpinner;
            public ushort version;

        }

        public MemArray<Aspect> list;
        
        public uint GetReservedSizeInBytes(safe_ptr<State> state) {

            if (this.list.IsCreated == false) return 0u;

            return this.list.GetReservedSizeInBytes();

        }

        [INLINE(256)]
        public static AspectsStorage Create(safe_ptr<State> state) {
            var aspectsCount = AspectTypeInfo.counter + 1u;
            var storage = new AspectsStorage() {
                list = new MemArray<Aspect>(ref state.ptr->allocator, aspectsCount),
            };
            
            return storage;
        }

        [INLINE(256)]
        public ref T Initialize<T>(safe_ptr<State> state) where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            return ref *(T*)this.Initialize(state, typeId, TSize<T>.size).ptr;
            
        }

        [INLINE(256)]
        public safe_ptr Initialize(safe_ptr<State> state, uint typeId, uint size) {
            
            ref var item = ref this.list[state, typeId];
            if (item.constructedAspect.IsValid() == false) {
                item.lockSpinner.Lock();
                if (item.constructedAspect.IsValid() == false) {
                    item.version = state.ptr->allocator.version;
                    item.constructedAspect = state.ptr->allocator.Alloc(size);
                }
                item.lockSpinner.Unlock();
            }

            return state.ptr->allocator.GetUnsafePtr(in item.constructedAspect);

        }

        [INLINE(256)]
        public static void SetAspect(safe_ptr<State> state, in Ent ent, uint aspectTypeId) {

            for (uint i = 0u; i < AspectTypeInfo.with.Get(aspectTypeId).Length; ++i) {

                var typeId = AspectTypeInfo.with.Get(aspectTypeId).Get(i);
                var has = Components.HasUnknownType(state, typeId, ent.id, ent.gen, checkEnabled: false);
                if (has == false) {
                    Components.SetUnknownType(state, typeId, StaticTypes.groups.Get(typeId), in ent, (void*)StaticTypes.defaultValues.Get(typeId));
                    Batches.Set_INTERNAL(typeId, in ent, state);
                }

            }

        }

    }

}