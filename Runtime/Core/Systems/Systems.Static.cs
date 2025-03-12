namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    public class TSystemGraph<T> where T : unmanaged, ISystem {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<TSystemGraph<T>>();

    }

    public unsafe class TSystemGraph {
        
        public static void Register<T>(int graphId, void* ptr) where T : unmanaged, ISystem {
            ref var dic = ref TSystemGraph<T>.dic.Data;
            if (dic.IsCreated == false) dic = new UnsafeHashMap<int, System.IntPtr>(4, Constants.ALLOCATOR_DOMAIN);
            if (dic.TryGetValue(graphId, out var sysPtr) == false) {
                dic.Add(graphId, (System.IntPtr)ptr);
            } else {
                dic[graphId] = (System.IntPtr)ptr;
            }
        }

        public static bool GetSystem<T>(int graphId, out T* system) where T : unmanaged, ISystem {
            system = null;
            if (TSystemGraph<T>.dic.Data.TryGetValue(graphId, out var ptr) == true) {
                system = (T*)ptr;
                return true;
            }
            return false;
        }

    }

    public class SystemsStaticInitialization {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticInitialization>();

    }

    public class SystemsStaticOnAwake {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnAwake>();

    }

    public class SystemsStaticOnStart {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnStart>();

    }

    public class SystemsStaticOnUpdate {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnUpdate>();

    }

    public class SystemsStaticOnDrawGizmos {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnDrawGizmos>();

    }

    public class SystemsStaticOnDestroy {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnDestroy>();

    }

    public class SystemsStaticGetSystem {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticGetSystem>();

    }

    public class SystemsStaticPins {

        public static readonly Unity.Burst.SharedStatic<UnsafeList<System.Runtime.InteropServices.GCHandle>> dic = Unity.Burst.SharedStatic<UnsafeList<System.Runtime.InteropServices.GCHandle>>.GetOrCreate<SystemsStaticPins>();

    }

    public static unsafe class SystemsStatic {

        public delegate void InitializeGraph();
        public delegate void OnAwake(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnStart(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnUpdate(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnDestroy(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnDrawGizmos(uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn);
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

        public static void Initialize() {

            Dispose();

        }
        
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
        
        public static void RegisterMethod(InitializeGraph callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticInitialization.dic.Data, callback, graphId, isBurst);
            
        }

        public static void RegisterGetSystemMethod(GetSystem callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticGetSystem.dic.Data, callback, graphId, isBurst);
            
        }

        public static void RegisterAwakeMethod(OnAwake callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnAwake.dic.Data, callback, graphId, isBurst);

        }

        public static void RegisterStartMethod(OnStart callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnStart.dic.Data, callback, graphId, isBurst);

        }

        public static void RegisterUpdateMethod(OnUpdate callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticOnUpdate.dic.Data, callback, graphId, isBurst);

        }

        public static void RegisterDrawGizmosMethod(OnDrawGizmos callback, int graphId, bool isBurst) {
            
            Register(ref SystemsStaticOnDrawGizmos.dic.Data, callback, graphId, isBurst);

        }

        public static void RegisterDestroyMethod(OnDestroy callback, int graphId, bool isBurst) {

            Register(ref SystemsStaticOnDestroy.dic.Data, callback, graphId, isBurst);

        }

        public static bool RaiseInitialize(int graphId, ref SystemGroup group) {

            group.graphId = graphId;
            if (SystemsStaticInitialization.dic.Data.TryGetValue(graphId, out var ptr) == true) {

                var func = new Unity.Burst.FunctionPointer<InitializeGraph>(ptr);
                func.Invoke();
                return true;

            }

            return false;

        }

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

        public static bool RaiseOnUpdate(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                //UnityEngine.Debug.Log("RaiseOnUpdate Call: " + rootGroup.rootNode.ptr->childrenIndex + ", updateType: " + updateType);
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {

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

        public static bool RaiseOnDrawGizmos(in SystemGroup rootGroup, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        
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

        public static bool RaiseOnDestroy(in SystemGroup rootGroup, ushort updateType, uint deltaTimeMs, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode.ptr != null) {
                for (uint i = 0u; i < rootGroup.rootNode.ptr->childrenIndex; ++i) {
                    var child = rootGroup.rootNode.ptr->children[i];
                    if (child.data.ptr->graph.ptr != null) {
                        if (updateType == 0 || child.data.ptr->graph.ptr->updateType == updateType) {

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