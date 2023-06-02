namespace ME.BECS {

    public struct StaticSystemTypes {

        public static readonly Unity.Burst.SharedStatic<uint> counterBurst = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSystemTypes>();
        public static ref uint counter => ref counterBurst.Data;
        
    }
    
    public struct StaticSystemTypesId<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<uint> value = Unity.Burst.SharedStatic<uint>.GetOrCreate<StaticSystemTypesId<T>>();

    }

    public struct StaticSystemTypesNull<T> where T : unmanaged {

        public static readonly Unity.Burst.SharedStatic<T> value = Unity.Burst.SharedStatic<T>.GetOrCreate<StaticSystemTypesNull<T>>();

    }

    public struct StaticSystemTypes<T> where T : unmanaged {

        public static ref T nullValue {
            get {
                StaticSystemTypesNull<T>.value.Data = default;
                return ref StaticSystemTypesNull<T>.value.Data;
            }
        }
        public static ref uint typeId => ref StaticSystemTypesId<T>.value.Data;
        
        public static void Validate() {

            if (typeId == 0u) {
                StaticSystemTypes<T>.typeId = ++StaticSystemTypes.counter;
            }

        }

    }

    public static unsafe class SystemsGraphExtensions {

        public static ref T GetSystem<T>(this ref SystemGroup graph, out bool found) where T : unmanaged, ISystem {

            found = false;
            var typeId = StaticSystemTypes<T>.typeId;
            for (int i = 0; i < graph.index; ++i) {
                var node = graph.nodes[i];
                if (node.data->graph != null) {
                    return ref (*node.data->graph).GetSystem<T>(out found);
                } else if (node.data->systemData != null) {
                    if (node.data->systemTypeId == typeId) {
                        found = true;
                        return ref *(T*)node.data->systemData;
                    }
                }
            }

            return ref StaticSystemTypes<T>.nullValue;

        }
        
        private static object[] addDirectParametersCache = new object[3];
        private static System.Reflection.MethodInfo addDirectMethodCache = typeof(SystemsGraphExtensions).GetMethod(nameof(AddDirect));
        public static SystemHandle Add(this ref SystemGroup graph, ISystem system, in SystemHandle dependsOn = default) {
            var type = system.GetType();
            var gMethod = addDirectMethodCache.MakeGenericMethod(type);
            addDirectParametersCache[0] = graph;
            addDirectParametersCache[1] = system;
            addDirectParametersCache[2] = dependsOn;
            return (SystemHandle)gMethod.Invoke(null, addDirectParametersCache);
        }

        public static SystemHandle Add(this ref SystemGroup graph, in SystemGroup innerGraph, in SystemHandle dependsOn = default) {

            var node = Node.Create(innerGraph);
            graph.RegisterNode(node);
            if (dependsOn.IsValid() == false) {

                // No dependencies
                graph.rootNode->AddChild(node, graph.rootNode);

            } else {
                
                // Has dependencies
                var depNode = graph.GetNode(dependsOn);
                if (depNode->deps != null) {
                    // Combined dependencies
                    for (int i = 0; i < depNode->depsIndex; ++i) {
                        var dep = depNode->deps[i];
                        dep.data->AddChild(node, dep.data);
                    }
                } else {
                    // Single dependency
                    depNode->AddChild(node, depNode);
                }
                
            }
            
            return SystemHandle.Create(node->id);

        }

        public static SystemHandle AddDirect<T>(SystemGroup graph, T system, SystemHandle dependsOn) where T : unmanaged, ISystem {
            return Add<T>(ref graph, system, dependsOn);
        }

        public static SystemHandle Add<T>(this ref SystemGroup graph, in SystemHandle dependsOn = default) where T : unmanaged, ISystem {
            return Add<T>(ref graph, default, dependsOn);
        }

        public static SystemHandle Add<T>(this ref SystemGroup graph, in T system, in SystemHandle dependsOn = default) where T : unmanaged, ISystem {
            
            var node = Node.CreateMethods(system);
            graph.RegisterNode(node);
            node->name = typeof(T).Name;
            
            if (dependsOn.IsValid() == false) {

                // No dependencies
                graph.rootNode->AddChild(node, graph.rootNode);

            } else {
                
                // Has dependencies
                var depNode = graph.GetNode(dependsOn);
                if (depNode->deps != null) {
                    // Combined dependencies
                    for (int i = 0; i < depNode->depsIndex; ++i) {
                        var dep = depNode->deps[i];
                        dep.data->AddChild(node, dep.data);
                    }
                } else {
                    // Single dependency
                    depNode->AddChild(node, depNode);
                }
                
            }
            
            return SystemHandle.Create(node->id);

        }

    }

}