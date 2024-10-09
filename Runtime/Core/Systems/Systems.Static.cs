namespace ME.BECS {
    
    using Unity.Collections.LowLevel.Unsafe;

    public class TSystem<T> where T : unmanaged, ISystem {

        public static readonly Unity.Burst.SharedStatic<int> index = Unity.Burst.SharedStatic<int>.GetOrCreate<TSystem<T>>();

    }

    public class TSystemGraph<T> where T : unmanaged, ISystem {

        public static readonly Unity.Burst.SharedStatic<int> index = Unity.Burst.SharedStatic<int>.GetOrCreate<TSystemGraph<T>>();

    }

    public class SystemsStaticInitialization {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticInitialization>();

    }

    public class SystemsStaticOnAwake {

        public static readonly Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>> dic = Unity.Burst.SharedStatic<UnsafeHashMap<int, System.IntPtr>>.GetOrCreate<SystemsStaticOnAwake>();

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

    public static unsafe class SystemsStatic {

        public delegate void InitializeGraph();
        public delegate void OnAwake(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnUpdate(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnDestroy(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void OnDrawGizmos(float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn);
        public delegate void GetSystem(int index, out void* ptr);

        private static void Register<T>(ref UnsafeHashMap<int, System.IntPtr> registry, T callback, int graphId, bool isBurst) where T : class {

            System.IntPtr ptr;
            if (isBurst == true) {
                var pointer = Unity.Burst.BurstCompiler.CompileFunctionPointer(callback);
                ptr = pointer.Value;
            } else {
                var noBurstFunction = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(callback);
                ptr = (System.IntPtr)noBurstFunction.ToPointer();
            }
            
            if (registry.IsCreated == false) {
                registry = new UnsafeHashMap<int, System.IntPtr>(10, Constants.ALLOCATOR_PERSISTENT_ST);
            }
            
            registry.Add(graphId, ptr);

        }

        public static void Initialize() {

            Dispose();

        }
        
        public static void Dispose() {

            SystemsStaticInitialization.dic.Data.Dispose();
            SystemsStaticGetSystem.dic.Data.Dispose();
            SystemsStaticOnAwake.dic.Data.Dispose();
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

        public static bool RaiseOnAwake(in SystemGroup rootGroup, ushort updateType, float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode != null) {
                for (uint i = 0u; i < rootGroup.rootNode->childrenIndex; ++i) {
                    var child = rootGroup.rootNode->children[i];
                    if (child.data->graph != null) {
                        if (updateType == 0 || child.data->graph->updateType == updateType) {
                            
                            if (SystemsStaticOnAwake.dic.Data.TryGetValue(child.data->graph->graphId, out var ptr) == true) {

                                var func = new Unity.Burst.FunctionPointer<OnAwake>(ptr);
                                func.Invoke(dt, ref world, ref dependsOn);
                                result = true;

                            }
                            
                        }
                    }
                }
            }
            
            return result;

        }

        public static bool RaiseOnUpdate(in SystemGroup rootGroup, ushort updateType, float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode != null) {
                //UnityEngine.Debug.Log("RaiseOnUpdate Call: " + rootGroup.rootNode->childrenIndex + ", updateType: " + updateType);
                for (uint i = 0u; i < rootGroup.rootNode->childrenIndex; ++i) {
                    var child = rootGroup.rootNode->children[i];
                    if (child.data->graph != null) {
                        if (updateType == 0 || child.data->graph->updateType == updateType) {

                            //UnityEngine.Debug.Log("RaiseOnUpdate Call: " + SystemsStaticOnUpdate.dic.Data.Count + ", child.data->graph->graphId: " + child.data->graph->graphId + ", updateType: " + updateType);
                            if (SystemsStaticOnUpdate.dic.Data.TryGetValue(child.data->graph->graphId, out var ptr) == true) {

                                //UnityEngine.Debug.Log("static systems call RaiseOnUpdate: " + child.data->graph->graphId + ", updateType: " + updateType);
                                var func = new Unity.Burst.FunctionPointer<OnUpdate>(ptr);
                                func.Invoke(dt, ref world, ref dependsOn);
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
            if (rootGroup.rootNode != null) {
                for (uint i = 0u; i < rootGroup.rootNode->childrenIndex; ++i) {
                    var child = rootGroup.rootNode->children[i];
                    if (child.data->graph != null) {
                        
                        if (SystemsStaticOnDrawGizmos.dic.Data.TryGetValue(child.data->graph->graphId, out var ptr) == true) {

                            var func = new Unity.Burst.FunctionPointer<OnDrawGizmos>(ptr);
                            func.Invoke(0f, ref world, ref dependsOn);
                            result = true;

                        }

                    }
                }
            }
            
            return result;

        }

        public static bool RaiseOnDestroy(in SystemGroup rootGroup, ushort updateType, float dt, ref World world, ref Unity.Jobs.JobHandle dependsOn) {

            var result = false;
            if (rootGroup.rootNode != null) {
                for (uint i = 0u; i < rootGroup.rootNode->childrenIndex; ++i) {
                    var child = rootGroup.rootNode->children[i];
                    if (child.data->graph != null) {
                        if (updateType == 0 || child.data->graph->updateType == updateType) {

                            if (SystemsStaticOnDestroy.dic.Data.TryGetValue(child.data->graph->graphId, out var ptr) == true) {

                                var func = new Unity.Burst.FunctionPointer<OnDestroy>(ptr);
                                func.Invoke(dt, ref world, ref dependsOn);
                                result = true;

                            }

                        }
                    }
                }
            }
            
            return result;

        }

        public static bool TryGetSystem<T>(out T* system) where T : unmanaged, ISystem {

            system = null;
            if (SystemsStaticGetSystem.dic.Data.TryGetValue(TSystemGraph<T>.index.Data, out var ptr) == true) {

                var func = new Unity.Burst.FunctionPointer<GetSystem>(ptr);
                func.Invoke(TSystem<T>.index.Data, out var sysPtr);
                system = (T*)sysPtr;
                return true;

            }

            return false;

        }

    }

}