namespace ME.BECS.TransformAspect {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    /*
    public static class TransformHierarchyExt {

        [INLINE(256)]
        public static void OnEntDestroy(ref MemoryAllocator allocator, in Ent entity) {

            if (entity.Has<ParentComponent>() == true) {
                
                entity.SetParent(in Ent.Empty);
                
            }
            
            if (entity.Has<ChildrenComponent>() == true) {

                // TODO: Possible stack overflow while using Clear(true) because of OnEntDestroy call
                ref var nodes = ref entity.Get<ChildrenComponent>();
                var e = nodes.items.GetEnumerator(in allocator);
                while (e.MoveNext() == true) {
                    e.Current.Remove<ParentComponent>();
                    e.Current.Destroy();
                }
                e.Dispose();
                nodes.items.Dispose(ref allocator);

            }

        }

        [INLINE(256)]
        public static void OnEntVersionChanged(ref MemoryAllocator allocator, in Ent entity) {

            if (entity.TryRead<ChildrenComponent>(out var nodes) == true) {

                var world = Worlds.current;
                var e = nodes.items.GetEnumerator(in allocator);
                while (e.MoveNext() == true) {
                    var item = e.Current;
                    world.IncrementEntVersion(in item);
                    // TODO: Possible stack overflow while using OnEntVersionChanged call
                    ECSTransformHierarchy.OnEntVersionChanged(ref world.GetState().allocator, in entity);
                }
                e.Dispose();
                nodes.items.Dispose(ref allocator);
                
            }

        }

        [INLINE(256)]
        public static uint GetVersionInHierarchy(this in Ent entity) {

            var v = entity.GetVersion();
            var ent = entity;
            while (ent.TryRead<ParentComponent>(out var container) == true) {

                ent = container.entity;
                v += ent.GetVersion();
                
            }
            return v;

        }

        [INLINE(256)]
        public static void SetParent(this in TransformAspect child, in Ent root) {

            child.SetParent(in root, worldPositionStays: true);

        }

        [INLINE(256)]
        public static void SetParent(this in TransformAspect child, in Ent root, bool worldPositionStays) {

            if (worldPositionStays == true) {

                var pos = child.Position;
                var rot = child.Rotation;
                TransformHierarchyExt.SetParent_INTERNAL(in child, in root);
                child.Position = pos;
                child.Rotation = rot;

            } else {

                TransformHierarchyExt.SetParent_INTERNAL(in child, in root);

            }

        }

        [INLINE(256)]
        public static void SetParent2D(this in Ent child, in Ent root) {

            child.SetParent2D(in root, worldPositionStays: true);

        }

        [INLINE(256)]
        public static void SetParent2D(this in Ent child, in Ent root, bool worldPositionStays) {

            if (worldPositionStays == true) {

                var pos = child.GetPosition2D();
                var rot = child.GetRotation2D();
                TransformHierarchyExt.SetParent_INTERNAL(in child, in root);
                child.SetPosition2D(pos);
                child.SetRotation2D(rot);

            } else {

                TransformHierarchyExt.SetParent_INTERNAL(in child, in root);

            }

        }
        
        [INLINE(256)]
        private static void SetParent_INTERNAL(in Ent child, in Ent root) {

            if (child == root) return;

            if (root == Ent.Empty) {

                var childContainer = child.Read<ParentComponent>();
                if (childContainer.entity.IsAlive() == false) return;
                
                ref var nodes = ref childContainer.entity.Get<ChildrenComponent>();
                child.Remove<ParentComponent>();
                nodes.items.Remove(ref Worlds.current.GetState().allocator, child);
                return;

            }

            ref var container = ref child.Get<ParentComponent>();
            if (container.entity == root || root.IsAlive() == false) {

                return;

            }
            
            if (ECSTransformHierarchy.FindInHierarchy(in child, in root) == true) return;

            if (container.entity.IsAlive() == true) {

                child.SetParent(Ent.Empty);

            }

            container.entity = root;
            ref var rootNodes = ref root.Get<ChildrenComponent>();
            if (rootNodes.items.isCreated == false) rootNodes.items = new List<Ent>(ref Worlds.current.GetState().allocator, 1);
            rootNodes.items.Add(ref Worlds.current.GetState().allocator, child);

        }

        [INLINE(256)]
        public static Ent GetRoot(this in Ent child) {

            Ent root;
            var container = child;
            do {

                root = container;
                container = container.Read<ParentComponent>().entity;

            } while (container.IsAlive() == true);

            return root;

        }

        [INLINE(256)]
        private static bool FindInHierarchy(in Ent child, in Ent root) {

            var childNodes = child.Read<ChildrenComponent>();
            if (childNodes.items.isCreated == false) return false;
            
            if (childNodes.items.Contains(in Worlds.current.GetState().allocator, root) == true) {

                return true;

            }

            var e = childNodes.items.GetEnumerator(in Worlds.current.GetState().allocator);
            while (e.MoveNext() == true) {
                if (ECSTransformHierarchy.FindInHierarchy(e.Current, in root) == true) return true;
            }
            e.Dispose();

            return false;

        }

        [INLINE(256)]
        public static bool TryGetParent(this in Ent child, out Ent parent) {

            var r = child.TryRead<ParentComponent>(out var c);
            parent = c.entity;
            return r;

        }

        [INLINE(256)]
        public static bool HasParent(this in Ent child) {

            return child.Has<ParentComponent>();

        }

        [INLINE(256)]
        public static Ent GetParent(this in Ent child) {

            return child.Read<ParentComponent>().entity;

        }

        [INLINE(256)]
        public static bool TryReadParent(this in Ent child, out Ent parent) {

            return child.TryGetParent(out parent);

        }

        [INLINE(256)]
        public static Ent ReadParent(this in Ent child) {

            return child.GetParent();

        }

    }*/
    
}