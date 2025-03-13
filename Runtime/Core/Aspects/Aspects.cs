#define NO_INLINE

namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    
    public interface IAspect {

        Ent ent { get; set; }
        
    }

    public enum RefOp {
        ReadOnly  = 0,
        WriteOnly = 1,
        ReadWrite = 2,
    }

    public interface IRefOp {
        RefOp Op { get; }
    }

    [System.AttributeUsageAttribute(System.AttributeTargets.Field | System.AttributeTargets.Method)]
    public class DisableContainerSafetyRestrictionAttribute : System.Attribute {

    }

    public class SafetyCheckAttribute : System.Attribute {

        public RefOp Op { get; set; }

        public SafetyCheckAttribute(RefOp op) {
            this.Op = op;
        }

    }
    
    public unsafe struct SafetyComponentContainerRO<T> where T : unmanaged, IComponentBase {

        public SafetyComponentContainerRO(safe_ptr<State> state, ushort worldId) {
        }

    }

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct SafetyComponentContainerWO<T> where T : unmanaged, IComponentBase {

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        #pragma warning disable
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore
        #endif
        
        public SafetyComponentContainerWO(safe_ptr<State> state, ushort worldId) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

    }

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct SafetyComponentContainerRW<T> where T : unmanaged, IComponentBase {

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        #pragma warning disable
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #pragma warning restore
        #endif
        
        public SafetyComponentContainerRW(safe_ptr<State> state, ushort worldId) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

    }

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct RefROSafe<T> : IRefOp where T : unmanaged, IComponentBase {

        public RefOp Op => RefOp.ReadOnly;
        
        private ME.BECS.RefRO<T> data;
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #endif

        public RefROSafe(safe_ptr<State> state, ushort worldId) {
            this.data = state.ptr->components.GetRO<T>(state, worldId);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref readonly T Read(uint entId, ushort gen) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
            if (entId < this.m_MinIndex || entId > this.m_MaxIndex) this.ThrowMinMax(entId);
            #endif
            return ref this.data.Read(entId, gen);
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        [INLINE(256)]
        private readonly void ThrowMinMax(uint entId) {
            if (entId < this.m_MinIndex && (this.m_MinIndex != 0 || this.m_MaxIndex != this.m_Length - 1)) {
                throw new System.IndexOutOfRangeException(
                    $"Index {entId} is out of restricted IJobParallelFor range [{this.m_MinIndex}...{this.m_MaxIndex}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " + "reading & writing in parallel to the same elements from a job.");
            }
            throw new System.IndexOutOfRangeException($"Index {entId} is out of range of '{this.m_Length}' Length.");
        }
        #endif

    }

    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct RefRWSafe<T> : IRefOp where T : unmanaged, IComponentBase {

        public RefOp Op => RefOp.ReadWrite;

        private ME.BECS.RefRW<T> data;
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #endif

        public RefRWSafe(safe_ptr<State> state, ushort worldId) {
            this.data = state.ptr->components.GetRW<T>(state, worldId);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref T Get(uint entId, ushort gen) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(this.m_Safety);
            if (entId < this.m_MinIndex || entId > this.m_MaxIndex) this.ThrowMinMax(entId);
            #endif
            return ref this.data.Get(entId, gen);
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref readonly T Read(uint entId, ushort gen) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            //AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
            if (entId < this.m_MinIndex || entId > this.m_MaxIndex) this.ThrowMinMax(entId);
            #endif
            return ref this.data.Read(entId, gen);
        }

        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        [INLINE(256)]
        private readonly void ThrowMinMax(uint entId) {
            if (entId < this.m_MinIndex && (this.m_MinIndex != 0 || this.m_MaxIndex != this.m_Length - 1)) {
                throw new System.IndexOutOfRangeException(
                    $"Index {entId} is out of restricted IJobParallelFor range [{this.m_MinIndex}...{this.m_MaxIndex}] in ReadWriteBuffer.\n" +
                    "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                    "You can use double buffering strategies to avoid race conditions due to " + "reading & writing in parallel to the same elements from a job.");
            }
            throw new System.IndexOutOfRangeException($"Index {entId} is out of range of '{this.m_Length}' Length.");
        }
        #endif

    }

    public unsafe struct RefRW<T> : IRefOp, IIsCreated where T : unmanaged, IComponentBase {

        public RefOp Op => RefOp.ReadWrite;

        public safe_ptr<State> state;
        public MemAllocatorPtr storage;
        public ushort worldId;
        
        public bool IsCreated => this.state.ptr != null;
        
        [INLINE(256)]
        public RefRW(in World world) {
            this = world.state.ptr->components.GetRW<T>(world.state, world.id);
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref T Get(uint entId, ushort gen) {
            E.IS_CREATED(this);
            E.IS_IN_TICK(this.state);
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.groupId;
            var ent = new Ent(entId, gen, this.worldId);
            ref var res = ref *(T*)Components.GetUnknownType(this.state, this.storage, typeId, groupId, in ent, out var isNew, StaticTypes<T>.defaultValuePtr);
            if (isNew == true) {
                res = StaticTypes<T>.defaultValue;
                Journal.CreateComponent<T>(in ent, in res);
                Batches.Set_INTERNAL(typeId, in ent, this.state);
            } else {
                Journal.UpdateComponent<T>(in ent, in res);
            }
            return ref res;
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        internal readonly ref T GetReadonly(uint entId, ushort gen) {
            E.IS_CREATED(this);
            var typeId = StaticTypes<T>.typeId;
            ref var res = ref *(T*)Components.ReadUnknownType(this.state, this.storage, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValueGet;
            return ref res;
        }

        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref readonly T Read(uint entId, ushort gen) {
            E.IS_CREATED(this);
            var typeId = StaticTypes<T>.typeId;
            E.IS_NOT_TAG(typeId);
            ref var res = ref *(T*)Components.ReadUnknownType(this.state, this.storage, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }

    }

    public unsafe struct RefRO<T> : IRefOp, IIsCreated where T : unmanaged, IComponentBase {

        public RefOp Op => RefOp.ReadOnly;

        public safe_ptr<State> state;
        public MemAllocatorPtr storage;

        public bool IsCreated => this.state.ptr != null;

        [INLINE(256)]
        public RefRO(in World world) {
            this = world.state.ptr->components.GetRO<T>(world.state, world.id);
        }
        
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref readonly T Read(uint entId, ushort gen) {
            E.IS_CREATED(this);
            var typeId = StaticTypes<T>.typeId;
            E.IS_NOT_TAG(typeId);
            ref var res = ref *(T*)Components.ReadUnknownType(this.state, this.storage, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref res;
        }
        
    }

    public struct AspectStorage<T> where T : unmanaged, IAspect {

        [INLINE(256)]
        public static T GetAspect(in World world) {

            return InitAspect(in world);

        }

        [INLINE(256)]
        public static unsafe ref T InitAspect(in World world) {

            return ref world.state.ptr->aspectsStorage.Initialize<T>(world.state);

        }

    } 

    public static unsafe class AspectExt {

        [INLINE(256)]
        public static bool IsAlive<T>(this ref T aspect) where T : unmanaged, IAspect {

            return aspect.ent.IsAlive();

        }

        [INLINE(256)]
        public static T Set<T>(in this Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            
            var world = ent.World;
            AspectsStorage.SetAspect(world.state, in ent, AspectTypeInfo<T>.typeId);
            return ent.GetAspect<T>();
            
        }

        [INLINE(256)]
        public static T Get<T>(this in Ent ent) where T : unmanaged, IAspect {

            return ent.GetAspect<T>();

        }

        [INLINE(256)]
        public static T GetAspect<T>(this in EntRO ent) where T : unmanaged, IAspect => ent.ent.GetAspect<T>();

        [INLINE(256)]
        public static T GetOrCreateAspect<T>(this in EntRO ent) where T : unmanaged, IAspect => ent.ent.GetOrCreateAspect<T>();

        [INLINE(256)]
        public static T GetAspect<T>(this in Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            E.IS_VALID_FOR_ASPECT<T>(in ent);
            T aspect = AspectStorage<T>.GetAspect(in ent.World);
            aspect.ent = ent;
            return aspect;

        }

        [INLINE(256)]
        public static T GetOrCreateAspect<T>(this in Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            ent.Set<T>();
            return GetAspect<T>(in ent);

        }

        public static ref T InitializeAspect<T>(this in World world) where T : unmanaged, IAspect {
            
            return ref AspectStorage<T>.InitAspect(in world);
            
        }
        
    }

    /*
    public ref struct AspectQueryBuilder {

        internal QueryBuilder builder;
        internal QueryBuilderStatic builderStatic;

        [INLINE(256)]
        public AspectQueryBuilder(in QueryBuilder builder) {

            this = default;
            this.builder = builder;

        }

        [INLINE(256)]
        public AspectQueryBuilder(in QueryBuilderStatic builder) {

            this = default;
            this.builderStatic = builder;

        }

        [INLINE(256)]
        public AspectQueryBuilder WithAll<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.WithAll<T0, T1>();
            if (this.builder.isCreated == true) this.builder = this.builder.WithAll<T0, T1>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder WithAny<T0, T1>() where T0 : unmanaged, IComponent where T1 : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.WithAny<T0, T1>();
            if (this.builder.isCreated == true) this.builder = this.builder.WithAny<T0, T1>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder With<T>() where T : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.With<T>();
            if (this.builder.isCreated == true) this.builder = this.builder.With<T>();
            return this;
        }

        [INLINE(256)]
        public AspectQueryBuilder Without<T>() where T : unmanaged, IComponent {
            if (this.builderStatic.isCreated == true) this.builderStatic = this.builderStatic.Without<T>();
            if (this.builder.isCreated == true) this.builder = this.builder.Without<T>();
            return this;
        }

    }
    */

}