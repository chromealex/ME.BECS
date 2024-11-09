namespace ME.BECS {

    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public unsafe partial struct Components {

        [INLINE(256)]
        public static void CopyFrom(State* sourceState, in Ent ent, State* targetState, in Ent targetEnt) {

            var srcArchId = sourceState->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        public static void CopyFrom<TIgnore0>(State* sourceState, in Ent ent, State* targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            var srcArchId = sourceState->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        public static void CopyFrom<TIgnore0, TIgnore1>(State* sourceState, in Ent ent, State* targetState, in Ent targetEnt) where TIgnore0 : unmanaged, IComponent where TIgnore1 : unmanaged, IComponent {

            var ignore0 = StaticTypes<TIgnore0>.typeId;
            var ignore1 = StaticTypes<TIgnore1>.typeId;
            var srcArchId = sourceState->archetypes.entToArchetypeIdx[sourceState, ent.id];
            var srcArch = sourceState->archetypes.list[sourceState, srcArchId];
            var e = srcArch.components.GetEnumerator(sourceState);
            while (e.MoveNext() == true) {
                var typeId = e.Current;
                if (ignore0 == typeId || ignore1 == typeId) continue;
                CopyFrom_INTERNAL(sourceState, in ent, targetState, in targetEnt, typeId);
            }
            
        }

        [INLINE(256)]
        private static void CopyFrom_INTERNAL(State* sourceState, in Ent ent, State* targetState, in Ent targetEnt, uint typeId) {
            var groupId = StaticTypes.groups.Get(typeId);
            ref var ptr = ref sourceState->components.items[in sourceState->allocator, typeId];
            ref var storage = ref ptr.As<DataDenseSet>(in sourceState->allocator);
            var data = storage.Get(sourceState, ent.id, ent.gen, true, out _);
            if (targetState->components.SetUnknownType(targetState, typeId, groupId, in targetEnt, data) == true) {
                targetState->batches.Set_INTERNAL(typeId, in targetEnt, targetState);
            }
        }

    }

}