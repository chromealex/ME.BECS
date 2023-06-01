namespace ME.BECS.TransformAspect {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe class EntCloneExt {

        [INLINE(256)]
        public static Ent Clone(this in Ent source, bool cloneHierarchy) {
            return source.Clone(source.worldId, cloneHierarchy);
        }

        [INLINE(256)]
        public static Ent Clone(this in Ent source, ushort worldId, bool cloneHierarchy) {

            if (cloneHierarchy == false) {
                return source.Clone();
            }
            
            var ent = Ent.New(worldId);
            ent.CopyFrom(in source);
            {
                var state = source.World.state;
                ref readonly var children = ref source.Read<ChildrenComponent>().list;
                if (children.Count > 0u) {
                    ref var childrenTarget = ref ent.Get<ChildrenComponent>().list;
                    childrenTarget = new List<Ent>(ref ent.World.state->allocator, children.Count);
                    for (uint i = 0u; i < children.Count; ++i) {
                        var child = children[state, i];
                        childrenTarget.Add(ref state->allocator, child.Clone(worldId, true));
                    }
                }
            }
            return ent;

        }
        
        [INLINE(256)]
        public static void CopyFrom(this in Ent target, in Ent source) {

            var sourceState = source.World.state;
            var targetState = target.World.state;
            Batches.Apply(sourceState, source.worldId);
            sourceState->components.CopyFrom(sourceState, source.id, targetState, target.id, target.gen);

        }

    }

}