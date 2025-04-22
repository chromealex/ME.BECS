namespace ME.BECS {
    
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using Unity.Burst;
    using Unity.Collections;
    using System.Runtime.InteropServices;
    using Unity.Jobs;
    using static Cuts;

    public abstract class RegistryCallerBase {

        public abstract void Call(safe_ptr data);

        public virtual void Add(System.Delegate callback) {
            
        }

        public virtual bool Remove(System.Delegate callback) {
            return false;
        }

    }

    public unsafe class RegistryCaller<T> : RegistryCallerBase where T : unmanaged {

        public GlobalEventWithDataCallback<T> callback;

        public override void Add(System.Delegate callback) {

            this.callback += (GlobalEventWithDataCallback<T>)callback;

        }

        public override bool Remove(System.Delegate callback) {

            this.callback -= (GlobalEventWithDataCallback<T>)callback;
            return true;

        }

        public override void Call(safe_ptr data) {
            this.callback?.Invoke(*(T*)data.ptr);
        }

    }

    public class RegistryCaller : RegistryCallerBase {

        public GlobalEventCallback callback;

        public override void Add(System.Delegate callback) {

            this.callback += (GlobalEventCallback)callback;

        }

        public override bool Remove(System.Delegate callback) {

            this.callback -= (GlobalEventCallback)callback;
            return true;

        }

        public override void Call(safe_ptr data) {
            this.callback?.Invoke();
        }

    }

    public struct GlobalEventsData {

        public struct Item {

            public safe_ptr data;

        }
        
        public NativeHashMap<Event, Item> events;
        public LockSpinner spinner;

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
        public static System.Collections.Generic.Dictionary<Event, RegistryCallerBase>[] evtToCallers;

    }

    public delegate void GlobalEventWithDataCallback<T>(T data) where T : unmanaged;
    public delegate void GlobalEventCallback();

    public static unsafe class GlobalEvents {

        public static void Initialize() {
            Dispose();
            WorldEvents.readWriteSpinner.Data = ReadWriteNativeSpinner.Create(Constants.ALLOCATOR_DOMAIN);
        }

        public static void Dispose() {
            ref var items = ref WorldEvents.events.Data;
            items.Dispose();
            WorldEvents.evtToCallers = null;
        }

        public static void DisposeWorld(ushort worldId) {
            if (WorldEvents.evtToCallers == null) return;
            ref var dic = ref WorldEvents.evtToCallers[worldId];
            if (dic != null) dic.Clear();
            if (worldId >= WorldEvents.events.Data.Length) return;
            ref var events = ref WorldEvents.events.Data.Get(worldId).events;
            if (events.IsCreated == true) events.Clear();
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
            
            var world = Worlds.GetWorld(evt.worldId);
            E.IS_VISUAL_MODE(world.state.ptr->Mode);
            
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
                data = useData == true ? _make(data) : default,
            };
            item.Lock();
            if (item.events.TryAdd(evt, val) == false) {
                var elem = item.events[evt];
                _free(elem.data);
                elem.data = val.data;
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
    
    public partial struct World {

        public unsafe struct GlobalEventsProcessJob : IJob {

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
                        if (kv.Value.data.ptr != null) _free(kv.Value.data);
                    }
                }
                item.events.Clear();
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