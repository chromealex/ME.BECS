namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    /// <summary>
    /// Defines the graph structure used for t system graph.
    /// </summary>
    public class TSystemGraph<T> where T : unmanaged, ISystem {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<TSystemGraph<T>>();

    }

    /// <summary>
    /// Defines the graph structure used for t system graph.
    /// </summary>
    public unsafe class TSystemGraph {
        
        /// <summary>
        /// Registers the supplied instance or type for subsequent lookup.
        /// </summary>
        public static void Register<T>(int graphId, void* ptr) where T : unmanaged, ISystem {
            ref var dic = ref TSystemGraph<T>.dic.Data;
            if (dic.IsCreated == false) dic = new UnsafeHashMap<int, System.IntPtr>(4, Constants.ALLOCATOR_DOMAIN);
            if (dic.TryGetValue(graphId, out var sysPtr) == false) {
                dic.Add(graphId, (System.IntPtr)ptr);
            } else {
                dic[graphId] = (System.IntPtr)ptr;
            }
        }

        /// <summary>
        /// Returns system.
        /// </summary>
        public static bool GetSystem<T>(int graphId, out T* system) where T : unmanaged, ISystem {
            system = null;
            if (TSystemGraph<T>.dic.Data.TryGetValue(graphId, out var ptr) == true) {
                system = (T*)ptr;
                return true;
            }
            return false;
        }

    }

    /// <summary>
    /// Stores the initialization registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticInitialization {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticInitialization>();

    }

    /// <summary>
    /// Stores the on awake registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticOnAwake {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnAwake>();

    }

    /// <summary>
    /// Stores the on start registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticOnStart {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnStart>();

    }

    /// <summary>
    /// Stores the on update registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticOnUpdate {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnUpdate>();

    }

    /// <summary>
    /// Stores the on draw gizmos registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticOnDrawGizmos {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnDrawGizmos>();

    }

    /// <summary>
    /// Stores the on destroy registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticOnDestroy {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnDestroy>();

    }

    /// <summary>
    /// Coordinates systems static get during the ECS system lifecycle.
    /// </summary>
    public class SystemsStaticGetSystem {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticGetSystem>();

    }

    /// <summary>
    /// Stores the pins registrations used by system lifecycle dispatch.
    /// </summary>
    public class SystemsStaticPins {

        /// <summary>
        /// Lookup table used by this instance.
        /// </summary>
        public static readonly Unity.Burst.SharedStatic<UnsafeList<System.Runtime.InteropServices.GCHandle>> dic = Unity.Burst.SharedStatic<UnsafeList<System.Runtime.InteropServices.GCHandle>>.GetOrCreate<SystemsStaticPins>();

    }

    /// <summary>
    /// Stores the  registrations used by system lifecycle dispatch.
    /// </summary>
    public static unsafe class SystemsStatic {

        /// <summary>
        /// Defines the callback signature for initialize graph.
        /// </summary>
        public delegate void InitializeGraph();
        /// <summary>
        /// Defines the callback signature for on awake.
        /// </summary>
        public delegate void OnAwake(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Defines the callback signature for on start.
        /// </summary>
        public delegate void OnStart(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Defines the callback signature for on update.
        /// </summary>
        public delegate void OnUpdate(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Defines the callback signature for on destroy.
        /// </summary>
        public delegate void OnDestroy(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Defines the callback signature for on draw gizmos.
        /// </summary>
        public delegate void OnDrawGizmos(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        /// <summary>
        /// Defines the callback signature for get system.
        /// </summary>
        public delegate void GetSystem(int index, out void* ptr);

        private static void Register<T>(ref UnsafeHashMap<int, System.IntPtr> registry, T callback, int graphId, bool isBurst) where T : class {

            System.IntPtr ptr;
            var pinnedHandle = System.Runtime.InteropServices.GCHandle.Alloc(callback);
            if (isBurst == true) {
                var pointer = Unity.Burst.BurstCompiler.CompileFunctionPointer(callback);
                ptr = pointer.Value;
            } else {
                var noBurstFunction = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(callback);
                ptr = (System.IntPtr)noBurstFunction.ToPointer();
            }
            
            if (registry.IsCreated == false) {
                registry = new UnsafeHashMap<int, System.IntPtr>(10, Constants.ALLOCATOR_DOMAIN);
            }

            if (SystemsStaticPins.dic.Data.IsCreated == false) {
                SystemsStaticPins.dic.Data = new UnsafeList<System.Runtime.InteropServices.GCHandle>(1, Constants.ALLOCATOR_DOMAIN);
            }
            
            SystemsStaticPins.dic.Data.Add(pinnedHandle);
            registry.Add(graphId, ptr);

        }

        /// <summary>
        /// Initializes systems static state from the supplied context.
        /// </summary>
        public static void Initialize() {

            Dispose();

        }
        
        /// <summary>
        /// Releases the resources owned by this systems static instance.
        /// </summary>
        public static void Dispose() {

            if (SystemsStaticPins.dic.Data.IsCreated == true) {
                foreach (var pin in SystemsStaticPins.dic.Data) {
                    pin.Free();
                }
                SystemsStaticPins.dic.Data.Dispose();
            }

            SystemsStaticInitialization.dic.Data.Dispose();
            SystemsStaticGetSystem.dic.Data.Dispose();
            SystemsStaticOnAwake.dic.Data.Dispose();
            SystemsStaticOnStart.dic.Data.Dispose();
            SystemsStaticOnUpdate.dic.Data.Dispose();
            SystemsStaticOnDrawGizmos.dic.Data.Dispose();
            SystemsStaticOnDestroy.dic.Data.Dispose();

        }
        
        /// <summary>
        /// Registers method.
        /// </summary>
        public static void RegisterMethod(InitializeGraph callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticInitialization.dic.Data, callback, graphId, isBurst);
            
        }

        /// <summary>
        /// Registers get system method.
        /// </summary>
        public static void RegisterGetSystemMethod(GetSystem callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticGetSystem.dic.Data, callback, graphId, isBurst);
            
        }

        /// <summary>
        /// Registers awake method.
        /// </summary>
        public static void RegisterAwakeMethod(OnAwake callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnAwake.dic.Data, callback, graphId, isBurst);

        }

        /// <summary>
        /// Registers start method.
        /// </summary>
        public static void RegisterStartMethod(OnStart callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnStart.dic.Data, callback, graphId, isBurst);

        }

        /// <summary>
        /// Registers update method.
        /// </summary>
        public static void RegisterUpdateMethod(OnUpdate callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticOnUpdate.dic.Data, callback, graphId, isBurst);

        }

        /// <summary>
        /// Registers draw gizmos method.
        /// </summary>
        public static void RegisterDrawGizmosMethod(OnDrawGizmos callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnDrawGizmos.dic.Data, callback, graphId, isBurst);

        }

        /// <summary>
        /// Registers destroy method.
        /// </summary>
        public static void RegisterDestroyMethod(OnDestroy callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticOnDestroy.dic.Data, callback, graphId, isBurst);

        }

        /// <summary>
        /// Raises initialize.
        /// </summary>
        public static bool RaiseInitialize(int graphId, ref SystemGroup group) {

            group.graphId = graphId;
            if (SystemsStaticInitialization.dic.Data.TryGetValue(graphId, out var ptr) == true) {

                var func = new Unity.Burst.FunctionPointer<InitializeGraph>(ptr);
                func.Invoke();
                return true;

            }

            return false;

        }

        /// <summary>
        /// Raises on awake.
        /// </summary>
        public static bool RaiseOnAwake(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {
                            
                            if (SystemsStaticOnAwake.dic.Data.TryGetValue(child.data.ptr->graph.ptr->graphId, out var ptr) == true) {

                                var func = new Unity.Burst.FunctionPointer<OnAwake>(ptr);
                                func.Invoke(deltaTimeMs, ref world, ref dependsOn);
                                result = true;

                            }
                            
                        }
                    }
                }
            }
            
            return result;

        }

        /// <summary>
        /// Raises on start.
        /// </summary>
        public static bool RaiseOnStart(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {
                            
                            if (SystemsStaticOnStart.dic.Data.TryGetValue(child.data.ptr->graph.ptr->graphId, out var ptr) == true) {

                                var func = new Unity.Burst.FunctionPointer<OnStart>(ptr);
                                func.Invoke(deltaTimeMs, ref world, ref dependsOn);
                                result = true;

                            }
                            
                        }
                    }
                }
            }
            
            return result;

        }

        /// <summary>
        /// Raises on update.
        /// </summary>
        public static bool RaiseOnUpdate(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                //UnityEngine.Debug.Log("RaiseOnUpdate Call: " + rootGroup.rootNode.ptr->childrenIndex + ", updateType: " + updateType);
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {

                            //UnityEngine.Debug.Log("RaiseOnUpdate Call: " + SystemsStaticOnUpdate.dic.Data.Count + ", child.data.ptr->graph.ptr->graphId: " + child.data.ptr->graph.ptr->graphId + ", updateType: " + updateType);
                            if (SystemsStaticOnUpdate.dic.Data.TryGetValue(child.data.ptr->graph.ptr->graphId, out var ptr) == true) {

                                //UnityEngine.Debug.Log("static systems call RaiseOnUpdate: " + child.data.ptr->graph.ptr->graphId + ", updateType: " + updateType);
                                var func = new Unity.Burst.FunctionPointer<OnUpdate>(ptr);
                                func.Invoke(deltaTimeMs, ref world, ref dependsOn);
                                result = true;

                            }

                        }
                    }
                }
            }
            
            return result;

        }

        /// <summary>
        /// Raises on draw gizmos.
        /// </summary>
        public static bool RaiseOnDrawGizmos(in SystemGroup rootGroup, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                var tempSet = new UnsafeHashSet<int>((int)rootGroup.rootNode.ptr->childrenIndex, Constants.ALLOCATOR_TEMP);
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        
                        if (tempSet.Add(child.data.ptr->graph.ptr->graphId) == false) continue;
                        if (SystemsStaticOnDrawGizmos.dic.Data.TryGetValue(child.data.ptr->graph.ptr->graphId, out var ptr) == true) {

                            var func = new Unity.Burst.FunctionPointer<OnDrawGizmos>(ptr);
                            func.Invoke(0u, ref world, ref dependsOn);
                            result = true;

                        }

                    }
                }
            }
            
            return result;

        }

        /// <summary>
        /// Raises on destroy.
        /// </summary>
        public static bool RaiseOnDestroy(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                var tempSet = new UnsafeHashSet<int>((int)rootGroup.rootNode.ptr->childrenIndex, Constants.ALLOCATOR_TEMP);
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {

                            if (tempSet.Add(child.data.ptr->graph.ptr->graphId) == false) continue;
                            if (SystemsStaticOnDestroy.dic.Data.TryGetValue(child.data.ptr->graph.ptr->graphId, out var ptr) == true) {

                                var func = new Unity.Burst.FunctionPointer<OnDestroy>(ptr);
                                func.Invoke(deltaTimeMs, ref world, ref dependsOn);
                                result = true;

                            }

                        }
                    }
                }
            }
            
            return result;

        }

        /// <summary>
        /// Attempts to get system and reports whether the operation succeeded.
        /// </summary>
        public static bool TryGetSystem<T>(in SystemGroup rootGroup, out T* system) where T : unmanaged, ISystem {

            system = null;
            if (rootGroup.rootNode.ptr != null) {
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (TSystemGraph.GetSystem(child.data.ptr->graph.ptr->graphId, out system) == true) {
                            return true;
                        }
                    }
                }
            }
            return false;
            
        }

    }

}