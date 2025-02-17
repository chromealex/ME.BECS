namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Components {

        [INLINE(256)]
        public static void CopyFrom(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) {

            var srcArchId = sourceState.ptr->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState.ptr->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        public static void CopyFrom<TIgnore0>(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            var srcArchId = sourceState.ptr->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState.ptr->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        public static void CopyFrom<TIgnore0, TIgnore1>(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            var ignore1 = StaticTypes<TIgnore1>.typeId;
            var srcArchId = sourceState.ptr->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState.ptr->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId || ignore1 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        private static void CopyFrom_INTERNAL(safe_ptr<State> sourceState, in Ent ent, safe_ptr<State> targetState, in Ent targetEnt, uint typeId) {
            var groupId = StaticTypes.groups.Get(typeId);
            ref var ptr = ref sourceState.ptr->components.items[in sourceState.ptr->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in sourceState.ptr->allocator);
            var data = storage.Get(sourceState, ent.id, ent.gen, true, out _, default);
            if (Components.SetUnknownType(targetState, typeId, groupId, in targetEnt, data) == true) {
                Batches.Set_INTERNAL(typeId, in targetEnt, targetState);
            }
        }

    }

}