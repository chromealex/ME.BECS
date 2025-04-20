namespace ME.BECS.Transforms {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static class TransformHierarchy {
        
        /// <summary>
        /// Include (true) or exclude (false) entity from any filters with hierarchy.
        /// By default all entities are included.
        /// </summary>
        /// <param name="ent">Entity</param>
        /// <param name="state">State</param>
        [INLINE(256)]
        public static void SetActiveHierarchy(this Ent ent, bool state) {
            
            ent.SetActive(state);
            if (ent.Has<ChildrenComponent>() == true) {

                var queue = new UnsafeQueue<Ent>(Constants.ALLOCATOR_TEMP);
                queue.Enqueue(ent);
                while (queue.Count > 0) {
                    var child = queue.Dequeue();
                    var nodes = child.Read<ChildrenComponent>();
                    for (uint i = 0u; i < nodes.list.Count; ++i) {
                        var node = nodes.list[i];
                        queue.Enqueue(node);
                    }
                    child.SetActive(state);
                }
                queue.Dispose();

            }
            
        }

        /// <summary>
        /// Destroy entity with all children entities
        /// </summary>
        /// <param name="entity"></param>
        [INLINE(256)]
        public static void DestroyHierarchy(this in Ent entity) {

            if (entity.Has<ParentComponent>() == true) {
                // remove from parent
                entity.SetParent(default);
            }
            
            if (entity.Has<ChildrenComponent>() == true) {

                var queue = new UnsafeQueue<Ent>(Constants.ALLOCATOR_TEMP);
                queue.Enqueue(entity);
                while (queue.Count > 0) {
                    var ent = queue.Dequeue();
                    var nodes = ent.Read<ChildrenComponent>();
                    for (uint i = 0u; i < nodes.list.Count; ++i) {
                        var node = nodes.list[i];
                        queue.Enqueue(node);
                    }
                    ent.Destroy();
                }
                queue.Dispose();

            } else {

                entity.Destroy();

            }

        }

    }
    
}