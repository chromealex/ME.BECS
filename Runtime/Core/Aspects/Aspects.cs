#define NO_INLINE

namespace ME.BECS {

    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;
    using System.Runtime.InteropServices;
    using LAYOUT = System.Runtime.InteropServices.StructLayoutAttribute;
    
    /// <summary>
    /// Exposes a typed view of the components belonging to an entity.
    /// </summary>
    public interface IAspect {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        Ent ent { get; set; }
        
    }

    /// <summary>
    /// Defines the supported ref op values.
    /// </summary>
    public enum RefOp {
        /// <summary>
        /// Read only option for <c>RefOp</c>.
        /// </summary>
        ReadOnly  = 0,
        /// <summary>
        /// Write only option for <c>RefOp</c>.
        /// </summary>
        WriteOnly = 1,
        /// <summary>
        /// Read write option for <c>RefOp</c>.
        /// </summary>
        ReadWrite = 2,
    }

    /// <summary>
    /// Defines the operations required by ref op.
    /// </summary>
    public interface IRefOp {
        /// <summary>
        /// Op used by <c>IRefOp</c>.
        /// </summary>
        RefOp Op { get; }
    }

    /// <summary>
    /// Supplies disable container safety restriction metadata to annotated declarations.
    /// </summary>
    [System.AttributeUsageAttribute(System.AttributeTargets.Field | System.AttributeTargets.Method)]
    public class DisableContainerSafetyRestrictionAttribute : System.Attribute {

    }

    /// <summary>
    /// Supplies safety check metadata to annotated declarations.
    /// </summary>
    [IgnoreProfiler]
    public class SafetyCheckAttribute : System.Attribute {

        /// <summary>
        /// Op used by <c>SafetyCheckAttribute</c>.
        /// </summary>
        public RefOp Op { get; set; }

        /// <summary>
        /// Initializes <c>SafetyCheckAttribute</c> from the supplied op.
        /// </summary>
        public SafetyCheckAttribute(RefOp op) {
            this.Op = op;
        }

    }
    
    /// <summary>
    /// Defines safety component container ro data used by entity processing.
    /// </summary>
    [IgnoreProfiler]
    public unsafe struct SafetyComponentContainerRO<T> where T : unmanaged, IComponentBase {

        /// <summary>
        /// Safety used by <c>SafetyComponentContainerRO</c>.
        /// </summary>
        [Unity.Collections.ReadOnly]
        public SafetyComponentContainerRW<T> safety;
        
        /// <summary>
        /// Initializes <c>SafetyComponentContainerRO</c> from the supplied state, world ID.
        /// </summary>
        public SafetyComponentContainerRO(safe_ptr<State> state, ushort worldId) {
            this.safety = new SafetyComponentContainerRW<T>(state, worldId);
        }

    }

    /// <summary>
    /// Defines safety component container wo data used by entity processing.
    /// </summary>
    [IgnoreProfiler]
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
        
        /// <summary>
        /// Initializes <c>SafetyComponentContainerWO</c> from the supplied state, world ID.
        /// </summary>
        public SafetyComponentContainerWO(safe_ptr<State> state, ushort worldId) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

    }

    /// <summary>
    /// Defines safety component container rw data used by entity processing.
    /// </summary>
    [IgnoreProfiler]
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
        
        /// <summary>
        /// Initializes <c>SafetyComponentContainerRW</c> from the supplied state, world ID.
        /// </summary>
        public SafetyComponentContainerRW(safe_ptr<State> state, ushort worldId) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

    }

    /// <summary>
    /// Provides read-only component access with native-container safety tracking.
    /// </summary>
    [IgnoreProfiler]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct RefROSafe<T> : IRefOp where T : unmanaged, IComponentBase {

        /// <summary>
        /// Gets op; this implementation returns <c>RefOp.ReadOnly</c>.
        /// </summary>
        public RefOp Op => RefOp.ReadOnly;
        
        private RefRO<T> data;
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #endif

        /// <summary>
        /// Initializes <c>RefROSafe</c> from the supplied state, world ID.
        /// </summary>
        public RefROSafe(safe_ptr<State> state, ushort worldId) {
            this.data = state.ptr->components.GetRO<T>(state, worldId);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

        /// <summary>
        /// Reads the requested value from ref ro safe.
        /// </summary>
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

        /// <summary>
        /// Returns a pointer for reading the component at the specified entity slot and generation.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly T* ReadPtr(uint entId, ushort gen) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
            if (entId < this.m_MinIndex || entId > this.m_MaxIndex) this.ThrowMinMax(entId);
            #endif
            return this.data.ReadPtr(entId, gen);
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

    /// <summary>
    /// Provides writable component access with native-container safety tracking.
    /// </summary>
    [IgnoreProfiler]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    public unsafe struct RefRWSafe<T> : IRefOp where T : unmanaged, IComponentBase {

        /// <summary>
        /// Gets op; this implementation returns <c>RefOp.ReadWrite</c>.
        /// </summary>
        public RefOp Op => RefOp.ReadWrite;

        private RefRW<T> data;
        #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
        private int m_Length;
        private int m_MinIndex;
        private int m_MaxIndex;
        #endif

        /// <summary>
        /// Initializes <c>RefRWSafe</c> from the supplied state, world ID.
        /// </summary>
        public RefRWSafe(safe_ptr<State> state, ushort worldId) {
            this.data = state.ptr->components.GetRW<T>(state, worldId);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            this.m_MinIndex = 0;
            this.m_MaxIndex = int.MaxValue - 1;
            this.m_Length = int.MaxValue;
            this.m_Safety = state.ptr->components.GetSafetyHandler<T>();
            #endif
        }

        /// <summary>
        /// Returns the requested entry from ref rw safe.
        /// </summary>
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

        /// <summary>
        /// Reads the requested value from ref rw safe.
        /// </summary>
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

        /// <summary>
        /// Returns a pointer for reading the component at the specified entity slot and generation.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly T* ReadPtr(uint entId, ushort gen) {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS && ENABLE_BECS_COLLECTIONS_CHECKS
            //AtomicSafetyHandle.CheckReadAndThrow(this.m_Safety);
            if (entId < this.m_MinIndex || entId > this.m_MaxIndex) this.ThrowMinMax(entId);
            #endif
            return this.data.ReadPtr(entId, gen);
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

    /// <summary>
    /// Provides writable access to component storage through a typed accessor.
    /// </summary>
    [IgnoreProfiler]
    [LAYOUT(LayoutKind.Sequential, Size = 24, Pack = 4)]
    public unsafe struct RefRW<T> : IRefOp, IIsCreated where T : unmanaged, IComponentBase {

        /// <summary>
        /// Gets op; this implementation returns <c>RefOp.ReadWrite</c>.
        /// </summary>
        public RefOp Op => RefOp.ReadWrite;

        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
        /// <summary>
        /// Storage containing the associated data.
        /// </summary>
        public MemAllocatorPtr storage;
        /// <summary>
        /// Identifier of the world whose state this value addresses.
        /// </summary>
        public ushort worldId;
        
        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.state.ptr != null;
        
        /// <summary>
        /// Initializes <c>RefRW</c> from the supplied world.
        /// </summary>
        [INLINE(256)]
        public RefRW(in World world) {
            this = world.state.ptr->components.GetRW<T>(world.state, world.id);
        }

        /// <summary>
        /// Returns the requested entry from ref rw.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref T Get(uint entId, ushort gen) {
            E.IS_CREATED(this);
            E.IS_IN_TICK(this.state);
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var ent = new Ent(entId, gen, this.worldId);
            ref var res = ref *(T*)Components.GetUnknownType(this.state, this.storage, typeId, groupId, in ent, out var isNew, StaticTypes<T>.defaultValuePtr);
            if (isNew == true) {
                res = StaticTypes<T>.defaultValue;
                Journal.CreateComponent<T>(in ent, in res);
                Batches.Set_INTERNAL(typeId, in ent);
            } else {
                Journal.UpdateComponent<T>(in ent, in res);
            }
            return ref res;
        }

        /// <summary>
        /// Returns or throw.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly ref T GetOrThrow(uint entId, ushort gen) {
            E.IS_CREATED(this);
            E.IS_IN_TICK(this.state);
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var ent = new Ent(entId, gen, this.worldId);
            ref var res = ref *(T*)Components.GetOrThrowUnknownType(this.state, this.storage, typeId, groupId, in ent, out var isNew, StaticTypes<T>.defaultValuePtr);
            if (isNew == true) {
                E.THROW_REQUIRED<T>(in ent);
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

        /// <summary>
        /// Reads the requested value from ref rw.
        /// </summary>
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

        /// <summary>
        /// Returns a pointer for reading the component at the specified entity slot and generation.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly T* ReadPtr(uint entId, ushort gen) {
            E.IS_CREATED(this);
            var typeId = StaticTypes<T>.typeId;
            E.IS_NOT_TAG(typeId);
            var res = (T*)Components.ReadUnknownType(this.state, this.storage, typeId, entId, gen, out var exists);
            if (exists == false) return null;
            return res;
        }

    }

    /// <summary>
    /// Provides read-only access to component storage through a typed accessor.
    /// </summary>
    [IgnoreProfiler]
    [LAYOUT(LayoutKind.Sequential, Size = 16)]
    public unsafe struct RefRO<T> : IRefOp, IIsCreated where T : unmanaged, IComponentBase {

        /// <summary>
        /// Gets op; this implementation returns <c>RefOp.ReadOnly</c>.
        /// </summary>
        public RefOp Op => RefOp.ReadOnly;

        /// <summary>
        /// State accessed by the containing operation.
        /// </summary>
        public safe_ptr<State> state;
        /// <summary>
        /// Storage containing the associated data.
        /// </summary>
        public MemAllocatorPtr storage;

        /// <summary>
        /// Whether the backing state has been initialized.
        /// </summary>
        public bool IsCreated => this.state.ptr != null;

        /// <summary>
        /// Initializes <c>RefRO</c> from the supplied world.
        /// </summary>
        [INLINE(256)]
        public RefRO(in World world) {
            this = world.state.ptr->components.GetRO<T>(world.state, world.id);
        }
        
        /// <summary>
        /// Reads the requested value from ref ro.
        /// </summary>
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

        /// <summary>
        /// Returns a pointer for reading the component at the specified entity slot and generation.
        /// </summary>
        #if !NO_INLINE
        [INLINE(256)]
        #endif
        public readonly T* ReadPtr(uint entId, ushort gen) {
            E.IS_CREATED(this);
            var typeId = StaticTypes<T>.typeId;
            E.IS_NOT_TAG(typeId);
            var res = (T*)Components.ReadUnknownType(this.state, this.storage, typeId, entId, gen, out var exists);
            if (exists == false) return null;
            return res;
        }

    }

    /// <summary>
    /// Stores and indexes aspect entries.
    /// </summary>
    [IgnoreProfiler]
    public struct AspectStorage<T> where T : unmanaged, IAspect {

        /// <summary>
        /// Returns a typed aspect view over the existing entity components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T GetAspect(in World world) {

            return InitAspect(in world);

        }

        /// <summary>
        /// Initializes aspect.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static unsafe ref T InitAspect(in World world) {

            return ref WorldAspectStorage.Initialize<T>(world.id);

        }

    } 

    /// <summary>
    /// Provides helper operations for aspect.
    /// </summary>
    [IgnoreProfiler]
    public static unsafe class AspectExt {

        /// <summary>
        /// Tests whether the referenced entity or world still matches its registered lifetime.
        /// </summary>
        [INLINE(256)]
        [CodeGeneratorIgnore]
        public static bool IsAlive<T>(this ref T aspect) where T : unmanaged, IAspect {

            return aspect.ent.IsAlive();

        }

        /// <summary>
        /// Stores the supplied value in aspect ext.
        /// </summary>
        [INLINE(256)]
        public static T Set<T>(in this Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            
            var world = ent.World;
            UnsafeAspectsStorage.SetAspect(world.state, in ent, AspectTypeInfo<T>.typeId);
            return ent.GetAspect<T>();
            
        }

        /// <summary>
        /// Returns the requested entry from aspect ext.
        /// </summary>
        [INLINE(256)]
        public static T Get<T>(this in Ent ent) where T : unmanaged, IAspect {

            return ent.GetAspect<T>();

        }

        /// <summary>
        /// Returns a typed aspect view over the existing entity components.
        /// </summary>
        [INLINE(256)]
        public static T GetAspect<T>(this in EntRO ent) where T : unmanaged, IAspect => ent.ent.GetAspect<T>();

        /// <summary>
        /// Returns the requested aspect and creates any components required for it.
        /// </summary>
        [INLINE(256)]
        public static T GetOrCreateAspect<T>(this in EntRO ent) where T : unmanaged, IAspect => ent.ent.GetOrCreateAspect<T>();

        /// <summary>
        /// Returns a typed aspect view over the existing entity components.
        /// </summary>
        [INLINE(256)]
        public static T GetAspect<T>(this in Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            E.IS_VALID_FOR_ASPECT<T>(in ent);
            T aspect = AspectStorage<T>.GetAspect(in ent.World);
            aspect.ent = ent;
            return aspect;

        }

        /// <summary>
        /// Returns the requested aspect and creates any components required for it.
        /// </summary>
        [INLINE(256)]
        public static T GetOrCreateAspect<T>(this in Ent ent) where T : unmanaged, IAspect {

            E.IS_ALIVE(in ent);
            ent.Set<T>();
            return GetAspect<T>(in ent);

        }

        /// <summary>
        /// Initializes aspect.
        /// </summary>
        public static ref T InitializeAspect<T>(this in World world) where T : unmanaged, IAspect {
            
            return ref AspectStorage<T>.InitAspect(in world);
            
        }
        
    }



}