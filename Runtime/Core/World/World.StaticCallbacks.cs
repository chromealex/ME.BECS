using scg = System.Collections.Generic;

namespace ME.BECS {
    
    using ME.BECS.Internal;
    using Unity.Burst;

    /// <summary>
    /// Identifies the callback storage used by world lifecycle phases.
    /// </summary>
    public static class WorldStaticCallbacksTypes {

        /// <summary>
        /// Counter tracking the associated quantity.
        /// </summary>
        public static uint counter;

    }
    
    /// <summary>
    /// Identifies the callback storage used by world lifecycle phases.
    /// </summary>
    public static class WorldStaticCallbacksTypes<T> where T : unmanaged {

        /// <summary>
        /// Identifier used to address this entry within its containing registry.
        /// </summary>
        public static uint id;
        /// <summary>
        /// Callbacks registered for the associated lifecycle or event.
        /// </summary>
        public static readonly scg::Dictionary<uint, WorldStaticCallbacks.CallbackDelegate<T>> callbacks = new scg::Dictionary<uint, WorldStaticCallbacks.CallbackDelegate<T>>();

    }

    /// <summary>
    /// Defines world static config component callbacks types data used by entity processing.
    /// </summary>
    public class WorldStaticConfigComponentCallbacksTypes {

        /// <summary>
        /// Callbacks registered for the associated lifecycle or event.
        /// </summary>
        public static readonly SharedStatic<Array<FunctionPointer<UnsafeEntityConfig.MethodCallerDelegate>>> callbacks = SharedStatic<Array<FunctionPointer<UnsafeEntityConfig.MethodCallerDelegate>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldStaticConfigComponentCallbacksTypes>(TAlign<Array<FunctionPointer<UnsafeEntityConfig.MethodCallerDelegate>>>.align, 20001);

    }

    /// <summary>
    /// Defines world static copy from component callbacks types data used by entity processing.
    /// </summary>
    public class WorldStaticCopyFromComponentCallbacksTypes {

        /// <summary>
        /// Callbacks registered for the associated lifecycle or event.
        /// </summary>
        public static readonly SharedStatic<Array<FunctionPointer<WorldStaticCallbacks.CopyFromComponentCallbackDelegate>>> callbacks = SharedStatic<Array<FunctionPointer<WorldStaticCallbacks.CopyFromComponentCallbackDelegate>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldStaticCopyFromComponentCallbacksTypes>(TAlign<Array<FunctionPointer<WorldStaticCallbacks.CopyFromComponentCallbackDelegate>>>.align, 20002);

    }

    /// <summary>
    /// Defines world static config component mask callbacks types data used by entity processing.
    /// </summary>
    public class WorldStaticConfigComponentMaskCallbacksTypes {

        /// <summary>
        /// Callbacks registered for the associated lifecycle or event.
        /// </summary>
        public static readonly SharedStatic<Array<FunctionPointer<UnsafeEntityConfig.MethodMaskCallerDelegate>>> callbacks = SharedStatic<Array<FunctionPointer<UnsafeEntityConfig.MethodMaskCallerDelegate>>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldStaticConfigComponentMaskCallbacksTypes>(TAlign<Array<FunctionPointer<UnsafeEntityConfig.MethodMaskCallerDelegate>>>.align, 20003);

    }

    /// <summary>
    /// Registers and invokes static callbacks for world lifecycle phases.
    /// </summary>
    public static class WorldStaticCallbacks {

        private static scg::HashSet<System.Collections.IDictionary> allDics = new scg::HashSet<System.Collections.IDictionary>();
        
        /// <summary>
        /// Initializes world static callbacks state from the supplied context.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Reset);
            
        }
        
        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        public static void Reset() {

            foreach (var dic in allDics) {
                dic.Clear();
            }
            
        }

        /// <summary>
        /// Defines the callback signature for callback delegate.
        /// </summary>
        public delegate void CallbackDelegate<T>(ref T data) where T : unmanaged;
        /// <summary>
        /// Defines the callback signature for copy from component callback delegate.
        /// </summary>
        public unsafe delegate void CopyFromComponentCallbackDelegate(void* componentPtr, in Ent ent);

        /// <summary>
        /// Registers copy from component callback.
        /// </summary>
        public static void RegisterCopyFromComponentCallback<T>(CopyFromComponentCallbackDelegate callback) where T : unmanaged, IComponentBase {

            var maxTypeId = StaticTypes.counter;
            WorldStaticCopyFromComponentCallbacksTypes.callbacks.Data.Resize(maxTypeId + 1u);
            WorldStaticCopyFromComponentCallbacksTypes.callbacks.Data.Get(StaticTypes<T>.typeId) = BurstCompiler.CompileFunctionPointer(callback);

        }

        /// <summary>
        /// Raises copy from component callback.
        /// </summary>
        public static unsafe void RaiseCopyFromComponentCallback(uint typeId, void* component, in Ent ent) {

            if (WorldStaticCopyFromComponentCallbacksTypes.callbacks.Data.Length == 0u) return;
            var callback = WorldStaticCopyFromComponentCallbacksTypes.callbacks.Data.Get(typeId);
            if (callback.IsCreated == true) callback.Invoke(component, in ent);

        }

        /// <summary>
        /// Registers config component callback.
        /// </summary>
        public static void RegisterConfigComponentCallback<T>(UnsafeEntityConfig.MethodCallerDelegate callback) where T : unmanaged, IComponentBase {

            var maxTypeId = StaticTypes.counter;
            WorldStaticConfigComponentCallbacksTypes.callbacks.Data.Resize(maxTypeId + 1u);
            WorldStaticConfigComponentCallbacksTypes.callbacks.Data.Get(StaticTypes<T>.typeId) = BurstCompiler.CompileFunctionPointer(callback);

        }

        /// <summary>
        /// Registers auto destroy callback.
        /// </summary>
        public static void RegisterAutoDestroyCallback<T>(AutoDestroyRegistry.DestroyDelegate callback) where T : unmanaged, IComponentDestroy {

            var typeId = StaticTypes<T>.typeId;
            StaticTypesDestroyRegistry.registry.Data.Resize(typeId + 1);
            StaticTypesDestroyRegistry.registry.Data.Get(typeId) = Unity.Burst.BurstCompiler.CompileFunctionPointer(callback);
            StaticTypesAutoDestroy<T>.registry.Data = true;
            StaticTypesAutoDestroy.registry.Data.Resize(typeId + 1);
            StaticTypesAutoDestroy.registry.Data.Get(typeId) = true;

        }

        /// <summary>
        /// Raises config component callback.
        /// </summary>
        public static unsafe void RaiseConfigComponentCallback<T>(in UnsafeEntityConfig config, void* component, in Ent ent) where T : unmanaged, IComponentBase {

            if (WorldStaticConfigComponentCallbacksTypes.callbacks.Data.Length == 0u) return;
            var callback = WorldStaticConfigComponentCallbacksTypes.callbacks.Data.Get(StaticTypes<T>.typeId);
            if (callback.IsCreated == true) callback.Invoke(in config, component, in ent);

        }

        /// <summary>
        /// Registers config component mask callback.
        /// </summary>
        public static void RegisterConfigComponentMaskCallback<T>(UnsafeEntityConfig.MethodMaskCallerDelegate callback) where T : unmanaged, IComponentBase {

            var maxTypeId = StaticTypes.counter;
            WorldStaticConfigComponentMaskCallbacksTypes.callbacks.Data.Resize(maxTypeId + 1u);
            WorldStaticConfigComponentMaskCallbacksTypes.callbacks.Data.Get(StaticTypes<T>.typeId) = BurstCompiler.CompileFunctionPointer(callback);

        }

        /// <summary>
        /// Raises config component mask callback.
        /// </summary>
        public static unsafe void RaiseConfigComponentMaskCallback<T>(in UnsafeEntityConfig config, void* component, void* configComponent, void* mask, in Ent ent) where T : unmanaged, IComponentBase {

            if (WorldStaticConfigComponentMaskCallbacksTypes.callbacks.Data.Length == 0u) return;
            var callback = WorldStaticConfigComponentMaskCallbacksTypes.callbacks.Data.Get(StaticTypes<T>.typeId);
            if (callback.IsCreated == true) callback.Invoke(in config, component, configComponent, mask, in ent);

        }

        /// <summary>
        /// Raises callback.
        /// </summary>
        public static void RaiseCallback<T>(ref T data, uint subId = 0u) where T : unmanaged {

            if (WorldStaticCallbacksTypes<T>.id == 0u) {
                WorldStaticCallbacksTypes<T>.id = ++WorldStaticCallbacksTypes.counter;
            }

            if (WorldStaticCallbacksTypes<T>.callbacks.TryGetValue(subId, out var callbackDelegate) == true) {
                
                callbackDelegate.Invoke(ref data);
                
            }
            
        }

        /// <summary>
        /// Registers callback.
        /// </summary>
        public static void RegisterCallback<T>(CallbackDelegate<T> callback, uint subId = 0u) where T : unmanaged {
            
            if (WorldStaticCallbacksTypes<T>.id == 0u) {
                WorldStaticCallbacksTypes<T>.id = ++WorldStaticCallbacksTypes.counter;
            }
            
            allDics.Add(WorldStaticCallbacksTypes<T>.callbacks);
            if (WorldStaticCallbacksTypes<T>.callbacks.ContainsKey(subId) == false) {
                
                WorldStaticCallbacksTypes<T>.callbacks.Add(subId, callback);
                
            } else {
                
                WorldStaticCallbacksTypes<T>.callbacks[subId] += callback;

            }
            
        }
        
        /// <summary>
        /// Unregisters callback.
        /// </summary>
        public static void UnregisterCallback<T>(CallbackDelegate<T> callback, uint subId = 0u) where T : unmanaged {
            
            if (WorldStaticCallbacksTypes<T>.callbacks.TryGetValue(subId, out var callbacks) == true) {

                callbacks -= callback;
                if (callbacks == null) {
                    WorldStaticCallbacksTypes<T>.callbacks.Remove(subId);
                } else {
                    WorldStaticCallbacksTypes<T>.callbacks[subId] = callbacks;
                }

            }
            
        }

    }

}