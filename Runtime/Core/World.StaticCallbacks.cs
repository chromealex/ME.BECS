namespace ME.BECS {

    public static class WorldStaticCallbacksTypes {

        public static uint counter;

    }
    
    public static class WorldStaticCallbacksTypes<T> where T : unmanaged {

        public static uint id;

        public static System.Collections.Generic.Dictionary<uint, WorldStaticCallbacks.CallbackDelegate<T>> callbacks = new System.Collections.Generic.Dictionary<uint, WorldStaticCallbacks.CallbackDelegate<T>>();

    }

    public static class WorldStaticCallbacks {

        public delegate void CallbackDelegate<T>(ref T data) where T : unmanaged;

        public static void RaiseCallback<T>(ref T data, uint subId = 0u) where T : unmanaged {

            if (WorldStaticCallbacksTypes<T>.id == 0u) {
                WorldStaticCallbacksTypes<T>.id = ++WorldStaticCallbacksTypes.counter;
            }

            if (WorldStaticCallbacksTypes<T>.callbacks.TryGetValue(subId, out var callbackDelegate) == true) {
                
                callbackDelegate.Invoke(ref data);
                
            }
            
        }

        public static void RegisterCallback<T>(CallbackDelegate<T> callback, uint subId = 0u) where T : unmanaged {
            
            if (WorldStaticCallbacksTypes<T>.id == 0u) {
                WorldStaticCallbacksTypes<T>.id = ++WorldStaticCallbacksTypes.counter;
            }
            
            if (WorldStaticCallbacksTypes<T>.callbacks.ContainsKey(subId) == false) {
                
                WorldStaticCallbacksTypes<T>.callbacks.Add(subId, callback);
                
            } else {
                
                WorldStaticCallbacksTypes<T>.callbacks[subId] += callback;

            }
            
        }

    }

}