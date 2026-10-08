namespace ME.BECS {

    /// <summary>
    /// Caches  metadata for registered system types.
    /// </summary>
    public struct StaticSystemTypes {

        /// <summary>
        /// Counter storage accessed by Burst-compiled code.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSystemTypes>();
        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static ref uint counter => ref counterBurst.Data;
        
    }
    
    /// <summary>
    /// Caches ID metadata for registered system types.
    /// </summary>
    public struct StaticSystemTypesId<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSystemTypesId<T>>();

    }

    /// <summary>
    /// Caches null metadata for registered system types.
    /// </summary>
    public struct StaticSystemTypesNull<T> where T : unmanaged {

        /// <summary>
        /// Stored value used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<T> value = Unity.Burst.SharedStatic<T>.GetOrCreate<StaticSystemTypesNull<T>>();

    }

    /// <summary>
    /// Caches  metadata for registered system types.
    /// </summary>
    public struct StaticSystemTypes<T> where T : unmanaged {

        /// <summary>
        /// Null value used by <c>StaticSystemTypes</c>.
        /// </summary>
        public static ref T nullValue {
            get {
                StaticSystemTypesNull<T>.value.Data = default;
                return ref StaticSystemTypesNull<T>.value.Data;
            }
        }
        /// <summary>
        /// Type id used to locate the associated entry.
        /// </summary>
        public static ref uint typeId => ref StaticSystemTypesId<T>.value.Data;
        
        /// <summary>
        /// Checks the supplied state against the constraints required by this API.
        /// </summary>
        public static void Validate() {

            if (typeId == 0u) {
                StaticSystemTypes<T>.typeId = ++StaticSystemTypes.counter;
            }

        }

    }

    /// <summary>
    /// Provides extension operations for systems graph.
    /// </summary>
    public static unsafe class SystemsGraphExtensions {

        /// <summary>
        /// Returns system.
        /// </summary>
        public static ref T GetSystem<T>(this ref SystemGroup graph, out bool found) where T : unmanaged, ISystem {

            found = false;
            var typeId = StaticSystemTypes<T>.typeId;
            for (int i = 0; i < graph.index; ++i) {
                var node = graph.nodes[i];
                if (node.data.ptr->graph.ptr != null) {
                    ref var sys = ref (*node.data.ptr->graph.ptr).GetSystem<T>(out found);
                    if (found == true) return ref sys;
                } else if (node.data.ptr->systemData.ptr != null) {
                    if (node.data.ptr->systemTypeId == typeId) {
                        found = true;
                        return ref *(T*)node.data.ptr->systemData.ptr;
                    }
                }
            }

            return ref StaticSystemTypes<T>.nullValue;

        }
        
        private static readonly object[] addDirectParametersCache = new object[3];
        private static readonly System.Reflection.MethodInfo addDirectMethodCache = typeof(SystemsGraphExtensions).GetMethod(nameof(AddDirect));
        /// <summary>
        /// Adds the supplied entry to systems graph extensions.
        /// </summary>
        public static SystemHandle Add(this ref SystemGroup graph, ISystem system, in SystemHandle dependsOn = default) {
            var type = system.GetType();
            var gMethod = addDirectMethodCache.MakeGenericMethod(type);
            addDirectParametersCache[0] = graph;
            addDirectParametersCache[1] = system;
            addDirectParametersCache[2] = dependsOn;
            return (SystemHandle)gMethod.Invoke(null, addDirectParametersCache);
        }

        /// <summary>
        /// Adds the supplied entry to systems graph extensions.
        /// </summary>
        public static SystemHandle Add(this ref SystemGroup graph, in SystemGroup innerGraph, in SystemHandle dependsOn = default) {

            var node = Node.Create(innerGraph);
            graph.RegisterNode(node);
            if (dependsOn.IsValid() == false) {

                // No dependencies
                graph.rootNode.ptr->AddChild(node, graph.rootNode);

            } else {
                
                // Has dependencies
                var depNode = graph.GetNode(dependsOn);
                if (depNode.ptr->deps.ptr != null) {
                    // Combined dependencies
                    for (int i = 0; i < depNode.ptr->depsIndex; ++i) {
                        var dep = depNode.ptr->deps[i];
                        dep.data.ptr->AddChild(node, dep.data);
                    }
                } else {
                    // Single dependency
                    depNode.ptr->AddChild(node, depNode);
                }
                
            }
            
            Journal.AddSystem(Context.world.id, node.ptr->name);
            
            return SystemHandle.Create(node.ptr->id);

        }

        /// <summary>
        /// Adds direct.
        /// </summary>
        public static SystemHandle AddDirect<T>(SystemGroup graph, T system, SystemHandle dependsOn) where T : unmanaged, ISystem {
            return Add<T>(ref graph, system, dependsOn);
        }

        /// <summary>
        /// Adds the supplied entry to systems graph extensions.
        /// </summary>
        public static SystemHandle Add<T>(this ref SystemGroup graph, in SystemHandle dependsOn = default) where T : unmanaged, ISystem {
            return Add<T>(ref graph, default, dependsOn);
        }

        /// <summary>
        /// Adds the supplied entry to systems graph extensions.
        /// </summary>
        public static SystemHandle Add<T>(this ref SystemGroup graph, in T system, in SystemHandle dependsOn = default) where T : unmanaged, ISystem {
            
            var node = Node.CreateMethods(system);
            graph.RegisterNode(node);
            node.ptr->name = typeof(T).Name;
            
            if (dependsOn.IsValid() == false) {

                // No dependencies
                graph.rootNode.ptr->AddChild(node, graph.rootNode);

            } else {
                
                // Has dependencies
                var depNode = graph.GetNode(dependsOn);
                if (depNode.ptr->deps.ptr != null) {
                    // Combined dependencies
                    for (int i = 0; i < depNode.ptr->depsIndex; ++i) {
                        var dep = depNode.ptr->deps[i];
                        dep.data.ptr->AddChild(node, dep.data);
                    }
                } else {
                    // Single dependency
                    depNode.ptr->AddChild(node, depNode);
                }
                
            }
            
            Journal.AddSystem(Context.world.id, node.ptr->name);

            return SystemHandle.Create(node.ptr->id);

        }

    }

}