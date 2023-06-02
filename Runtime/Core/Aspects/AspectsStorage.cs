namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public class QueryWithAttribute : System.Attribute {}
    
    public struct T1 : IComponent {

        public int data;
        public byte test;

    }

    public struct T2 : IComponent {

        public int data;

    }

    public struct TestAspect : IAspect {
        
        public Ent ent { get; set; }

        public AspectDataPtr<T1> t1Value;
        public AspectDataPtr<T2> t2Value;

        public ref T1 t1 => ref this.t1Value.Get(this.ent);
        public ref T2 t2 => ref this.t2Value.Get(this.ent);

    }

    public interface IAspectData {}

    public unsafe struct AspectDataPtr<T> : IAspectData where T : unmanaged, IComponent {

        public RefRW<T> value;
        
        public AspectDataPtr(in World world) {

            this.value = world.state->components.GetRW<T>(world.state);

        }

        [INLINE(256)]
        public ref T Get(Ent ent) {

            return ref this.value.Get(ent);

        }

    }

    public struct AspectTypeInfoCounter {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoCounter>();

    }

    public struct AspectTypeInfoId<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<uint> typeIdBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<AspectTypeInfoId<T>>();

    }

    public struct AspectTypeInfoWith<T> where T : unmanaged, IAspect {

        public static readonly Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>> withBurst = Unity.Burst.SharedStatic<ME.BECS.Internal.Array<uint>>.GetOrCreatePartiallyUnsafeWithHashCode<AspectTypeInfoWith<T>>(TAlign<ME.BECS.Internal.Array<uint>>.align, 10400);

    }

    public struct AspectTypeInfo<T> where T : unmanaged, IAspect {

        public static ref ME.BECS.Internal.Array<uint> with => ref AspectTypeInfoWith<T>.withBurst.Data;
        public static ref uint typeId => ref AspectTypeInfoId<T>.typeIdBurst.Data;

        [INLINE(256)]
        public static void Validate() {

            if (typeId == 0u) {
                typeId = ++AspectTypeInfoCounter.value.Data;
            }
            
        }

    }

    public static unsafe class EntAspectsExt {

        public static void Set<T>(in this Ent ent) where T : unmanaged, IAspect {

            var world = ent.World;
            for (uint i = 0u; i < AspectTypeInfo<T>.with.Length; ++i) {

                var typeId = AspectTypeInfo<T>.with.Get(i);
                var has = world.state->components.HasUnknownType(world.state, typeId, ent.id, ent.gen);
                if (has == false) {
                    world.state->components.SetUnknownType(world.state, typeId, 0u, ent.id, ent.gen, null);
                    world.state->batches.Set_INTERNAL(typeId, ent.id, world.state);
                }

            }

        }

    }

    public unsafe struct AspectsStorage {

        public struct Aspect {

            public MemPtr constructedAspect;
            public ushort version;

        }

        public MemArray<Aspect> list;
        
        public uint GetReservedSizeInBytes(State* state) {

            if (this.list.isCreated == false) return 0u;

            return this.list.GetReservedSizeInBytes();

        }

        [INLINE(256)]
        public static AspectsStorage Create(State* state) {
            var aspectsCount = AspectTypeInfoCounter.value.Data + 1u;
            var storage = new AspectsStorage() {
                list = new MemArray<Aspect>(ref state->allocator, aspectsCount),
            };
            
            return storage;
        }

        [INLINE(256)]
        public ref T Initialize<T>(State* state) where T : unmanaged, IAspect {
            
            var typeId = AspectTypeInfo<T>.typeId;
            ref var item = ref this.list[state, typeId];
            if (item.constructedAspect.IsValid() == false) {
                item = new Aspect() {
                    constructedAspect = MemoryAllocatorExt.Alloc(ref state->allocator, sizeof(T)),
                    version = state->allocator.version,
                };
            }

            return ref *(T*)MemoryAllocatorExt.GetUnsafePtr(in state->allocator, in item.constructedAspect);

        }

    }

}