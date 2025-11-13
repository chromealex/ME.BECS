namespace ME.BECS {
    
    using static Cuts;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using Unity.Collections.LowLevel.Unsafe;
    using IgnoreProfiler = Unity.Profiling.IgnoredByDeepProfilerAttribute;

    public unsafe partial struct Components {

        [INLINE(256)][IgnoreProfiler]
        public static bool IsEnabled<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            return Components.ReadState(state, typeId, in ent);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Enable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetState(state, typeId, groupId, in ent, true);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Disable<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetState(state, typeId, groupId, in ent, false);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Set<T>(safe_ptr<State> state, in Ent ent, in T data) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.SetUnknownType(state, typeId, groupId, in ent, in data);
            
        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Remove<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Remove(safe_ptr<State> state, in Ent ent, uint typeId, uint groupId) {

            return Components.RemoveUnknownType(state, typeId, groupId, in ent);

        }

        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static ref readonly T Read<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return ref StaticTypes<T>.defaultValue;
            return ref *(T*)data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen, out bool exists) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static T* ReadPtr<T>(safe_ptr<State> state, uint entId, ushort gen) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var data = Components.ReadUnknownType(state, typeId, entId, gen, out var exists);
            if (exists == false) return default;
            return (T*)data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static bool Has<T>(safe_ptr<State> state, uint entId, ushort gen, bool checkEnabled) where T : unmanaged, IComponentBase {

            var typeId = StaticTypes<T>.typeId;
            return Components.HasUnknownType(state, typeId, entId, gen, checkEnabled);

        }

        [INLINE(256)][IgnoreProfiler]
        public static ref T Get<T>(safe_ptr<State> state, in Ent ent) where T : unmanaged, IComponent => ref Get<T>(state, ent);

        [INLINE(256)][IgnoreProfiler]
        public static ref T Get<T>(safe_ptr<State> state, Ent ent) where T : unmanaged, IComponent {

            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out _, StaticTypes<T>.defaultValuePtr);
            return ref *(T*)data;

        }

        [INLINE(256)][IgnoreProfiler]
        public static T* Get<T>(safe_ptr<State> state, in Ent ent, out bool isNew) where T : unmanaged, IComponent {
            
            var typeId = StaticTypes<T>.typeId;
            var groupId = StaticTypes<T>.trackerIndex;
            var data = Components.GetUnknownType(state, typeId, groupId, in ent, out isNew, StaticTypes<T>.defaultValuePtr);
            return (T*)data;

        }

        [IgnoreProfiler]
        public static bool HasStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            return ent.HasStatic<T>();

        }

        [IgnoreProfiler]
        public static T ReadStaticDirect<T>(Ent ent) where T : unmanaged, IConfigComponentStatic {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return ent.ReadStatic<T>();

        }

        [IgnoreProfiler]
        public static bool IsTagDirect<T>() where T : unmanaged, IComponentBase {

            return StaticTypesIsTag<T>.value.Data;

        }
        
        [IgnoreProfiler]
        public static bool IsEnabledDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.ReadState(ent.World.state, StaticTypes<T>.typeId, in ent);

        }

        [IgnoreProfiler]
        public static bool HasDirect<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: false);

        }

        [IgnoreProfiler]
        public static bool HasDirectEnabled<T>(Ent ent) where T : unmanaged, IComponent {

            return Components.Has<T>(ent.World.state, ent.id, ent.gen, checkEnabled: true);

        }

        [IgnoreProfiler]
        public static T ReadDirect<T>(Ent ent) where T : unmanaged, IComponent {

            if (StaticTypes<T>.isTag == true) return StaticTypes<T>.defaultValue;

            return Components.Read<T>(ent.World.state, ent.id, ent.gen);

        }

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
                storage.ptr->Set(state, ent.id, ent.gen, dataPtr, out var changed);
            }

        }

    }

}