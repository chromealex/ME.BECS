namespace ME.BECS.Transforms {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static unsafe class EntCloneExt {

        [INLINE(256)]
        public static Ent Clone(this in Ent source, bool cloneHierarchy, in JobInfo jobInfo = default) {
            return source.Clone(source.worldId, cloneHierarchy, in jobInfo);
        }

        [INLINE(256)]
        public static Ent Clone(this in Ent source, ushort worldId, bool cloneHierarchy, in JobInfo jobInfo = default) {

            if (cloneHierarchy == false) {
                var copy = source.Clone();
                copy.Remove<ParentComponent>();
                copy.Remove<ChildrenComponent>();
                return copy;
            }
            
            var ent = Ent.New(worldId, in jobInfo);
            ent.CopyFrom(in source);
            {
                ref readonly var children = ref source.Read<ChildrenComponent>().list;
                if (children.Count > 0u) {
                    ref var childrenTarget = ref ent.Get<ChildrenComponent>().list;
                    childrenTarget = new ListAuto<Ent>(in ent, children.Count);
                    for (uint i = 0u; i < children.Count; ++i) {
                        var child = children[i];
                        var copy = child.Clone(worldId, true, in jobInfo);
                        copy.Set(new ParentComponent() {
                            value = ent,
                        });
                        childrenTarget.Add(copy);
                    }
                }
            }
            return ent;

        }

    }

}