namespace ME.BECS {
    
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Burst;
    using Unity.Collections;
    using System.Runtime.InteropServices;
    using Unity.Jobs;
    using static Cuts;

    /// <summary>
    /// Defines registry caller base state and operations.
    /// </summary>
    public abstract class RegistryCallerBase {

        /// <summary>
        /// Invokes the registered callback with the supplied arguments.
        /// </summary>
        public abstract void Call(safe_ptr data);

        /// <summary>
        /// Provides the <c>Add</c> callback; this implementation performs no work.
        /// </summary>
        public virtual void Add(System.Delegate callback) {
            
        }

        /// <summary>
        /// Removes the specified entry from registry caller base.
        /// </summary>
        public virtual bool Remove(System.Delegate callback) {
            return false;
        }

    }

    /// <summary>
    /// Defines registry caller state and operations.
    /// </summary>
    public unsafe class RegistryCaller<T> : RegistryCallerBase where T : unmanaged {

        /// <summary>
        /// Callback invoked for callback.
        /// </summary>
        public GlobalEventWithDataCallback<T> callback;

        /// <summary>
        /// Adds the supplied entry to registry caller.
        /// </summary>
        public override void Add(System.Delegate callback) {

            this.callback += (GlobalEventWithDataCallback<T>)callback;

        }

        /// <summary>
        /// Removes the specified entry from registry caller.
        /// </summary>
        public override bool Remove(System.Delegate callback) {

            this.callback -= (GlobalEventWithDataCallback<T>)callback;
            return true;

        }

        /// <summary>
        /// Invokes the registered callback with the supplied arguments.
        /// </summary>
        public override void Call(safe_ptr data) {
            this.callback?.Invoke(*(T*)data.ptr);
        }

    }

    /// <summary>
    /// Defines registry caller state and operations.
    /// </summary>
    public class RegistryCaller : RegistryCallerBase {

        /// <summary>
        /// Callback invoked for callback.
        /// </summary>
        public GlobalEventCallback callback;

        /// <summary>
        /// Adds the supplied entry to registry caller.
        /// </summary>
        public override void Add(System.Delegate callback) {

            this.callback += (GlobalEventCallback)callback;

        }

        /// <summary>
        /// Removes the specified entry from registry caller.
        /// </summary>
        public override bool Remove(System.Delegate callback) {

            this.callback -= (GlobalEventCallback)callback;
            return true;

        }

        /// <summary>
        /// Invokes the registered callback with the supplied arguments.
        /// </summary>
        public override void Call(safe_ptr data) {
            this.callback?.Invoke();
        }

    }

    /// <summary>
    /// Stores the data and callbacks used by global events.
    /// </summary>
    public struct GlobalEventsData {

        /// <summary>
        /// Stores a item record used by <c>GlobalEventsData</c>.
        /// </summary>
        public struct Item {

            /// <summary>
            /// Data consumed or produced by the containing operation.
            /// </summary>
            public safe_ptr data;

        }
        
        /// <summary>
        /// Events queued or stored by this operation.
        /// </summary>
        public NativeHashMap<Event, Item> events;
        /// <summary>
        /// Spin lock used to coordinate access to this state.
        /// </summary>
        public LockSpinner spinner;

        /// <summary>
        /// Acquires the synchronization lock before accessing protected state.
        /// </summary>
        [INLINE(256)]
        public void Lock() {
            this.spinner.Lock();
        }
        
        /// <summary>
        /// Releases the synchronization lock after accessing protected state.
        /// </summary>
        [INLINE(256)]
        public void Unlock() {
            this.spinner.Unlock();
        }

    }

    /// <summary>
    /// Stores or dispatches events associated with a world.
    /// </summary>
    public class WorldEvents {

        /// <summary>
        /// Events queued or stored by this operation.
        /// </summary>
        public static readonly SharedStatic<Internal.Array<GlobalEventsData>> events = SharedStatic<Internal.Array<GlobalEventsData>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldEvents>(TAlign<Internal.Array<GlobalEventsData>>.align, 30100L);
        /// <summary>
        /// Read-only access to write spinner.
        /// </summary>
        public static readonly SharedStatic<ReadWriteNativeSpinner> readWriteSpinner = SharedStatic<ReadWriteNativeSpinner>.GetOrCreatePartiallyUnsafeWithHashCode<WorldEvents>(TAlign<ReadWriteNativeSpinner>.align, 30101L);
        /// <summary>
        /// Evt to callers used by <c>WorldEvents</c>.
        /// </summary>
        public static System.Collections.Generic.Dictionary<Event, RegistryCallerBase>[] evtToCallers;

    }

    /// <summary>
    /// Defines the callback signature for global event with data callback.
    /// </summary>
    public delegate void GlobalEventWithDataCallback<T>(T data) where T : unmanaged;
    /// <summary>
    /// Defines the callback signature for global event callback.
    /// </summary>
    public delegate void GlobalEventCallback();

    /// <summary>
    /// Dispatches registered world-associated callbacks through the global event infrastructure.
    /// </summary>
    public static unsafe class GlobalEvents {

        /// <summary>
        /// Initializes global events state from the supplied context.
        /// </summary>
        public static void Initialize() {
            Dispose();
            WorldEvents.readWriteSpinner.Data = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_PERSISTENT);
        }

        /// <summary>
        /// Releases the resources owned by this global events instance.
        /// </summary>
        public static void Dispose() {
            if (WorldEvents.readWriteSpinner.Data.IsCreated == true) WorldEvents.readWriteSpinner.Data.Dispose();
            ref var items = ref WorldEvents.events.Data;
            items.Dispose();
            WorldEvents.evtToCallers = null;
        }

        /// <summary>
        /// Releases the resources registered for the specified world.
        /// </summary>
        public static void DisposeWorld(ushort worldId) {
            if (WorldEvents.evtToCallers == null) return;
            ref var dic = ref WorldEvents.evtToCallers[worldId];
            if (dic != null) dic.Clear();
            if (worldId >= WorldEvents.events.Data.Length) return;
            ref var events = ref WorldEvents.events.Data.Get(worldId).events;
            if (events.IsCreated == true) {
                foreach (var item in events) {
                    if (item.Value.data.ptr != null) {
                        _free(item.Value.data);
                    }
                }
                events.Clear();
            }
        }
        
        /// <summary>
        /// Call this method from logic system
        /// </summary>
        /// <param name="evt"></param>
        [INLINE(256)]
        public static void RaiseEvent(in Event evt) {
            RaiseEvent<TNull>(in evt, default, false);
        }

        /// <summary>
        /// Call this method from logic step
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="data"></param>
        [INLINE(256)]
        public static void RaiseEvent<T>(in Event evt, in T data) where T : unmanaged {
            RaiseEvent(in evt, in data, true);
        }
        
        [INLINE(256)]
        private static void RaiseEvent<T>(in Event evt, in T data, bool useData) where T : unmanaged {

            WorldEvents.readWriteSpinner.Data.ReadBegin();
            if (WorldEvents.events.Data.IsCreated == false) {
                WorldEvents.readWriteSpinner.Data.ReadEnd();
                return;
            }
            WorldEvents.readWriteSpinner.Data.ReadEnd();
            
            var world = Worlds.GetWorld(evt.worldId);
            E.IS_VISUAL_MODE(world.state.ptr->Mode);
            
            WorldEvents.readWriteSpinner.Data.ReadBegin();
            ref var item = ref WorldEvents.events.Data.Get(evt.worldId);
            if (item.events.IsCreated == false) {
                item.Lock();
                if (item.events.IsCreated == false) {
                    item.events = new NativeHashMap<Event, GlobalEventsData.Item>(8, Constants.ALLOCATOR_PERSISTENT);
                }
                item.Unlock();
            }

            item.Lock();
            if (item.events.TryAdd(evt, new GlobalEventsData.Item()) == false) {
                var elem = item.events[evt];
                if (elem.data.ptr != null) {
                    fixed (void* dataPtr = &data) {
                        _memcpy((safe_ptr)dataPtr, elem.data, TSize<T>.size);
                    }
                } else {
                    elem.data = useData == true ? _make(data) : default;
                }
                item.events[evt] = elem;
            } else {
                var elem = item.events[evt];
                elem.data = useData == true ? _make(data) : default;
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
        [INLINE(256)][NotThreadSafe]
        public static bool UnregisterEvent(in Event evt, GlobalEventCallback callback) {

            if (WorldEvents.evtToCallers == null || evt.worldId >= WorldEvents.evtToCallers.Length) return false;
            
            if (WorldEvents.evtToCallers[evt.worldId].TryGetValue(evt, out var item) == true) {
                return item.Remove(callback);
            }

            return false;

        }

        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        [INLINE(256)][NotThreadSafe]
        public static bool UnregisterEvent<T>(in Event evt, GlobalEventWithDataCallback<T> callback) where T : unmanaged {

            if (WorldEvents.evtToCallers == null || evt.worldId >= WorldEvents.evtToCallers.Length) return false;
            
            if (WorldEvents.evtToCallers[evt.worldId].TryGetValue(evt, out var item) == true) {
                return item.Remove(callback);
            }

            return false;

        }
        
        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        [INLINE(256)][NotThreadSafe]
        public static void RegisterEvent<T>(in Event evt, GlobalEventWithDataCallback<T> callback) where T : unmanaged {

            ValidateCapacity();

            if (WorldEvents.evtToCallers == null || Worlds.MaxWorldId >= WorldEvents.evtToCallers.Length) System.Array.Resize(ref WorldEvents.evtToCallers, (int)(Worlds.MaxWorldId + 1u));
            if (WorldEvents.evtToCallers[evt.worldId] == null) WorldEvents.evtToCallers[evt.worldId] = new System.Collections.Generic.Dictionary<Event, RegistryCallerBase>();
            
            if (WorldEvents.evtToCallers[evt.worldId].TryGetValue(evt, out var item) == false) {
                WorldEvents.evtToCallers[evt.worldId].Add(evt, new RegistryCaller<T>() {
                    callback = callback,
                });
            } else {
                item.Add(callback);
            }

        }
        
        /// <summary>
        /// Call this method from UI
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="callback"></param>
        [INLINE(256)][NotThreadSafe]
        public static void RegisterEvent(in Event evt, GlobalEventCallback callback) {

            ValidateCapacity();

            if (WorldEvents.evtToCallers == null || Worlds.MaxWorldId >= WorldEvents.evtToCallers.Length) System.Array.Resize(ref WorldEvents.evtToCallers, (int)(Worlds.MaxWorldId + 1u));
            if (WorldEvents.evtToCallers[evt.worldId] == null) WorldEvents.evtToCallers[evt.worldId] = new System.Collections.Generic.Dictionary<Event, RegistryCallerBase>();

            if (WorldEvents.evtToCallers[evt.worldId].TryGetValue(evt, out var item) == false) {
                WorldEvents.evtToCallers[evt.worldId].Add(evt, new RegistryCaller() {
                    callback = callback,
                });
            } else {
                item.Add(callback);
            }

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
    
    /// <summary>
    /// Owns an ECS simulation state, entity storage and scheduled system work.
    /// </summary>
    public partial struct World {

        /// <summary>
        /// Executes global events process work through the job scheduler.
        /// </summary>
        public unsafe partial struct GlobalEventsProcessJob : IJob {

            /// <summary>
            /// Identifier of the world whose state this value addresses.
            /// </summary>
            public ushort worldId;
            
            /// <summary>
            /// Processes global events process using the supplied job inputs.
            /// </summary>
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
                if (WorldEvents.evtToCallers != null && this.worldId < WorldEvents.evtToCallers.Length) {
                    var callers = WorldEvents.evtToCallers[this.worldId];
                    foreach (var kv in item.events) {
                        var evt = kv.Key;
                        if (callers != null && callers.TryGetValue(evt, out var caller) == true) {
                            try {
                                caller.Call(kv.Value.data);
                            } catch (System.Exception ex) {
                                UnityEngine.Debug.LogException(ex);
                            }
                        }
                        if (kv.Value.data.ptr != null) {
                            _free(kv.Value.data);
                        }
                    }
                }
                item.events.Clear();
                item.Unlock();
                WorldEvents.readWriteSpinner.Data.ReadEnd();
                
            }

        }
        
        /// <summary>
        /// Raises events.
        /// </summary>
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