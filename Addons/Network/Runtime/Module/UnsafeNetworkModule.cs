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

    public unsafe struct NetworkPackage {

        /// <summary>
        /// Tick
        /// </summary>
        public ulong tick;
        
        /// <summary>
        /// All packages ordered by playerId first
        /// </summary>
        public uint playerId;
        /// <summary>
        /// Then by localOrder
        /// </summary>
        public byte localOrder;

        /// <summary>
        /// Registered method id
        /// </summary>
        public ushort methodId;
        
        /// <summary>
        /// Package data
        /// </summary>
        public ushort dataSize;
        [NativeDisableUnsafePtrRestriction]
        public byte* data;

        internal void Dispose() {
            _free(this.data);
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
            result.data = _make((uint)result.dataSize);
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

    }

    public readonly unsafe ref struct InputData {

        private readonly NetworkPackage package;
        public readonly World world;

        public uint PlayerId => this.package.playerId;
        
        public InputData(NetworkPackage package, in World world) {
            this.package = package;
            this.world = world;
        }
        
        public T GetData<T>() where T : unmanaged {

            E.SIZE_EQUALS(TSize<T>.size, this.package.dataSize);
            return *(T*)this.package.data;

        }

    }

    public delegate JobHandle NetworkMethodDelegate(in InputData data, JobHandle dependsOn);

    [System.AttributeUsageAttribute(System.AttributeTargets.Method)]
    public class NetworkMethodAttribute : AOT.MonoPInvokeCallbackAttribute {

        public NetworkMethodAttribute() : base(typeof(NetworkMethodDelegate)) {}

    }
    
    public unsafe struct UnsafeNetworkModule {

        public struct MethodsStorage {

            private struct Method {

                public GCHandle targetHandle;
                public GCHandle methodHandle;
                public void* methodPtr;

            }

            private MemArray<Method> methods;
            // methodPtr to methodId
            private EquatableDictionary<System.IntPtr, ushort> methodPtrs;
            private ushort index;
            private readonly State* state;
            public NetworkModuleProperties.MethodsStorageProperties properties;

            public MethodsStorage(State* state, in World connectedWorld, NetworkModuleProperties.MethodsStorageProperties properties) {

                this.state = state;
                this.properties = properties;
                this.methods = new MemArray<Method>(ref state->allocator, properties.capacity, growFactor: 2);
                this.methodPtrs = new EquatableDictionary<System.IntPtr, ushort>(ref state->allocator, properties.capacity);
                this.index = 0;

            }

            public ushort GetMethodId(NetworkMethodDelegate method) {
                
                var ptr = Marshal.GetFunctionPointerForDelegate(method);
                if (this.methodPtrs.TryGetValue(in this.state->allocator, ptr, out var methodId) == true) {
                    return methodId;
                }
                
                return this.Add(method);
                
            }
            
            public ushort Add(NetworkMethodDelegate method) {

                var ptr = (void*)Marshal.GetFunctionPointerForDelegate(method);
                if (this.methodPtrs.TryGetValue(in this.state->allocator, (System.IntPtr)ptr, out var id) == true) {

                    return id;

                }
                
                var idx = this.index++;
                id = (ushort)(idx + 1);
                if (idx >= this.methods.Length) this.methods.Resize(ref this.state->allocator, id);

                var targetHandle = GCHandle.Alloc(method.Target);
                var handle = GCHandle.Alloc(method);
                ref var item = ref this.methods[this.state, idx];
                item.targetHandle = targetHandle;
                item.methodHandle = handle;
                item.methodPtr = ptr;
                
                this.methodPtrs.Add(ref this.state->allocator, (System.IntPtr)item.methodPtr, id);
                
                return id;

            }

            public JobHandle Call(in NetworkPackage package, in World world, JobHandle dependsOn) {
                
                var idx = package.methodId - 1u;
                if (idx >= this.methods.Length) return dependsOn;

                ref var item = ref this.methods[this.state, idx];
                var func = Marshal.GetDelegateForFunctionPointer<NetworkMethodDelegate>((System.IntPtr)item.methodPtr);
                var input = new InputData(package, in world);
                dependsOn = func.Invoke(input, dependsOn);
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
            private ULongDictionary<SortedNetworkPackageList> eventsByTick;
            private readonly State* state;
            private ulong oldestTick;
            // playerId => localOrder
            private UIntDictionary<byte> localPlayersOrders;

            public readonly NetworkModuleProperties.EventsStorageProperties properties;

            public EventsStorage(State* state, in World connectedWorld, NetworkModuleProperties.EventsStorageProperties properties) {

                if (properties.capacity == 0u) properties.capacity = 1u;
                if (properties.capacityPerTick == 0u) properties.capacityPerTick = 1u;
                this.properties = properties;

                this.state = state;
                this.eventsByTick = new ULongDictionary<SortedNetworkPackageList>(ref state->allocator, properties.capacity);
                this.oldestTick = EMPTY_TICK;
                this.localPlayersOrders = new UIntDictionary<byte>(ref state->allocator, this.properties.localPlayersCapacity);

            }

            public byte GetLocalOrder(uint playerId) {

                return ++this.localPlayersOrders.GetValue(ref this.state->allocator, playerId);

            }

            public void Dispose() {

                var e = this.eventsByTick.GetEnumerator(this.state);
                while (e.MoveNext() == true) {
                    var kv = e.Current;
                    var list = kv.value;
                    if (list.isCreated == true) {
                        for (uint i = 0u; i < list.Count; ++i) {
                            var item = list[in this.state->allocator, i];
                            item.Dispose();
                        }
                    }
                }
                
            }

            public void Add(NetworkPackage package) {

                ref var list = ref this.eventsByTick.GetValue(ref this.state->allocator, package.tick);
                if (list.isCreated == false) list = new SortedNetworkPackageList(ref this.state->allocator, this.properties.capacityPerTick);
                list.Add(ref this.state->allocator, package);

                if (package.tick < this.oldestTick || this.oldestTick == EMPTY_TICK) {
                    // Update oldest tick to rollback in the future
                    //UnityEngine.Debug.Log("Set oldest tick: " + package.tick);
                    this.oldestTick = package.tick;
                }

            }

            public SortedNetworkPackageList GetEvents(ulong tick) {

                this.eventsByTick.TryGetValue(in this.state->allocator, tick, out var list);
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

            public JobHandle Tick(ulong tick, in World world, Data* data, JobHandle dependsOn) {
                
                var events = this.GetEvents(tick);
                if (events.isCreated == true && events.Count > 0u) {

                    ref var allocator = ref data->networkWorld.state->allocator;
                    for (uint i = 0u; i < events.Count; ++i) {

                        var evt = events[in allocator, i];
                        dependsOn = data->methodsStorage.Call(in evt, in world, dependsOn);

                    }
                    
                }

                return dependsOn;

            }

        }

        public struct StatesStorage {

            private struct Entry {

                public State* state;
                public ulong tick;

            }

            public readonly NetworkModuleProperties.StatesStorageProperties properties;
            private readonly MemArray<Entry> entries;
            private State* resetState;
            private uint rover;
            private readonly State* networkState;
            private readonly State* connectedWorldState;

            public StatesStorage(State* state, in World connectedWorld, NetworkModuleProperties.StatesStorageProperties properties) {

                this.connectedWorldState = connectedWorld.state;
                this.networkState = state;
                this.properties = properties;
                this.entries = new MemArray<Entry>(ref state->allocator, this.properties.capacity);
                this.rover = 0u;
                this.resetState = null;

            }

            private void Put(State* state) {

                if (this.resetState == null) this.resetState = State.Clone(this.connectedWorldState);
                
                ref var item = ref this.entries[this.networkState, this.rover];
                if (item.state != null) {
                    item.state->Dispose();
                    _free(item.state);
                }
                item = new Entry() {
                    state = state,
                    tick = state->tick,
                };
                ++this.rover;
                if (this.rover >= this.entries.Length) {
                    this.rover = 0u;
                }

            }

            public State* GetResetState() {
                return this.resetState;
            }

            [BURST]
            private struct CopyStatePrepareJob : IJobSingle {

                [NativeDisableUnsafePtrRestriction]
                public Data* data;
                public Unity.Collections.NativeReference<System.IntPtr> tempData;
                
                public void Execute() {
                    
                    var srcState = this.data->connectedWorld.state;
                    var state = State.ClonePrepare(srcState);
                    this.tempData.Value = (System.IntPtr)state;
                    this.data->statesStorage.Put(state);
                    
                }

            }

            [BURST]
            private struct CopyStateCompleteJob : IJobParallelFor {

                [NativeDisableUnsafePtrRestriction]
                public Data* data;
                [Unity.Collections.ReadOnly]
                public Unity.Collections.NativeReference<System.IntPtr> tempData;
                
                public void Execute(int index) {
                    
                    var srcState = this.data->connectedWorld.state;
                    State.CloneComplete(srcState, (State*)this.tempData.Value, index);
                    
                }

            }

            public JobHandle Tick(ulong tick, in World world, Data* data, JobHandle dependsOn) {

                if (tick % this.properties.copyPerTick == 0u) {
                    
                    var tempData = new Unity.Collections.NativeReference<System.IntPtr>(Unity.Collections.Allocator.Persistent);
                    var count = (int)data->connectedWorld.state->allocator.zonesListCount;
                    dependsOn = new CopyStatePrepareJob() {
                        data = data,
                        tempData = tempData,
                    }.ScheduleSingle(dependsOn);
                    dependsOn = new CopyStateCompleteJob() {
                        data = data,
                        tempData = tempData,
                    }.Schedule(count, 4, dependsOn);
                    dependsOn = tempData.Dispose(dependsOn);
                    JobUtils.RunScheduled();

                }
                
                return dependsOn;

            }

            public void Dispose() {

                for (uint i = 0u; i < this.entries.Length; ++i) {

                    ref var entry = ref this.entries[this.networkState, i];
                    if (entry.state != null) entry.state->Dispose();
                    
                }

                this = default;

            }

            public void InvalidateStatesFromTick(ulong tick) {
                
                for (uint i = 0u; i < this.entries.Length; ++i) {

                    ref var entry = ref this.entries[this.networkState, i];
                    if (tick > entry.tick && entry.state != null) {
                        entry.state->Dispose();
                        entry = default;
                    }
                    
                }
                
            }

            public State* GetStateForRollback(ulong tickToRollback) {

                State* nearestState = null;
                var rover = this.rover;
                var delta = ulong.MaxValue;
                for (;;) {

                    ref var item = ref this.entries[this.networkState, rover];
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
            public Data* selfPtr;
            public ulong rollbackTargetTick;
            
            [INLINE(256)]
            public Data(in World connectedWorld, NetworkModuleProperties properties) {

                this = default;
                this.tickTime = properties.tickTime;
                this.inputLag = properties.inputLag;
                var worldProperties = new WorldProperties() {
                    allocatorProperties = new AllocatorProperties() {
                        sizeInBytesCapacity = (uint)MemoryAllocator.MIN_ZONE_SIZE,
                    },
                    name = "Network World",
                };
                this.networkWorld = World.CreateUninitialized(worldProperties);
                this.connectedWorld = connectedWorld;

                this.writeBuffer = new StreamBufferWriter(properties.eventsStorageProperties.bufferCapacity);
                
                var state = this.networkWorld.state;
                this.eventsStorage = new EventsStorage(state, this.connectedWorld, properties.eventsStorageProperties);
                this.statesStorage = new StatesStorage(state, this.connectedWorld, properties.statesStorageProperties);
                this.methodsStorage = new MethodsStorage(state, this.connectedWorld, properties.methodsStorageProperties);
                this.rollbackTargetTick = 0UL;

            }

            [INLINE(256)]
            public ulong GetTargetTick() {
                return (ulong)(this.currentTimestamp / this.tickTime);
            }

            [INLINE(256)]
            public void SetServerStartTime(double startTime, in World world) {
                this.previousTimestamp = startTime;
                this.currentTimestamp = startTime;
                world.state->tick = this.GetTargetTick();
            }

            [INLINE(256)]
            public void SetServerTime(double timeFromStart) {
                this.previousTimestamp = this.currentTimestamp;
                this.currentTimestamp = timeFromStart;
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
            public JobHandle Rollback(ref ulong currentTick, ref ulong targetTick, JobHandle dependsOn) {

                var tickToRollback = this.eventsStorage.GetOldestTickAndReset();
                if (tickToRollback != EventsStorage.EMPTY_TICK && currentTick > tickToRollback) {
                    
                    // we need to rollback
                    // need to complete all dependencies
                    dependsOn.Complete();
                    UnityEngine.Debug.LogWarning("Rollback from " + currentTick + " to " + tickToRollback);

                    var rollbackState = this.statesStorage.GetStateForRollback(tickToRollback);
                    if (rollbackState == null) {
                        // can't find state to rollback
                        // that means that requested tick had never seen before (player connected in the middle of the game)
                        // or it was reset by statesStorageProperties.capacity (event is out of history storage)
                        // so we can use reset state as an oldest state in history or throw an exception
                        rollbackState = this.statesStorage.GetResetState();
                    }

                    currentTick = rollbackState->tick;
                    UnityEngine.Debug.LogWarning("Rollback State tick: " + currentTick);
                    this.connectedWorld.state->CopyFrom(in *rollbackState);
                    this.statesStorage.InvalidateStatesFromTick(currentTick);
                    this.rollbackTargetTick = targetTick;
                    UnityEngine.Debug.LogWarning("Rollback State CopyFrom ended: " + currentTick + ".." + targetTick);
                    
                }
                
                return dependsOn;
                
            }

            [INLINE(256)]
            public bool IsInRollback() {
                return this.IsInRollback(this.connectedWorld.state->tick);
            }

            [INLINE(256)]
            public bool IsInRollback(ulong tick) {
                return tick < this.rollbackTargetTick;
            }

            [INLINE(256)]
            public JobHandle Tick(ulong tick, in World world, JobHandle dependsOn) {

                dependsOn = this.statesStorage.Tick(tick, in world, this.selfPtr, dependsOn);
                dependsOn = this.eventsStorage.Tick(tick, in world, this.selfPtr, dependsOn);
                
                return dependsOn;

            }

            [INLINE(256)]
            public void Dispose() {
                this.writeBuffer.Dispose();
                this.eventsStorage.Dispose();
                this.statesStorage.Dispose();
                this.methodsStorage.Dispose();
                this.networkWorld.Dispose();
            }
            
        }
        
        public readonly NetworkModuleProperties properties;
        internal readonly Data* data;

        private readonly System.Diagnostics.Stopwatch frameStopwatch;
        internal INetworkTransport networkTransport;

        public UnsafeNetworkModule(in World connectedWorld, NetworkModuleProperties properties) {
            this = default;
            this.properties = properties;
            this.data = _make(new Data(in connectedWorld, properties));
            this.data->selfPtr = this.data;
            this.frameStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            this.SetTransport(properties.transport);
            // Register all methods for this module instance
            WorldStaticCallbacks.RaiseCallback(ref this.data->methodsStorage);
            ME.BECS.Network.Markers.WorldNetworkMarkers.Set(connectedWorld, in this);
        }

        [INLINE(256)]
        public void SetTransport(INetworkTransport transport) {
            this.networkTransport = transport;
            this.networkTransport.OnAwake();
        }

        public struct TestData {

            public int a;
            public byte b;
            public int c;

        }

        [NetworkMethod]
        [AOT.MonoPInvokeCallback(typeof(NetworkMethodDelegate))]
        public static JobHandle TestNetMethod(in InputData data, JobHandle dependsOn) {
            var input = data.GetData<TestData>();
            UnityEngine.Debug.Log("TestNetMethod: " + input.a + " :: " + input.b + " :: " + input.c);
            return dependsOn;
        }

        [INLINE(256)]
        public void Dispose() {
            if (this.networkTransport != null) this.networkTransport.Dispose();
            this.data->Dispose();
            this = default;
        }

        [INLINE(256)]
        public bool IsInRollback() {
            return this.data->IsInRollback();
        }

        [INLINE(256)]
        private float GetDeltaTime() {
            return this.properties.tickTime / 1000f;
        }

        [INLINE(256)]
        private ulong GetTargetTick() {
            return this.data->GetTargetTick();
        }

        [INLINE(256)]
        public void SetLocalPlayerId(uint playerId) {
            this.data->localPlayerId = playerId;
        }

        [INLINE(256)]
        public void SetServerStartTime(double startTime, in World world) {
            this.data->SetServerStartTime(startTime, in world);
        }
        
        [INLINE(256)]
        public void SetServerTime(double timeFromStart) {
            this.data->SetServerTime(timeFromStart);
        }

        [INLINE(256)]
        public uint RegisterMethod(NetworkMethodDelegate method) {
            return this.data->methodsStorage.Add(method);
        }

        [INLINE(256)]
        public void AddEvent<T>(NetworkMethodDelegate method, in T data) where T : unmanaged {
            AddEvent(this.networkTransport, this.data, this.data->localPlayerId, this.data->methodsStorage.GetMethodId(method), in data, 0UL);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, NetworkMethodDelegate method, in T data) where T : unmanaged {
            AddEvent(this.networkTransport, this.data, playerId, this.data->methodsStorage.GetMethodId(method), in data, 0UL);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, NetworkMethodDelegate method, in T data, ulong negativeDeltaTicks) where T : unmanaged {
            AddEvent(this.networkTransport, this.data, playerId, this.data->methodsStorage.GetMethodId(method), in data, negativeDeltaTicks);
        }

        [INLINE(256)]
        public void AddEvent<T>(uint playerId, ushort methodId, in T data) where T : unmanaged {
            AddEvent(this.networkTransport, this.data, playerId, methodId, in data, 0UL);
        }

        [INLINE(256)]
        public static void AddEvent<T>(INetworkTransport networkTransport, Data* moduleData, uint playerId, NetworkMethodDelegate method, in T data, ulong negativeDeltaTicks) where T : unmanaged {
            AddEvent(networkTransport, moduleData, playerId, moduleData->methodsStorage.GetMethodId(method), in data, negativeDeltaTicks);
        }

        [INLINE(256)]
        public static void AddEvent<T>(INetworkTransport networkTransport, Data* moduleData, uint playerId, ushort methodId, in T data, ulong negativeDeltaTicks) where T : unmanaged {
            
            // Form the package
            var tick = moduleData->GetTargetTick() - negativeDeltaTicks;
            var localOrder = moduleData->eventsStorage.GetLocalOrder(playerId);
            var package = new NetworkPackage() {
                tick = tick + moduleData->inputLag,
                playerId = playerId,
                localOrder = localOrder,
                methodId = methodId,
                data = (byte*)_make(data),
                dataSize = (ushort)TSize<T>.size,
            };
            
            var eventsBehaviour = (EventsBehaviourState)EventsBehaviour.RunLocalOnly;
            if (networkTransport != null) {
                eventsBehaviour = (EventsBehaviourState)networkTransport.EventsBehaviour;
            }

            if ((eventsBehaviour & EventsBehaviourState.RunLocal) != 0) {
                // Store locally
                moduleData->eventsStorage.Add(package);
            }

            if ((eventsBehaviour & EventsBehaviourState.SendToNetwork) != 0) {
                // Send to network
                if (networkTransport != null) {
                    moduleData->writeBuffer.Reset();
                    package.Serialize(ref moduleData->writeBuffer);
                    var bytes = moduleData->writeBuffer.ToArray();
                    networkTransport.Send(bytes);
                }
            }

        }

        [INLINE(256)]
        public JobHandle Update(NetworkWorldInitializer initializer, JobHandle dependsOn, ref World world) {

            if (this.networkTransport.Status != TransportStatus.Connected) return dependsOn; 
            
            /*
            // test
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.A) == true) {
                // Add forward
                this.AddEvent(1u, UnsafeNetworkModule.TestNetMethod, new TestData() {
                    a = 123,
                    b = 234,
                    c = 567,
                });
            }
            if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.S) == true) {
                // Add backward
                this.data->connectedWorld.SendNetworkEvent(new TestData() {
                    a = 123,
                    b = 234,
                    c = 567,
                }, UnsafeNetworkModule.TestNetMethod, negativeDelta: 20);
            }
            */
            
            dependsOn.Complete();
            {
                var deltaTime = this.GetDeltaTime();
                var currentTick = world.state->tick;
                var targetTick = this.GetTargetTick();
                {
                    var bytes = this.networkTransport.Receive();
                    if (bytes != null) {
                        var readBuffer = new StreamBufferReader(bytes);
                        var package = NetworkPackage.Create(ref readBuffer);
                        this.data->eventsStorage.Add(package);
                        readBuffer.Dispose();
                    }
                }
                if (targetTick > currentTick && targetTick - currentTick > 1) UnityEngine.Debug.Log($"Tick {currentTick}..{targetTick}, dt: {deltaTime}, ticks: {(targetTick - currentTick)}");
                {
                    // Do we need the rollback?
                    dependsOn = this.data->Rollback(ref currentTick, ref targetTick, dependsOn);
                }
                //var completePerTick = this.properties.maxFrameTime / this.properties.tickTime;
                this.frameStopwatch.Restart();
                for (ulong tick = currentTick; tick < targetTick; ++tick) {

                    //UnityEngine.Debug.LogWarning("---- BEGIN TICK ----");
                    dependsOn = State.SetWorldState(in world, WorldState.BeginTick, dependsOn);
                    {
                        // Apply events for this tick
                        //dependsOn.Complete();
                        dependsOn = this.data->Tick(tick, in world, dependsOn);
                    }
                    
                    //UnityEngine.Debug.LogWarning("---- BEGIN WORLD TICK ----");
                    dependsOn = world.Tick(deltaTime, dependsOn);
                    dependsOn = State.SetWorldState(in world, WorldState.EndTick, dependsOn);
                    dependsOn.Complete();

                    if (this.data->IsRollbackRequired(tick) == true) {
                        break;
                    }
                    //UnityEngine.Debug.LogWarning("---- END TICK ----");
                    
                    if (this.frameStopwatch.ElapsedMilliseconds >= this.properties.maxFrameTime) {
                        // drop current and try to targetTick in the next frame
                        break;
                    }
                    
                }
                JobUtils.RunScheduled();
            }

            return dependsOn;

        }

    }

}