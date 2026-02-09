
using Unity.Profiling;
#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS.Network {
    
    using Unity.Jobs;
    using ME.BECS.Network.Markers;
    using scg = System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using static Cuts;
    using Jobs;

    public unsafe struct SyncHashPackage : System.IComparable<SyncHashPackage>, System.IEquatable<SyncHashPackage> {

        public ulong tick;
        public uint playerId;
        public int hash;
        
        public static SyncHashPackage Create(ref StreamBufferReader reader) {

            var result = new SyncHashPackage();
            reader.Read(ref result.tick);
            reader.Read(ref result.playerId);
            reader.Read(ref result.hash);
            return result;

        }

        public void Serialize(ref StreamBufferWriter writeBufferWriter) {
            
            writeBufferWriter.Write(this.tick);
            writeBufferWriter.Write(this.playerId);
            writeBufferWriter.Write(this.hash);
            
        }
        
        public int CompareTo(SyncHashPackage other) {
            var tickComparison = this.tick.CompareTo(other.tick);
            if (tickComparison != 0) {
                return tickComparison;
            }

            var playerIdComparison = this.playerId.CompareTo(other.playerId);
            if (playerIdComparison != 0) {
                return playerIdComparison;
            }

            return this.hash.CompareTo(other.hash);
        }
        public bool Equals(SyncHashPackage other) {
            return this.tick == other.tick && this.playerId == other.playerId && this.hash == other.hash;
        }

        public override bool Equals(object obj) {
            return obj is SyncHashPackage other && this.Equals(other);
        }
        
        public override string ToString() {
            return $"[ SYNC PACKAGE ] Tick: {this.tick}, playerId: {this.playerId}, hash: {this.hash}";
        }
        
        public override int GetHashCode() {
            return System.HashCode.Combine(this.tick, this.playerId, this.hash);
        }
        
    }
    
    public unsafe struct NetworkPackage : System.IComparable<NetworkPackage>, System.IEquatable<NetworkPackage> {

        /// <summary>
        /// Tick
        /// </summary>
        public ulong tick;
        /// <summary>
        /// All packages ordered by playerId first
        /// </summary>
        public uint playerId;
        /// <summary>
        /// Registered method id
        /// </summary>
        public ushort methodId;
        /// <summary>
        /// Package data
        /// </summary>
        public ushort dataSize;
        /// <summary>
        /// Then by localOrder
        /// </summary>
        public byte localOrder;
        
        [NativeDisableUnsafePtrRestriction]
        public byte* data;

        public override string ToString() {
            return $"[ PACKAGE ] Tick: {this.tick}, playerId: {this.playerId}, methodId: {this.methodId}, dataSize: {this.dataSize}, localOrder: {this.localOrder}";
        }

        public string ToStringShort() => $"playerId: {this.playerId}, methodId: {this.methodId}";

        internal void Dispose() {
            _free((safe_ptr)this.data);
        }

        public ulong GetKey() {
            var a = ((ulong)this.playerId << 32);
            var b = ((ulong)this.localOrder & 0xffffffffL);
            return a | b;
        }

        public static NetworkPackage Create(ref StreamBufferReader reader) {

            var result = new NetworkPackage();
            reader.Read(ref result.tick);
            reader.Read(ref result.playerId);
            reader.Read(ref result.localOrder);
            reader.Read(ref result.methodId);
            reader.Read(ref result.dataSize);
            result.data = _make((uint)result.dataSize).ptr;
            reader.Read(ref result.data, result.dataSize);
            return result;

        }

        public void Serialize(ref StreamBufferWriter writeBufferWriter) {
            
            writeBufferWriter.Write(this.tick);
            writeBufferWriter.Write(this.playerId);
            writeBufferWriter.Write(this.localOrder);
            writeBufferWriter.Write(this.methodId);
            writeBufferWriter.Write(this.dataSize);
            writeBufferWriter.Write(this.data, this.dataSize);
            
        }

        public int CompareTo(NetworkPackage other) {
            var tickComparison = this.tick.CompareTo(other.tick);
            if (tickComparison != 0) {
                return tickComparison;
            }

            var playerIdComparison = this.playerId.CompareTo(other.playerId);
            if (playerIdComparison != 0) {
                return playerIdComparison;
            }

            return this.localOrder.CompareTo(other.localOrder);
        }

        public bool Equals(NetworkPackage other) {
            return this.tick == other.tick && this.playerId == other.playerId && this.methodId == other.methodId && this.dataSize == other.dataSize && this.localOrder == other.localOrder;
        }

        public override bool Equals(object obj) {
            return obj is NetworkPackage other && this.Equals(other);
        }

        public override int GetHashCode() {
            return System.HashCode.Combine(this.tick, this.playerId, this.methodId, this.dataSize, this.localOrder);
        }

    }

    public interface IPackageData {

        void Serialize(ref StreamBufferWriter writer);
        void Deserialize(ref StreamBufferReader reader);

    }

    public readonly unsafe ref struct InputData {

        private readonly NetworkPackage package;
        public readonly World world;

        public uint PlayerId => this.package.playerId;
        
        public InputData(NetworkPackage package, in World world) {
            this.package = package;
            this.world = world;
        }
        
        public T GetData<T>() where T : unmanaged, IPackageData {

            //E.SIZE_EQUALS(TSize<T>.size, this.package.dataSize);
            var result = default(T);
            var packageData = this.package.data;
            var readBuffer = new StreamBufferReader((safe_ptr)packageData, this.package.dataSize);
            result.Deserialize(ref readBuffer);
            return result;

        }

    }

    public delegate void NetworkMethodDelegate(in InputData data, ref SystemContext context);

    [System.AttributeUsageAttribute(System.AttributeTargets.Method)]
    public class NetworkMethodAttribute : AOT.MonoPInvokeCallbackAttribute {

        public NetworkMethodAttribute() : base(typeof(NetworkMethodDelegate)) {}

    }
    
    [BURST]
    public unsafe struct CopyStatePrepareJob : IJobSingle {
        
        public safe_ptr<UnsafeNetworkModule.Data> data;
        public Unity.Collections.NativeReference<System.IntPtr> tempData;
        
        public void Execute() {
            
            var srcState = this.data.ptr->connectedWorld.state;
            var state = State.ClonePrepare(srcState);
            this.tempData.Value = (System.IntPtr)state.ptr;
            var oldStateWasRemoved = this.data.ptr->statesStorage.Put(state, out var removedStateHash, out var removedStateTick);
            if (oldStateWasRemoved == true) {
                this.data.ptr->removedStateTick = removedStateTick;
                this.data.ptr->removedStateHash = removedStateHash;
                this.data.ptr->oldStateWasRemoved = true;
            }
            
        }
        
    }

    [BURST]
    public unsafe struct CopyStateCompleteJob : IJobParallelFor {
        
        public safe_ptr<UnsafeNetworkModule.Data> data;
        [Unity.Collections.ReadOnly]
        public Unity.Collections.NativeReference<System.IntPtr> tempData;
        
        public void Execute(int index) {
            
            var srcState = this.data.ptr->connectedWorld.state;
            State.CloneComplete(srcState, new safe_ptr<State>((State*)this.tempData.Value, TSize<State>.size), index);
            
        }
        
    }

    public unsafe struct UnsafeNetworkModule {

        public struct MethodsStorage {

            public struct Method {

                public GCHandle targetHandle;
                public GCHandle methodHandle;
                public void* methodPtr;

            }

            private MemArrayAuto<Method> methods;
            // methodPtr to methodId
            private EquatableDictionaryAuto<System.IntPtr, ushort> methodPtrs;
            private ushort index;
            private readonly safe_ptr<State> state;
            public NetworkModuleProperties.MethodsStorageProperties properties;

            public MethodsStorage(in World networkWorld, in World connectedWorld, NetworkModuleProperties.MethodsStorageProperties properties) {

                this.state = networkWorld.state;
                this.properties = properties;
                var ent = Ent.New(in networkWorld, editorName: "MethodsStorage");
                this.methods = new MemArrayAuto<Method>(in ent, properties.capacity);
                this.methodPtrs = new EquatableDictionaryAuto<System.IntPtr, ushort>(in ent, properties.capacity);
                this.index = 0;

            }

            public ushort GetMethodId(NetworkMethodDelegate method) {
                
                var ptr = Marshal.GetFunctionPointerForDelegate(method);
                if (this.methodPtrs.TryGetValue(ptr, out var methodId) == true) {
                    return methodId;
                }
                
                return this.Add(method);
                
            }
            
            public ushort Add(NetworkMethodDelegate method) {

                var ptr = Marshal.GetFunctionPointerForDelegate(method);
                if (this.methodPtrs.TryGetValue(ptr, out var id) == true) {

                    return id;

                }
                
                var idx = this.index++;
                id = (ushort)(idx + 1);
                if (idx >= this.methods.Length) this.methods.Resize(id, 2);

                var targetHandle = GCHandle.Alloc(method.Target);
                var handle = GCHandle.Alloc(method);
                ref var item = ref this.methods[this.state, idx];
                item.targetHandle = targetHandle;
                item.methodHandle = handle;
                item.methodPtr = (void*)Marshal.GetFunctionPointerForDelegate(method);
                
                this.methodPtrs.Add((System.IntPtr)item.methodPtr, id);
                
                return id;

            }

            public Method GetMethodInfo(uint methodId) {
                
                var idx = methodId - 1u;
                if (idx >= this.methods.Length) return default;

                return this.methods[this.state, idx];
                
            }

            public JobHandle Call(in NetworkPackage package, uint deltaTimeMs, in World world, JobHandle dependsOn) {
                
                var idx = package.methodId - 1u;
                if (idx >= this.methods.Length) return dependsOn;

                ref var item = ref this.methods[this.state, idx];
                var func = Marshal.GetDelegateForFunctionPointer<NetworkMethodDelegate>((System.IntPtr)item.methodPtr);
                var input = new InputData(package, in world);
                var context = SystemContext.Create(deltaTimeMs, world, dependsOn);
                func.Invoke(input, ref context);
                dependsOn = context.dependsOn;
                return dependsOn;

            }

            public void Dispose() {

                for (uint i = 0u; i < this.methods.Length; ++i) {

                    var item = this.methods[this.state, i];
                    if (item.methodPtr != null) {
                        item.targetHandle.Free();
                        item.methodHandle.Free();
                    }

                }

                this = default;

            }

        }
        
        public struct EventsStorage {

            public const ulong EMPTY_TICK = 0UL;
            
            // tick => [sorted events list by playerId + localOrder]
            private ULongDictionaryAuto<SortedNetworkPackageList> eventsByTick;
            private readonly safe_ptr<State> state;
            private ulong oldestTick;
            // playerId => localOrder
            private UIntDictionaryAuto<byte> localPlayersOrders;

            public readonly NetworkModuleProperties.EventsStorageProperties properties;

            public EventsStorage(in World networkWorld, in World connectedWorld, NetworkModuleProperties.EventsStorageProperties properties) {

                if (properties.capacity == 0u) properties.capacity = 1u;
                if (properties.capacityPerTick == 0u) properties.capacityPerTick = 1u;
                this.properties = properties;

                var ent = Ent.New(in networkWorld);
                this.state = networkWorld.state;
                this.eventsByTick = new ULongDictionaryAuto<SortedNetworkPackageList>(in ent, properties.capacity);
                this.oldestTick = EMPTY_TICK;
                this.localPlayersOrders = new UIntDictionaryAuto<byte>(in ent, this.properties.localPlayersCapacity);

            }

            public byte GetLocalOrder(uint playerId) {

                return ++this.localPlayersOrders.GetValue(playerId);

            }

            public void Dispose() {

                var e = this.eventsByTick.GetEnumerator(this.state);
                while (e.MoveNext() == true) {
                    var kv = e.Current;
                    var list = kv.value;
                    if (list.IsCreated == true) {
                        for (uint i = 0u; i < list.Count; ++i) {
                            var item = list[in this.state.ptr->allocator, i];
                            item.Dispose();
                        }
                    }
                }
                
            }

            public void Add(NetworkPackage package, ulong currentTick) {

                ref var list = ref this.eventsByTick.GetValue(package.tick);
                if (list.IsCreated == false) list = new SortedNetworkPackageList(ref this.state.ptr->allocator, this.properties.capacityPerTick);
                list.Add(ref this.state.ptr->allocator, package);
                
                Logger.Network.Log($"Added package (now: {currentTick}): {package}");

                if (package.tick < this.oldestTick || this.oldestTick == EMPTY_TICK) {
                    // Update the oldest tick to rollback in the future
                    this.oldestTick = package.tick;
                }

            }

            public ULongDictionaryAuto<SortedNetworkPackageList> GetEvents() {

                return this.eventsByTick;

            }

            public SortedNetworkPackageList GetEvents(ulong tick) {

                this.eventsByTick.TryGetValue(tick, out var list);
                return list;

            }

            public ulong GetOldestTickAndReset() {

                var oldestTick = this.oldestTick;
                this.oldestTick = EventsStorage.EMPTY_TICK;
                return oldestTick;

            }

            public ulong GetOldestTick() {

                return this.oldestTick;

            }

            public JobHandle Tick(ulong tick, uint deltaTimeMs, in World world, safe_ptr<Data> data, JobHandle dependsOn) {
                
                var events = this.GetEvents(tick);
                if (events.IsCreated == true && events.Count > 0u) {

                    ref var allocator = ref data.ptr->networkWorld.state.ptr->allocator;
                    for (uint i = 0u; i < events.Count; ++i) {

                        var evt = events[in allocator, i];
                        Logger.Network.Log($"Play event for tick {tick}: {evt}");
                        dependsOn = data.ptr->methodsStorage.Call(in evt, deltaTimeMs, in world, dependsOn);

                    }
                    
                }

                return dependsOn;

            }

            public void RemoveEvent(NetworkPackage package) {
                if (this.eventsByTick.TryGetValue(package.tick, out var list) == true) {
                    if (list.Remove(ref this.state.ptr->allocator, package) == true) {
                        this.eventsByTick[package.tick] = list;
                    }
                }
            }

            public void Clear() {
                foreach (var entry in this.eventsByTick) {
                    for (uint i = 0u; i < entry.value.Count; ++i) {
                        entry.value[this.state.ptr->allocator, i].Dispose();
                    }
                }
                this.eventsByTick.Clear();
            }

        }

        public struct StatesStorage {

            public struct Entry {

                public safe_ptr<State> state;
                public ulong tick;

            }

            public readonly NetworkModuleProperties.StatesStorageProperties properties;
            private readonly MemArrayAuto<Entry> entries;
            private safe_ptr<State> resetState;
            private uint rover;
            private readonly safe_ptr<State> networkState;
            private readonly safe_ptr<State> connectedWorldState;

            public StatesStorage(in World networkWorld, in World connectedWorld, NetworkModuleProperties.StatesStorageProperties properties) {

                this.connectedWorldState = connectedWorld.state;
                this.networkState = networkWorld.state;
                this.properties = properties;
                var ent = Ent.New(in networkWorld, editorName: "StatesStorage");
                this.entries = new MemArrayAuto<Entry>(in ent, this.properties.capacity);
                this.rover = 0u;
                this.resetState = default;

            }
            
            public readonly MemArrayAuto<Entry> GetEntries() => this.entries;

            public readonly void GetMinMaxTicks(out ulong minTick, out ulong maxTick) {
                minTick = ulong.MaxValue;
                maxTick = 0UL;
                for (uint i = 0u; i < this.entries.Length; ++i) {
                    var entry = this.entries[i];
                    if (entry.state.ptr != null) {
                        minTick = math.min(minTick, entry.tick);
                        maxTick = math.max(maxTick, entry.tick);
                    }
                }
            }

            internal bool Put(safe_ptr<State> state, out int removedStateHash, out ulong removedStateTick) {

                if (this.resetState.ptr == null) this.SaveResetState();
                
                if (this.rover >= this.entries.Length) {
                    this.rover = 0u;
                }

                removedStateHash = 0;
                removedStateTick = 0u;
                var oldStateWasRemoved = false;
                
                ref var item = ref this.entries[this.rover];
                if (item.state.ptr != null) {
                    removedStateHash = item.state.ptr->Hash;
                    removedStateTick = item.state.ptr->tick;
                    oldStateWasRemoved = true;
                    item.state.ptr->Dispose();
                    _free(item.state);
                }
                item = new Entry() {
                    state = state,
                    tick = state.ptr->tick,
                };
                ++this.rover;
                if (this.rover >= this.entries.Length) {
                    this.rover = 0u;
                }
                
                return oldStateWasRemoved;

            }

            public readonly safe_ptr<State> GetResetState() {
                return this.resetState;
            }

            public readonly JobHandle Tick(ulong tick, in World world, safe_ptr<Data> data, JobHandle dependsOn) {

                if (tick % this.properties.copyPerTick == 0u) {
                    
                    var tempData = new Unity.Collections.NativeReference<System.IntPtr>(Constants.ALLOCATOR_TEMPJOB);
                    var count = (int)data.ptr->connectedWorld.state.ptr->allocator.zonesCount;
                    dependsOn = new CopyStatePrepareJob() {
                        data = data,
                        tempData = tempData,
                    }.ScheduleSingle(dependsOn);
                    dependsOn = new CopyStateCompleteJob() {
                        data = data,
                        tempData = tempData,
                    }.Schedule(count, 4, dependsOn);
                    world.AddEndTickHandle(tempData.Dispose(dependsOn));
                    JobUtils.RunScheduled();

                }
                
                return dependsOn;

            }

            public void Dispose() {

                for (uint i = 0u; i < this.entries.Length; ++i) {

                    ref var entry = ref this.entries[i];
                    if (entry.state.ptr != null) {
                        entry.state.ptr->Dispose();
                        _free(entry.state);
                    }
                    
                }

                if (this.resetState.ptr != null) {
                    this.resetState.ptr->Dispose();
                    _free(this.resetState);
                }

                this = default;

            }

            public void InvalidateStatesFromTick(ulong tick) {

                var minTick = ulong.MaxValue;
                var minTickIndex = 0u;
                for (uint i = 0u; i < this.entries.Length; ++i) {

                    ref var entry = ref this.entries[i];
                    if (entry.tick >= tick && entry.state.ptr != null) {
                        if (entry.tick < minTick) {
                            minTick = entry.tick;
                            minTickIndex = i;
                        }
                        entry.state.ptr->Dispose();
                        _free(entry.state);
                        entry = default;
                    }
                    
                }

                if (minTick != ulong.MaxValue) this.rover = minTickIndex;
                
            }

            public readonly safe_ptr<State> GetStateForRollback(ulong tickToRollback) {

                safe_ptr<State> nearestState = default;
                var rover = this.rover;
                var delta = ulong.MaxValue;
                for (;;) {

                    ref var item = ref this.entries[rover];
                    if (item.tick <= tickToRollback) {
                        var d = tickToRollback - item.tick;
                        if (d < delta) {
                            delta = d;
                            nearestState = item.state;
                        }
                    }

                    if (rover == 0u) {
                        rover = this.entries.Length - 1u;
                        if (rover == this.rover) break;
                        continue;
                    }
                    --rover;
                    if (rover == this.rover) break;

                }

                return nearestState;

            }

            public void SaveResetState() {
                if (this.resetState.ptr == null) {
                    this.resetState = State.Clone(this.connectedWorldState);
                } else {
                    this.resetState.ptr->CopyFrom(in *this.connectedWorldState.ptr);
                }
            }

            public void Clear() {
                this.entries.Clear();
                this.rover = 0u;
            }

        }

        public struct HashTableStorage {

            public struct Entry {

                public MemArray<int> hashes;
                public MemArray<bool> hasHashFlags;

                public void Dispose(safe_ptr<State> state) {
                    this.hashes.Dispose(ref state.ptr->allocator);
                    this.hasHashFlags.Dispose(ref state.ptr->allocator);
                }

            }
            
            public readonly NetworkModuleProperties.HashTableStorageProperties hashTableStorageProperties;
            public readonly NetworkModuleProperties.StatesStorageProperties statesStorageProperties;

            private MemArray<Entry> entries;
            private ulong lastStoredTick;
            private uint rover;
            private safe_ptr<State> state;

            public HashTableStorage(in World networkWorld, in NetworkModuleProperties.HashTableStorageProperties hashTableStorageProperties, in NetworkModuleProperties.StatesStorageProperties statesStorageProperties) {

                this.hashTableStorageProperties = hashTableStorageProperties;
                this.statesStorageProperties = statesStorageProperties;
                this.lastStoredTick = 0u;
                this.rover = 0u;
                this.state = networkWorld.state;
                this.entries = new MemArray<Entry>(ref this.state.ptr->allocator, this.hashTableStorageProperties.capacity);

                for (var i = 0; i < this.entries.Length; ++i) {
                    this.entries[this.state, i] = new Entry() {
                        hashes = new MemArray<int>(ref this.state.ptr->allocator, 2u),
                        hasHashFlags = new MemArray<bool>(ref this.state.ptr->allocator, 2u),
                    };
                }

            }

            private void InvalidateEntry(uint index) {
                ref var entry = ref this.entries[this.state, index];
                entry.hasHashFlags.Clear(ref this.state.ptr->allocator);
            }

            private bool PutAndValidateEntry(uint index, uint playerIndex, int hash) {

                var length = playerIndex + 1u;
                ref var entry = ref this.entries[this.state, index];
                entry.hashes.Resize(ref this.state.ptr->allocator, length, 1);
                entry.hasHashFlags.Resize(ref this.state.ptr->allocator, length, 1);

                entry.hasHashFlags[this.state, playerIndex] = true;
                entry.hashes[this.state, playerIndex] = hash;

                for (var i = 0; i < entry.hashes.Length; ++i) {
                    if (entry.hasHashFlags[this.state, i] == true) {
                        if (entry.hashes[this.state, i] != hash) {
                            return false;
                        }
                    }
                }

                return true;

            }

            /// <summary>
            /// Adds hash sync package to the local hash sync table
            /// </summary>
            /// <param name="syncHashPackage"></param>
            /// <param name="innerHashIndex">Inner index of hash table where new hash was added</param>
            /// <returns>false if hash desync appeared on package addition</returns>
            public bool AddPackage(SyncHashPackage syncHashPackage, out uint innerHashIndex) {
                
                var tickToStore = syncHashPackage.tick;
                var playerId = syncHashPackage.playerId;
                var hash = syncHashPackage.hash;
                innerHashIndex = this.rover;

                if (this.lastStoredTick == 0u) {
                    this.lastStoredTick = tickToStore;
                    this.rover = 0u;
                }

                // Special case - tick to store out of table in the past
                if (tickToStore < this.lastStoredTick && (this.lastStoredTick - tickToStore) / this.statesStorageProperties.copyPerTick > this.entries.Length) {
                    // Ignore
                    return true;
                }

                var targetIndex = this.rover;

                if (tickToStore > this.lastStoredTick) {

                    for (var throughTick = this.lastStoredTick + this.statesStorageProperties.copyPerTick; throughTick <= tickToStore; throughTick += this.statesStorageProperties.copyPerTick) {
                        this.rover = (this.rover + 1u) % this.entries.Length;
                        this.lastStoredTick = throughTick;

                        this.InvalidateEntry(this.rover);
                    }
                    
                    targetIndex = this.rover;
                    
                }

                if (tickToStore < this.lastStoredTick) {
                    // Somewhere in the past, but in the range of stored ticks
                    var indexDelta = (uint) (this.lastStoredTick - tickToStore) / this.statesStorageProperties.copyPerTick;
                    var indexFrom = this.rover;
                    while (indexDelta > indexFrom) {
                        indexFrom += this.entries.Length;
                    }
                    targetIndex = indexFrom - indexDelta;
                    
                }

                innerHashIndex = targetIndex;
                var valid = this.PutAndValidateEntry(targetIndex, playerId, hash);
                return valid;

            }

            public void PrepareDesyncData(uint hashTableIndex, out bool[] hasHashFlags, out int[] hashes) {

                var entry = this.entries[this.state, hashTableIndex];
                hasHashFlags = new bool[entry.hashes.Length];
                hashes = new int[entry.hashes.Length];

                for (int i = 0; i < entry.hasHashFlags.Length; i++) {
                    hasHashFlags[i] = entry.hasHashFlags[this.state, i];
                }

                for (int i = 0; i < entry.hashes.Length; i++) {
                    hashes[i] = entry.hashes[this.state, i];
                }

            }

            public void Dispose() {
                
                for (var i = 0; i < this.entries.Length; ++i) {
                    this.entries[this.state, i].Dispose(this.state);
                }
                this.entries.Dispose(ref this.state.ptr->allocator);

            }
            
        }

        public struct Data {

            public double currentTimestamp;
            public double previousTimestamp;
            public uint localPlayerId;

            public StreamBufferWriter writeBuffer;
            public uint tickTime;
            public uint inputLag;

            public World networkWorld;
            public World connectedWorld;
            public EventsStorage eventsStorage;
            public StatesStorage statesStorage;
            public MethodsStorage methodsStorage;
            public HashTableStorage hashTableStorage;
            public safe_ptr<Data> selfPtr;
            public ulong rollbackTargetTick;

            public safe_ptr<State> startFrameState;

            public int removedStateHash;
            public ulong removedStateTick;
            public bool oldStateWasRemoved;
            
            [INLINE(256)]
            public Data(in World connectedWorld, NetworkModuleProperties properties) {

                this = default;
                this.tickTime = properties.tickTime;
                this.inputLag = properties.inputLag;
                var stateProperties = StateProperties.Min;
                stateProperties.mode = WorldMode.Visual;
                var worldProperties = new WorldProperties() {
                    allocatorProperties = new AllocatorProperties() {
                        sizeInBytesCapacity = (uint)MemoryAllocator.MIN_ZONE_SIZE,
                    },
                    stateProperties = stateProperties,
                    name = "Network World",
                };
                this.networkWorld = World.Create(worldProperties, false);
                this.connectedWorld = connectedWorld;

                this.writeBuffer = new StreamBufferWriter(properties.eventsStorageProperties.bufferCapacity);
                
                this.eventsStorage = new EventsStorage(in this.networkWorld, in this.connectedWorld, properties.eventsStorageProperties);
                this.statesStorage = new StatesStorage(in this.networkWorld, in this.connectedWorld, properties.statesStorageProperties);
                this.methodsStorage = new MethodsStorage(in this.networkWorld, in this.connectedWorld, properties.methodsStorageProperties);
                this.hashTableStorage = new HashTableStorage(in this.networkWorld, properties.hashTableStorageProperties, properties.statesStorageProperties);
                this.rollbackTargetTick = 0UL;
                this.startFrameState = _make(new State());

            }

            [INLINE(256)]
            public ulong GetTargetTick() {
                return (ulong)Unity.Mathematics.math.ceil(this.currentTimestamp / this.tickTime);
            }

            [INLINE(256)]
            public void SetServerStartTime(double startTime, in World world) {
                this.previousTimestamp = startTime;
                this.currentTimestamp = startTime;
                world.state.ptr->tick = this.GetTargetTick();
                Logger.Network.LogInfo($"SetServerStartTime: {startTime} => tick: {this.GetTargetTick()}", true);
            }

            [INLINE(256)]
            public void SetServerTime(double timeFromStart) {
                this.previousTimestamp = this.currentTimestamp;
                this.currentTimestamp = timeFromStart;
                Logger.Network.LogInfo($"SetServerTime: {timeFromStart} => tick: {this.GetTargetTick()}", true);
            }

            [INLINE(256)]
            public void SaveResetState() {
                this.statesStorage.SaveResetState();
            }

            [INLINE(256)]
            public bool IsRollbackRequired(ulong currentTick) {

                var tickToRollback = this.eventsStorage.GetOldestTick();
                if (tickToRollback != EventsStorage.EMPTY_TICK && currentTick >= tickToRollback) {

                    return true;

                }
                
                return false;

            }

            [INLINE(256)]
            public bool RollbackTo(ulong tickToRollback, ref ulong currentTick, ulong targetTick) {
                
                var rollbackState = this.statesStorage.GetStateForRollback(tickToRollback);
                if (rollbackState.ptr == null) {
                    // can't find state to roll back
                    // that means that requested tick had never seen before (player connected in the middle of the game)
                    // or it was reset by statesStorageProperties.capacity (event is out of history storage)
                    // so we can use reset state as the oldest state in history or throw an exception
                    rollbackState = this.statesStorage.GetResetState();
                }

                if (rollbackState.ptr == null) {
                    return false;
                }
                
                Logger.Network.Warning($"Rollback from {currentTick} to {tickToRollback}");
                currentTick = rollbackState.ptr->tick;
                var updateType = this.connectedWorld.state.ptr->updateType;
                var tickCheck = this.connectedWorld.state.ptr->tickCheck;
                var worldState = this.connectedWorld.state.ptr->WorldState;
                this.connectedWorld.state.ptr->CopyFrom(in *rollbackState.ptr);
                this.connectedWorld.state.ptr->updateType = updateType;
                this.connectedWorld.state.ptr->tickCheck = tickCheck;
                this.connectedWorld.state.ptr->WorldState = worldState;
                this.statesStorage.InvalidateStatesFromTick(currentTick);
                this.rollbackTargetTick = targetTick;
                Logger.Network.Warning($"Rollback State CopyFrom ended: {currentTick}..{targetTick}");

                return true;

            }
            
            [INLINE(256)]
            public JobHandle Rollback(ref ulong currentTick, ulong targetTick, JobHandle dependsOn) {

                var tickToRollback = this.eventsStorage.GetOldestTickAndReset();
                if (tickToRollback != EventsStorage.EMPTY_TICK && currentTick > tickToRollback) {
                    
                    // we need to rollback
                    // need to complete all dependencies
                    dependsOn.Complete();

                    if (this.RollbackTo(tickToRollback, ref currentTick, targetTick) == false) {
                        Logger.Network.Error("Rollback State is null. That means you are run out of state's history.");
                        throw new System.Exception();
                    }
                    
                }
                
                return dependsOn;
                
            }

            [INLINE(256)]
            public bool IsInRollback() {
                return this.IsInRollback(this.connectedWorld.state.ptr->tick);
            }

            [INLINE(256)]
            public bool IsInRollback(ulong tick) {
                return tick < this.rollbackTargetTick;
            }

            [INLINE(256)]
            public JobHandle Tick(ulong tick, uint deltaTimeMs, in World world, JobHandle dependsOn) {

                dependsOn = this.statesStorage.Tick(tick, in world, this.selfPtr, dependsOn);
                dependsOn = this.eventsStorage.Tick(tick, deltaTimeMs, in world, this.selfPtr, dependsOn);
                
                return dependsOn;

            }

            [INLINE(256)]
            public void Dispose() {
                this.writeBuffer.Dispose();
                this.eventsStorage.Dispose();
                this.statesStorage.Dispose();
                this.methodsStorage.Dispose();
                this.hashTableStorage.Dispose();
                this.networkWorld.Dispose();
                this.startFrameState.ptr->Dispose();
                _free(ref this.startFrameState);
            }
            
        }
        
        public readonly NetworkModuleProperties properties;
        internal readonly safe_ptr<Data> data;

        private readonly System.Diagnostics.Stopwatch frameStopwatch;
        internal INetworkTransport networkTransport;

        public UnsafeNetworkModule(in World connectedWorld, NetworkModuleProperties properties) {
            this = default;
            this.properties = properties;
            this.data = _make(new Data(in connectedWorld, properties));
            this.data.ptr->selfPtr = this.data;
            this.frameStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            this.SetTransport(properties.transport);
            // Register all methods for this module instance
            WorldStaticCallbacks.RaiseCallback(ref this.data.ptr->methodsStorage);
            ME.BECS.Network.Markers.WorldNetworkMarkers.Set(connectedWorld, in this);
        }
        
        public INetworkTransport GetTransport() => this.networkTransport;

        [INLINE(256)]
        public void SetTransport(INetworkTransport transport) {
            this.networkTransport = transport;
            this.networkTransport.OnAwake();
        }

        public ULongDictionaryAuto<SortedNetworkPackageList> GetEvents() => this.data.ptr->eventsStorage.GetEvents();

        public safe_ptr<Data> GetUnsafeData() => this.data;
        
        [INLINE(256)]
        public void Dispose() {
            if (this.networkTransport != null) this.networkTransport.Dispose();
            this.data.ptr->Dispose();
            _free(this.data);
            this = default;
        }

        [INLINE(256)]
        public bool IsInRollback() {
            return this.data.ptr->IsInRollback();
        }

        [INLINE(256)]
        public void GetMinMaxTicks(out ulong minTick, out ulong maxTick) {
            this.data.ptr->statesStorage.GetMinMaxTicks(out minTick, out maxTick);
        }

        [INLINE(256)]
        private uint GetDeltaTime() {
            return this.properties.tickTime;
        }

        [INLINE(256)]
        private ulong GetTargetTick() {
            return this.data.ptr->GetTargetTick();
        }

        [INLINE(256)]
        public void SetLocalPlayerId(uint playerId) {
            this.data.ptr->localPlayerId = playerId;
        }

        [INLINE(256)]
        public void SetServerStartTime(double startTime, in World world) {
            this.data.ptr->SetServerStartTime(startTime, in world);
        }

        [INLINE(256)]
        public void SetServerTime(double timeFromStart) {
            this.data.ptr->SetServerTime(timeFromStart);
        }

        [INLINE(256)]
        public void SaveResetState() {
            this.data.ptr->SaveResetState();
        }

        [INLINE(256)]
        public safe_ptr<State> GetResetState() {
            return this.data.ptr->statesStorage.GetResetState();
        }

        [INLINE(256)]
        public double GetCurrentTime() => this.data.ptr->currentTimestamp;

        public bool RewindTo(ulong targetTick) {

            if (targetTick > this.data.ptr->connectedWorld.state.ptr->tick) {
                // just set server time in the future
                this.data.ptr->SetServerTime(this.properties.tickTime * targetTick);
                return true;
            } else if (targetTick < this.data.ptr->connectedWorld.state.ptr->tick) {
                // rollback to targetTick
                this.data.ptr->SetServerTime(this.properties.tickTime * targetTick);
                return this.data.ptr->RollbackTo(targetTick, ref this.data.ptr->connectedWorld.state.ptr->tick, targetTick);
            }

            return false;

        }

        [INLINE(256)]
        public uint RegisterMethod(NetworkMethodDelegate method) {
            return this.data.ptr->methodsStorage.Add(method);
        }

        [INLINE(256)]
        public void AddEvent<T>(NetworkMethodDelegate method, in T data) where T : unmanaged, IPackageData {
            AddEvent(this.networkTransport, this.data, this.data.ptr->localPlayerId, this.data.ptr->methodsStorage.GetMethodId(method), in data, 0UL);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, NetworkMethodDelegate method, in T data) where T : unmanaged, IPackageData {
            AddEvent(this.networkTransport, this.data, playerId, this.data.ptr->methodsStorage.GetMethodId(method), in data, 0UL);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, NetworkMethodDelegate method, in T data, ulong negativeDeltaTicks) where T : unmanaged, IPackageData {
            AddEvent(this.networkTransport, this.data, playerId, this.data.ptr->methodsStorage.GetMethodId(method), in data, negativeDeltaTicks);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, ushort methodId, in T data) where T : unmanaged, IPackageData {
            AddEvent(this.networkTransport, this.data, playerId, methodId, in data, 0UL);
        }

        [INLINE(256)]
        public static void AddEvent<T>(INetworkTransport networkTransport, safe_ptr<Data> moduleData, uint playerId, NetworkMethodDelegate method, in T data, ulong negativeDeltaTicks) where T : unmanaged, IPackageData {
            AddEvent(networkTransport, moduleData, playerId, moduleData.ptr->methodsStorage.GetMethodId(method), in data, negativeDeltaTicks);
        }

        [INLINE(256)]
        public static void AddEvent<T>(INetworkTransport networkTransport, safe_ptr<Data> moduleData, uint playerId, ushort methodId, in T data, ulong negativeDeltaTicks) where T : unmanaged, IPackageData {

            if (networkTransport != null && networkTransport.Status != TransportStatus.Connected) {
                
                E.CustomException.Throw("NetworkModule is not connected");
                
            }

            ushort dataLength = 0;
            safe_ptr<byte> dataPtr = default;
            { // Custom data serialization
                moduleData.ptr->writeBuffer.Reset();
                data.Serialize(ref moduleData.ptr->writeBuffer);
                var dataBytes = moduleData.ptr->writeBuffer.ToArray();
                if (dataBytes.Length > 0) {
                    dataPtr = _makeArray<byte>((uint)dataBytes.Length);
                    fixed (void* ptr = &dataBytes[0]) {
                        _memcpy((safe_ptr)ptr, dataPtr, dataBytes.Length);
                    }
                    dataLength = (ushort)dataBytes.Length;
                }
            }

            // Form the package
            var tick = moduleData.ptr->GetTargetTick() - negativeDeltaTicks;
            var localOrder = moduleData.ptr->eventsStorage.GetLocalOrder(playerId);
            var package = new NetworkPackage() {
                tick = tick + moduleData.ptr->inputLag + (networkTransport != null ? networkTransport.InputLagInTicks : 0UL),
                playerId = playerId,
                localOrder = localOrder,
                methodId = methodId,
                data = dataPtr.ptr,
                dataSize = dataLength,
            };
            
            var eventsBehaviour = (EventsBehaviourState)EventsBehaviour.RunLocalOnly;
            if (networkTransport != null) {
                eventsBehaviour = (EventsBehaviourState)networkTransport.EventsBehaviour;
            }

            if ((eventsBehaviour & EventsBehaviourState.RunLocal) != 0) {
                // Store locally
                moduleData.ptr->eventsStorage.Add(package, moduleData.ptr->GetTargetTick());
            }

            if ((eventsBehaviour & EventsBehaviourState.SendToNetwork) != 0) {
                // Send to network
                if (networkTransport != null) {
                    moduleData.ptr->writeBuffer.Reset();
                    package.Serialize(ref moduleData.ptr->writeBuffer);
                    var bytes = moduleData.ptr->writeBuffer.ToArray();
                    networkTransport.Send(bytes);
                }
            }

        }

        public void PreUpdate(uint dtMs) {

            if (this.networkTransport is INetworkTransportPreUpdate networkTransportPreUpdate) {
                networkTransportPreUpdate.PreUpdate(dtMs);
            }

        }

        [INLINE(256)]
        public JobHandle Update(NetworkWorldInitializer initializer, JobHandle dependsOn, ref World world) {

            if (this.networkTransport.Status != TransportStatus.Connected) {
                Logger.Network.Log($"Transport status: {this.networkTransport.Status}");
                return dependsOn;
            } 
            
            dependsOn.Complete();
            {

                this.ReceiveHashState();
                
                var deltaTimeMs = this.GetDeltaTime();
                var currentTick = world.state.ptr->tick;
                var targetTick = this.GetTargetTick();
                while (true) {
                    var bytes = this.networkTransport.Receive();
                    if (bytes != null) {
                        var readBuffer = new StreamBufferReader(bytes);
                        var package = NetworkPackage.Create(ref readBuffer);
                        this.data.ptr->eventsStorage.Add(package, currentTick);
                        readBuffer.Dispose();
                    } else {
                        break;
                    }
                }
                if (targetTick > currentTick && targetTick - currentTick > 1) Logger.Network.LogInfo($"Tick {currentTick}..{targetTick}, dt: {deltaTimeMs}, ticks: {unchecked(targetTick - currentTick)}");
                {
                    // Do we need the rollback?
                    dependsOn = this.data.ptr->Rollback(ref currentTick, targetTick, dependsOn);
                }
                if (unchecked((targetTick - currentTick) > 0UL) && this.data.ptr->IsInRollback() == false) {
                    // Make a state copy for interpolation
                    this.data.ptr->startFrameState.ptr->CopyFrom(in *world.state.ptr);
                }
                //var completePerTick = this.properties.maxFrameTime / this.properties.tickTime;
                this.frameStopwatch.Restart();
                for (ulong tick = currentTick; tick < targetTick; ++tick) {

                    var marker = new ProfilerMarker("Tick");
                    marker.Begin();
                    // Begin tick
                    dependsOn = State.SetWorldState(in world, WorldState.BeginTick, UpdateType.FIXED_UPDATE, deltaTimeMs, dependsOn);
                    {
                        // Apply events for this tick
                        dependsOn = this.data.ptr->Tick(tick, deltaTimeMs, in world, dependsOn);
                    }
                    
                    dependsOn = world.TickWithoutWorldState(deltaTimeMs, UpdateType.FIXED_UPDATE, dependsOn);
                    dependsOn = State.SetWorldState(in world, WorldState.EndTick, UpdateType.FIXED_UPDATE, deltaTimeMs, dependsOn);
                    dependsOn.Complete();
                    // End tick
                    
                    marker.End();

                    this.SendHashState();

                    if (this.data.ptr->IsRollbackRequired(tick) == true) {
                        break;
                    }
                    
                    if (this.frameStopwatch.ElapsedMilliseconds >= this.properties.maxFrameTime) {
                        // drop current and try to targetTick in the next frame
                        break;
                    }
                    
                }
                JobUtils.RunScheduled();
            }

            return dependsOn;

        }

        private void SendHashState() {
            
            if (this.data.ptr->oldStateWasRemoved == true && this.data.ptr->removedStateTick != 0 && this.networkTransport is INetworkTransportHashSync networkTransportHashSync) {

                var package = new SyncHashPackage() {
                    hash = this.data.ptr->removedStateHash,
                    playerId = this.data.ptr->localPlayerId,
                    tick = this.data.ptr->removedStateTick,
                };

                this.AddSyncHashPackage(networkTransportHashSync, package);

                this.data.ptr->writeBuffer.Reset();
                package.Serialize(ref this.data.ptr->writeBuffer);
                var bytes = this.data.ptr->writeBuffer.ToArray();
                networkTransportHashSync.SendHashSync(bytes);

            }
            this.data.ptr->oldStateWasRemoved = false;
            
        }

        private void ReceiveHashState() {

            if (this.networkTransport is INetworkTransportHashSync networkTransportHashSync) {
                while (true) {
                    var bytes = networkTransportHashSync.ReceiveSyncHash();
                    if (bytes != null) {
                        var readBuffer = new StreamBufferReader(bytes);
                        var package = SyncHashPackage.Create(ref readBuffer);
                        this.AddSyncHashPackage(networkTransportHashSync, package);
                        // UnityEngine.Debug.Log($"Received hash state {package}");
                        readBuffer.Dispose();
                    } else {
                        break;
                    }
                }
            }
            
        }

        private void AddSyncHashPackage(INetworkTransportHashSync networkTransportHashSync, SyncHashPackage package) {
            var valid = this.data.ptr->hashTableStorage.AddPackage(package, out var innerHashIndex);
            if (valid == false) {
                this.data.ptr->hashTableStorage.PrepareDesyncData(innerHashIndex, out var hasHashFlags, out var hashes);
                networkTransportHashSync.OnHashDesync(package.tick, hasHashFlags, hashes);
            }
        }

        public byte[] SerializeAllEvents() {
            var events = this.GetEvents();
            var cnt = 0u;
            foreach (var e in events) {
                cnt += e.value.Count;
            }
            var stream = new StreamBufferWriter(cnt * 24u); // avg bytes per package
            stream.Write(this.GetResetState().ptr->tick);
            stream.Write(cnt);
            foreach (var e in events) {
                for (uint i = 0u; i < e.value.Count; ++i) {
                    var evt = e.value[this.data.ptr->networkWorld.state.ptr->allocator, i];
                    evt.Serialize(ref stream);
                }
            }
            var bytes = stream.ToArray();
            stream.Dispose();
            return bytes;
        }

        public bool DeserializeAllEvents(byte[] bytes) {
            try {
                var reader = new StreamBufferReader(bytes);
                uint count = 0u;
                ulong startTick = 0UL;
                reader.Read(ref startTick);
                reader.Read(ref count);
                var events = new Unity.Collections.NativeList<NetworkPackage>(Constants.ALLOCATOR_TEMP);
                for (uint i = 0u; i < count; ++i) {
                    var package = NetworkPackage.Create(ref reader);
                    events.Add(package);
                }
                this.RewindTo(startTick);
                this.data.ptr->eventsStorage.Clear();
                this.data.ptr->statesStorage.Clear();
                this.GetResetState().ptr->tick = startTick;
                foreach (var package in events) {
                    this.data.ptr->eventsStorage.Add(package, package.tick);
                }
                this.SetServerStartTime(startTick * this.properties.tickTime, in this.data.ptr->connectedWorld);
                this.SetServerTime(startTick * this.properties.tickTime);
            } catch (System.Exception ex) {
                UnityEngine.Debug.LogException(ex);
                return false;
            }
            return true;
        }

    }

}