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
        public LockSpinner spinner;

        public void Lock() {
            this.spinner.Lock();
        }
        
        public void Unlock() {
            this.spinner.Unlock();
        }

    }

    public class WorldEvents {

        public static readonly SharedStatic<Internal.Array<GlobalEventsData>> events = SharedStatic<Internal.Array<GlobalEventsData>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldEvents>(TAlign<Internal.Array<GlobalEventsData>>.align, 30100L);
        public static readonly SharedStatic<ReadWriteNativeSpinner> readWriteSpinner = SharedStatic<ReadWriteNativeSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<WorldEvents>(TAlign<ReadWriteNativeSpinner>.align, 30101L);
        public static readonly SharedStatic<LockSpinner> spinner = SharedStatic<LockSpinner>.GetOrCreate<WorldEvents>();

    }

    public unsafe delegate void GlobalEventWithDataCallback(void* data);
    public delegate void GlobalEventCallback();

    public static unsafe class GlobalEvents {

        public static void Initialize() {
            WorldEvents.readWriteSpinner.Data = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_DOMAIN);
        }
        
        /// <summary>
        /// Call this method from logic system
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="context"></param>
        public static void RaiseEvent(in Event evt, in SystemContext context) {
            RaiseEvent(in evt, null, in context);
        }

        /// <summary>
        /// Call this method from logic job
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="jobInfo"></param>
        public static void RaiseEvent(in Event evt, in JobInfo jobInfo) {
            RaiseEvent(in evt, null, jobInfo.worldId);
        }

        /// <summary>
        /// Call this method from logic job with data
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        public static void RaiseEvent(in Event evt, void* data, in JobInfo jobInfo) {
            RaiseEvent(in evt, data, jobInfo.worldId);
        }

        /// <summary>
        /// Call this method from logic system with data
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="context"></param>
        public static void RaiseEvent(in Event evt, void* data, in SystemContext context) {
            RaiseEvent(in evt, data, context.world.id);
        }
        
        /// <summary>
        /// Call this method from logic step
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="logicWorldId"></param>
        public static void RaiseEvent(in Event evt, void* data, ushort logicWorldId) {

            var world = Worlds.GetWorld(evt.worldId);
            var logicWorld = Worlds.GetWorld(logicWorldId);
            E.IS_VISUAL_MODE(world.state->mode);
            E.IS_LOGIC_MODE(logicWorld.state->mode);
            E.IS_IN_TICK(logicWorld.state);
            
            ValidateCapacity();
            
            WorldEvents.readWriteSpinner.Data.ReadBegin();
            ref var item = ref WorldEvents.events.Data.Get(evt.worldId);
            if (item.events.IsCreated == false) {
                item.Lock();
                if (item.events.IsCreated == false) {
                    item.events = new NativeHashMap<Event, GlobalEventsData.Item>(8, Constants.ALLOCATOR_DOMAIN);
                }
                item.Unlock();
            }
            var val = new GlobalEventsData.Item() {
                data = data,
                dataSet = true,
            };
            item.Lock();
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                elem.data = val.data;
                elem.dataSet = true;
                item.events[evt] = elem;
            }
            item.Unlock();
            WorldEvents.events.Data.Get(evt.worldId) = item;
            WorldEvents.readWriteSpinner.Data.ReadEnd();

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
            
            ValidateCapacity();
            
            WorldEvents.readWriteSpinner.Data.ReadBegin();
            ref var item = ref WorldEvents.events.Data.Get(evt.worldId);
            if (item.events.IsCreated == false) {
                item.Lock();
                if (item.events.IsCreated == false) {
                    item.events = new NativeHashMap<Event, GlobalEventsData.Item>(8, Constants.ALLOCATOR_DOMAIN);
                }
                item.Unlock();
            }
            var val = new GlobalEventsData.Item() {
                callback = callback,
                withData = withData,
            };
            item.Lock();
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                elem.callback = val.callback;
                elem.withData = val.withData;
                item.events[evt] = elem;
            }
            item.Unlock();
            WorldEvents.events.Data.Get(evt.worldId) = item;
            WorldEvents.readWriteSpinner.Data.ReadEnd();

        }

        private static void ValidateCapacity() {
            if (Worlds.MaxWorldId > WorldEvents.events.Data.Length) {
                WorldEvents.readWriteSpinner.Data.WriteBegin();
                if (Worlds.MaxWorldId > WorldEvents.events.Data.Length) {
                    WorldEvents.events.Data.Resize(Worlds.MaxWorldId + 1u);
                }
                WorldEvents.readWriteSpinner.Data.WriteEnd();
            }
        }

    }
    
    public unsafe partial struct World {

        public readonly void RaiseEvents() {
            
            E.IS_NOT_IN_TICK(this.state);

            WorldEvents.readWriteSpinner.Data.ReadBegin();
            ref var item = ref WorldEvents.events.Data.Get(this.id);
            item.Lock();
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
            item.Unlock();
            WorldEvents.readWriteSpinner.Data.ReadEnd();

        }
        
    }
    
}