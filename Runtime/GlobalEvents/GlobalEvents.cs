namespace ME.BECS {
    
    using Unity.Burst;
    using Unity.Collections;
    using System.Runtime.InteropServices;

    public unsafe struct GlobalEventsData {

        public struct Item {

            public void* data;
            public System.IntPtr callback;
            public bool dataSet;
            public bool withData;

        }
        
        public NativeHashMap<Event, Item> events;

    }

    public class WorldEvents {

        public static readonly SharedStatic<Internal.Array<GlobalEventsData>> events = SharedStatic<Internal.Array<GlobalEventsData>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldEvents>(TAlign<Internal.Array<GlobalEventsData>>.align, 30100L);

    }

    public unsafe delegate void GlobalEventWithDataCallback(void* data);
    public delegate void GlobalEventCallback();

    public static unsafe class GlobalEvents {

        /// <summary>
        /// Call this method from logic step
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="context"></param>
        public static void RaiseEvent(in Event evt, in SystemContext context) {
            RaiseEvent(in evt, null, in context);
        }

        /// <summary>
        /// Call this method from logic step
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="context"></param>
        public static void RaiseEvent(in Event evt, void* data, in SystemContext context) {
            
            E.IS_LOGIC_MODE(context.world.state->mode);
            E.IS_IN_TICK(context.world.state);
            
            WorldEvents.events.Data.Resize(evt.worldId + 1u);
            ref var item = ref WorldEvents.events.Data.Get(evt.worldId);
            if (item.events.IsCreated == false) item.events = new NativeHashMap<Event, GlobalEventsData.Item>(8, Constants.ALLOCATOR_DOMAIN);
            var val = new GlobalEventsData.Item() {
                data = data,
                dataSet = true,
            };
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                elem.data = val.data;
                elem.dataSet = true;
                item.events[evt] = elem;
            }

        }

        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        public static void RegisterEvent(in Event evt, GlobalEventWithDataCallback callback) {

            RegisterEvent(in evt, Marshal.GetFunctionPointerForDelegate(callback), withData: true);

        }
        
        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        public static void RegisterEvent(in Event evt, GlobalEventCallback callback) {

            RegisterEvent(in evt, Marshal.GetFunctionPointerForDelegate(callback), withData: false);

        }
        
        private static void RegisterEvent(in Event evt, System.IntPtr callback, bool withData) {

            var world = Worlds.GetWorld(evt.worldId);
            E.IS_VISUAL_MODE(world.state->mode);
            
            WorldEvents.events.Data.Resize(evt.worldId + 1u);
            ref var item = ref WorldEvents.events.Data.Get(evt.worldId);
            if (item.events.IsCreated == false) item.events = new NativeHashMap<Event, GlobalEventsData.Item>(8, Constants.ALLOCATOR_DOMAIN);
            var val = new GlobalEventsData.Item() {
                callback = callback,
                withData = withData,
            };
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                elem.callback = val.callback;
                elem.withData = val.withData;
                item.events[evt] = elem;
            }

        }

    }
    
    public unsafe partial struct World {

        public readonly void RaiseEvents() {
            
            E.IS_VISUAL_MODE(this.state->mode);

            ref var item = ref WorldEvents.events.Data.Get(this.id);
            foreach (var kv in item.events) {
                ref var val = ref kv.Value;
                if (val.dataSet == true) {
                    val.dataSet = false;
                    if (val.withData == true) {
                        var del = Marshal.GetDelegateForFunctionPointer<GlobalEventWithDataCallback>(val.callback);
                        del.Invoke(val.data);
                    } else {
                        var del = Marshal.GetDelegateForFunctionPointer<GlobalEventCallback>(val.callback);
                        del.Invoke();
                    }
                }
            }
            
        }
        
    }
    
}