namespace ME.BECS {
    
    using static Cuts;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    /// <summary>
    /// Defines components data used by entity processing.
    /// </summary>
    public unsafe partial struct Components {

        /// <summary>
        /// Tests whether the context is enabled.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool IsEnabled<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            return Components.ReadState(state, typeId, in ent);
            
        }

        /// <summary>
        /// Enables the associated component or processing state.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Enable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetState(state, typeId, groupId, in ent, true);
            
        }

        /// <summary>
        /// Disables the associated component or processing state.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Disable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetState(state, typeId, groupId, in ent, false);
            
        }

        /// <summary>
        /// Stores the supplied value in components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Set<T>(safe_ptr<State> state, in Ent ent, in T data) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetUnknownType(state, typeId, groupId, in ent, in data);
            
        }

        /// <summary>
        /// Removes the specified entry from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Remove<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        /// <summary>
        /// Removes the specified entry from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Remove(safe_ptr<State> state, in Ent ent, uint typeId, uint groupId) {

            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        /// <summary>
        /// Reads the requested value from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        /// <summary>
        /// Reads the requested value from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        /// <summary>
        /// Reads ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return (T*)StaticTypes<T>.defaultValuePtr.ptr;
            return (T*)data;

        }

        /// <summary>
        /// Reads ptr.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return (T*)StaticTypes<T>.defaultValuePtr.ptr;
            return (T*)data;

        }

        /// <summary>
        /// Tests whether the requested entry is present.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static bool Has<T>(safe_ptr<State> state, uint entId, ushort gen, bool checkEnabled) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            return Components.HasUnknownType(state, typeId, entId, gen, checkEnabled);

        }

        /// <summary>
        /// Returns the requested entry from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref T Get<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent => ref Get<T>(state, ent);

        /// <summary>
        /// Returns the requested entry from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static ref T Get<T>(safe_ptr<State> state, Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out _, StaticTypes<T>.defaultValuePtr);
            return ref *(T*)data;

        }

        /// <summary>
        /// Returns the requested entry from components.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* Get<T>(safe_ptr<State> state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out isNew, StaticTypes<T>.defaultValuePtr);
            return (T*)data;

        }

        /// <summary>
        /// Returns or throw.
        /// </summary>
        [INLINE(256)][IgnoreProfiler]
        public static T* GetOrThrow<T>(safe_ptr<State> state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var data = Components.GetOrThrowUnknownType(state, typeId, groupId, in ent, out isNew, StaticTypes<T>.defaultValuePtr);
            return (T*)data;

        }

        /// <summary>
        /// Tests whether the context has static direct.
        /// </summary>
        [IgnoreProfiler]
        public static bool HasStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            return ent.HasStatic<T>();

        }

        /// <summary>
        /// Reads static direct.
        /// </summary>
        [IgnoreProfiler]
        public static T ReadStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return ent.ReadStatic<T>();

        }

        /// <summary>
        /// Tests whether the context is tag direct.
        /// </summary>
        [IgnoreProfiler]
        public static bool IsTagDirect<T>() where T : unmanaged, IComponentBase {

            return StaticTypesIsTag<T>.value.Data;

        }
        
        /// <summary>
        /// Tests whether the context is enabled direct.
        /// </summary>
        [IgnoreProfiler]
        public static bool IsEnabledDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.ReadState(ent.World.state, StaticTypes<T>.typeId, in ent);

        }

        /// <summary>
        /// Tests whether the context has direct.
        /// </summary>
        [IgnoreProfiler]
        public static bool HasDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: false);

        }

        /// <summary>
        /// Tests whether the context has direct enabled.
        /// </summary>
        [IgnoreProfiler]
        public static bool HasDirectEnabled<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: true);

        }

        /// <summary>
        /// Reads direct.
        /// </summary>
        [IgnoreProfiler]
        public static T ReadDirect<T>(Ent ent) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return Components.Read<T>(ent.World.state, ent.id, ent.gen);

        }

        /// <summary>
        /// Sets direct.
        /// </summary>
        [IgnoreProfiler]
        public static void SetDirect<T>(Ent ent, T data) where T : unmanaged, IComponent {

            SetDirect_INTERNAL(ent, in data);

        }

        [IgnoreProfiler]
        private static void SetDirect_INTERNAL<T>(Ent ent, in T data) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return;

            var typeId = StaticTypes<T>.typeId;
            E.IS_VALID_TYPE_ID(typeId);

            var state = ent.World.state;
            var ptr = state.ptr->components.items.GetUnsafePtr(in state.ptr->allocator, typeId);
            var storage = ptr.ptr->AsPtr<DataDenseSet>(in state.ptr->allocator);
            fixed (T* dataPtr = &data) {
                storage.ptr->Set(state, ent.worldId, ent.id, ent.gen, dataPtr, out var changed);
            }

        }

    }

}
