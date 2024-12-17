namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Burst;
    using Unity.Collections;
    using System.Runtime.InteropServices;
    using Unity.Jobs;
    using static Cuts;

    public unsafe struct GlobalEventsData {

        public struct Item {

            public void* data;
            public System.IntPtr callback;
            public bool callbackSet;
            public GCHandle handle;
            public bool dataSet;
            public bool withData;

            public void Dispose() {
                this.handle.Free();
            }

        }
        
        public NativeHashMap<Event, Item> events;
        public LockSpinner spinner;

        public void Dispose() {
            foreach (var kv in this.events) {
                kv.Value.Dispose();
            }
        }
        
        [INLINE(256)]
        public void Lock() {
            this.spinner.Lock();
        }
        
        [INLINE(256)]
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
            Dispose();
            WorldEvents.readWriteSpinner.Data = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_DOMAIN);
        }

        public static void Dispose() {
            ref var items = ref WorldEvents.events.Data;
            for (uint i = 0u; i < items.Length; ++i) {
                ref var item = ref items.Get(i);
                if (item.events.IsCreated == true) {
                    item.Dispose();
                }
            }
            items.Dispose();
        }
        
        /// <summary>
        /// Call this method from logic system
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="context"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt, in SystemContext context) {
            RaiseEvent(in evt, null, in context);
        }

        /// <summary>
        /// Call this method from logic job
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt, in JobInfo jobInfo) {
            RaiseEvent(in evt, null, jobInfo.worldId);
        }

        /// <summary>
        /// Call this method from logic job with data
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="jobInfo"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt, void* data, in JobInfo jobInfo) {
            RaiseEvent(in evt, data, jobInfo.worldId);
        }

        /// <summary>
        /// Call this method from logic system with data
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="context"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt, void* data, in SystemContext context) {
            RaiseEvent(in evt, data, context.world.id);
        }
        
        /// <summary>
        /// Call this method from logic step
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        /// <param name="logicWorldId"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt, void* data, ushort logicWorldId) {

            var world = Worlds.GetWorld(evt.worldId);
            //var logicWorld = Worlds.GetWorld(logicWorldId);
            E.IS_VISUAL_MODE(world.state.ptr->mode);
            //E.IS_LOGIC_MODE(logicWorld.state.ptr->mode);
            //E.IS_IN_TICK(logicWorld.state);
            
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
            WorldEvents.readWriteSpinner.Data.ReadEnd();

        }

        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        [INLINE(256)]
        public static void RegisterEvent(in Event evt, GlobalEventWithDataCallback callback) {

            var handle = GCHandle.Alloc(callback);
            RegisterEvent(in evt, Marshal.GetFunctionPointerForDelegate(callback), handle, withData: true);

        }
        
        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        [INLINE(256)]
        public static void RegisterEvent(in Event evt, GlobalEventCallback callback) {

            var handle = GCHandle.Alloc(callback);
            RegisterEvent(in evt, Marshal.GetFunctionPointerForDelegate(callback), handle, withData: false);

        }
        
        [INLINE(256)]
        private static void RegisterEvent(in Event evt, System.IntPtr callback, GCHandle handle, bool withData) {

            var world = Worlds.GetWorld(evt.worldId);
            E.IS_VISUAL_MODE(world.state.ptr->mode);
            
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
                handle = handle,
                withData = withData,
                callbackSet = true,
            };
            item.Lock();
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                elem.callback = val.callback;
                elem.withData = val.withData;
                elem.callbackSet = true;
                item.events[evt] = elem;
            }
            item.Unlock();
            WorldEvents.events.Data.Get(evt.worldId) = item;
            WorldEvents.readWriteSpinner.Data.ReadEnd();

        }

        [INLINE(256)]
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

        public struct GlobalEventsProcessJob : IJob {

            public ushort worldId;
            
            public void Execute() {
                
                WorldEvents.readWriteSpinner.Data.ReadBegin();
                if (this.worldId >= WorldEvents.events.Data.Length) {
                    WorldEvents.readWriteSpinner.Data.ReadEnd();
                    return;
                }
                ref var item = ref WorldEvents.events.Data.Get(this.worldId);
                if (item.events.IsCreated == false) {
                    WorldEvents.readWriteSpinner.Data.ReadEnd();
                    return;
                }
                item.Lock();
                foreach (var kv in item.events) {
                    ref var val = ref kv.Value;
                    if (val.dataSet == true && val.callbackSet == true) {
                        val.dataSet = false;
                        if (val.withData == true) {
                            var del = Marshal.GetDelegateForFunctionPointer<GlobalEventWithDataCallback>(val.callback);
                            try {
                                del.Invoke(val.data);
                            } catch (System.Exception ex) {
                                UnityEngine.Debug.LogException(ex);
                            }
                            _free((safe_ptr)val.data);
                        } else {
                            var del = Marshal.GetDelegateForFunctionPointer<GlobalEventCallback>(val.callback);
                            try {
                                del.Invoke();
                            } catch (System.Exception ex) {
                                UnityEngine.Debug.LogException(ex);
                            }
                        }
                    }
                }
                item.Unlock();
                WorldEvents.readWriteSpinner.Data.ReadEnd();
                
            }

        }
        
        [INLINE(256)]
        public readonly JobHandle RaiseEvents(JobHandle dependsOn) {
            
            E.IS_NOT_IN_TICK(this.state);

            if (this.id >= WorldEvents.events.Data.Length) return dependsOn;
            
            dependsOn.Complete();
            new GlobalEventsProcessJob() {
                worldId = this.id,
            }.Execute();
            return dependsOn;

        }
        
    }
    
}