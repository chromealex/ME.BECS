namespace ME.BECS.Network {

    using Unity.Jobs;

    /// <summary>
    /// Configures network module behavior and storage.
    /// </summary>
    [System.Serializable]
    public struct NetworkModuleProperties {

        /// <summary>
        /// Configures methods storage behavior and storage.
        /// </summary>
        [System.Serializable]
        public struct MethodsStorageProperties {

            /// <summary>
            /// Default settings or value supplied by this type.
            /// </summary>
            public static MethodsStorageProperties Default => new MethodsStorageProperties() {
                capacity = 10u,
            };
            
            /// <summary>
            /// Methods storage resize by this value.
            /// </summary>
            [UnityEngine.Tooltip("Methods storage resize by this value.")]
            public uint capacity;

        }
        
        /// <summary>
        /// Configures events storage behavior and storage.
        /// </summary>
        [System.Serializable]
        public struct EventsStorageProperties {

            /// <summary>
            /// Default settings or value supplied by this type.
            /// </summary>
            public static EventsStorageProperties Default => new EventsStorageProperties() {
                capacity = 1000u,
                capacityPerTick = 30u,
                localPlayersCapacity = 1u,
                bufferCapacity = 1000u,
            };
            
            /// <summary>
            /// Events storage resize by this value.
            /// </summary>
            [UnityEngine.Tooltip("Events storage resize by this value.")]
            public uint capacity;
            /// <summary>
            /// Events storage per tick resize by this value.
            /// </summary>
            [UnityEngine.Tooltip("Events storage per tick resize by this value.")]
            public uint capacityPerTick;
            /// <summary>
            /// How many local players will be in your game.
            /// </summary>
            [UnityEngine.Tooltip("How many local players will be in your game.")]
            public uint localPlayersCapacity;
            /// <summary>
            /// Write/Read buffer capacity.
            /// </summary>
            [UnityEngine.Tooltip("Write/Read buffer capacity.")]
            public uint bufferCapacity;

        }

        /// <summary>
        /// Configures states storage behavior and storage.
        /// </summary>
        [System.Serializable]
        public struct StatesStorageProperties {

            /// <summary>
            /// Default settings or value supplied by this type.
            /// </summary>
            public static StatesStorageProperties Default => new StatesStorageProperties() {
                capacity = 10u,
                copyPerTick = 30u,
            };

            /// <summary>
            /// How many states we need to store.
            /// </summary>
            [UnityEngine.Tooltip("How many states we need to store.")]
            public uint capacity;
            /// <summary>
            /// Copy state every N ticks. This value is used on rollback.
            /// </summary>
            [UnityEngine.Tooltip("Copy state every N ticks. This value is used on rollback.")]
            public uint copyPerTick;

        }
        
        /// <summary>
        /// Configures hash table storage behavior and storage.
        /// </summary>
        [System.Serializable]
        public struct HashTableStorageProperties {

            /// <summary>
            /// Default settings or value supplied by this type.
            /// </summary>
            public static HashTableStorageProperties Default => new HashTableStorageProperties() {
                capacity = 10u,
            };

            /// <summary>
            /// How many hashes we need to store.
            /// </summary>
            [UnityEngine.Tooltip("How many hashes we need to store.")]
            public uint capacity;

        }

        /// <summary>
        /// Default settings or value supplied by this type.
        /// </summary>
        public static NetworkModuleProperties Default => new NetworkModuleProperties() {
            eventsStorageProperties = EventsStorageProperties.Default,
            statesStorageProperties = StatesStorageProperties.Default,
            methodsStorageProperties = MethodsStorageProperties.Default,
            hashTableStorageProperties = HashTableStorageProperties.Default,
            tickTime = 33u,
            maxFrameTime = 100u,
            inputLag = 1u,
            transport = new LocalTransport(),
        };
        
        /// <summary>
        /// How often should run Update methods on systems (ms).
        /// </summary>
        [UnityEngine.Tooltip("How often should run Update methods on systems (ms).")]
        public uint tickTime;
        /// <summary>
        /// How long can take one frame (ms). Logic will smoothly run up to the next frames.
        /// </summary>
        [UnityEngine.Tooltip("How long can take one frame (ms). Logic will smoothly run up to the next frames.")]
        public uint maxFrameTime;
        /// <summary>
        /// Input lag in ticks. How many ticks should be added to current tick when send network event.
        /// </summary>
        [UnityEngine.Tooltip("Input lag in ticks. How many ticks should be added to current tick when send network event.")]
        public uint inputLag;
        /// <summary>
        /// Custom transport implementation for INetworkTransport interface.
        /// </summary>
        [UnityEngine.SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(runtimeAssembliesOnly: true, showLabel = false)]
        [UnityEngine.Tooltip("Custom transport implementation for INetworkTransport interface.")]
        public INetworkTransport transport;
        /// <summary>
        /// Events storage properties used by <c>NetworkModuleProperties</c>.
        /// </summary>
        public EventsStorageProperties eventsStorageProperties;
        /// <summary>
        /// States storage properties used by <c>NetworkModuleProperties</c>.
        /// </summary>
        public StatesStorageProperties statesStorageProperties;
        /// <summary>
        /// Methods storage properties used by <c>NetworkModuleProperties</c>.
        /// </summary>
        public MethodsStorageProperties methodsStorageProperties;
        /// <summary>
        /// Indicates hash table storage properties.
        /// </summary>
        public HashTableStorageProperties hashTableStorageProperties;

    }
    
    /// <summary>
    /// Configures the transport and deterministic network simulation for a world.
    /// </summary>
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Network Module")]
    public unsafe class NetworkModule : Module {

        private enum NetworkState {
            /// <summary>
            /// In normal mode transport's server time will affect current network time as expected.
            /// </summary>
            Normal = 0,
            /// <summary>
            /// In replay mode transport's server time doesn't affect current network time.
            /// </summary>
            Replay = 1,
        }
        
        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public NetworkModuleProperties properties = NetworkModuleProperties.Default;
        private UnsafeNetworkModule network;
        private NetworkState networkState;

        /// <summary>
        /// Current state of the associated operation.
        /// </summary>
        public TransportStatus Status => this.network.networkTransport?.Status ?? TransportStatus.Unknown;
        
        /// <summary>
        /// Local player id used to locate the associated entry.
        /// </summary>
        public uint LocalPlayerId  => this.network.data.ptr->localPlayerId;

        /// <summary>
        /// Initializes network module state from the supplied context.
        /// </summary>
        public override void OnAwake(ref World world) {
            this.network = new UnsafeNetworkModule(in world, this.properties);
        }

        /// <summary>
        /// Starts network module processing for the supplied context.
        /// </summary>
        public override JobHandle OnStart(ref World world, JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Updates network module using the current inputs and execution context.
        /// </summary>
        public override JobHandle OnUpdate(JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Releases network module state at the end of its owning lifecycle.
        /// </summary>
        public override void DoDestroy() {
            this.network.Dispose();
        }

        /// <summary>
        /// Tests whether the context is in rollback.
        /// </summary>
        public bool IsInRollback() => this.network.IsInRollback();

        /// <summary>
        /// Updates initializer.
        /// </summary>
        public JobHandle UpdateInitializer(uint dtMs, NetworkWorldInitializer initializer, JobHandle dependsOn, ref World world) {
            
            {
                var serverTime = this.network.networkTransport.ServerTime;
                if (serverTime > this.GetCurrentTime() && this.networkState == NetworkState.Normal) {
                    this.SetServerTime(serverTime);
                } else {
                    this.SetServerTime(this.GetCurrentTime() + dtMs);
                }
            }

            if (this.network.networkTransport.Status == TransportStatus.Disconnected ||
                this.network.networkTransport.Status == TransportStatus.Unknown) {
                
                dependsOn = this.Connect(dependsOn);
                
            }

            this.network.PreUpdate(dependsOn, dtMs);

            return this.network.Update(initializer, dependsOn, ref world);
            
        }

        /// <summary>
        /// Returns min max ticks.
        /// </summary>
        public void GetMinMaxTicks(out ulong minTick, out ulong maxTick) {
            this.network.GetMinMaxTicks(out minTick, out maxTick);
        }

        /// <summary>
        /// Returns current time.
        /// </summary>
        public double GetCurrentTime() => this.network.GetCurrentTime();

        /// <summary>
        /// Sets local player ID.
        /// </summary>
        public void SetLocalPlayerId(uint playerId) {
            this.network.SetLocalPlayerId(playerId);
        }

        /// <summary>
        /// Sets server start time.
        /// </summary>
        public void SetServerStartTime(double startTime, in World world) {
            this.network.SetServerStartTime(startTime, in world);
        }

        /// <summary>
        /// Sets server time.
        /// </summary>
        public void SetServerTime(double timeFromStart) {
            this.network.SetServerTime(timeFromStart);
        }

        /// <summary>
        /// Saves reset state.
        /// </summary>
        public void SaveResetState() {
            this.network.SaveResetState();
        }

        /// <summary>
        /// Releases the stored reset snapshot.
        /// </summary>
        public void DropResetState() {
            this.network.DropResetState();
        }

        /// <summary>
        /// Returns reset state.
        /// </summary>
        public safe_ptr<State> GetResetState() => this.network.GetResetState();

        /// <summary>
        /// Registers method.
        /// </summary>
        public void RegisterMethod(NetworkMethodDelegate method) {
            this.network.RegisterMethod(method);
        }

        /// <summary>
        /// Adds event.
        /// </summary>
        public void AddEvent<T>(uint playerId, ushort methodId, in T data) where T : unmanaged, IPackageData {
            this.network.AddEvent(playerId, methodId, in data);
        }

        /// <summary>
        /// Starts a connection using the supplied transport configuration.
        /// </summary>
        public JobHandle Connect(JobHandle dependsOn) {
            return this.network.networkTransport.Connect(in this.network.data.ptr->connectedWorld, this, dependsOn);
        }

        /// <summary>
        /// Returns start frame state.
        /// </summary>
        public safe_ptr<State> GetStartFrameState() {
            return this.network.data.ptr->startFrameState;
        }

        /// <summary>
        /// Returns transport.
        /// </summary>
        public INetworkTransport GetTransport() {
            return this.network.GetTransport();
        }

        /// <summary>
        /// Returns events.
        /// </summary>
        public ULongDictionaryAuto<SortedNetworkPackageList> GetEvents() {
            return this.network.GetEvents();
        }

        /// <summary>
        /// Rewinds tracked state to the specified tick or position.
        /// </summary>
        public bool RewindTo(ulong targetTick) {
            return this.network.RewindTo(targetTick);
        }

        /// <summary>
        /// Returns current tick.
        /// </summary>
        public ulong GetCurrentTick() {
            return this.network.data.ptr->connectedWorld.CurrentTick;
        }

        /// <summary>
        /// Returns target tick.
        /// </summary>
        public ulong GetTargetTick() {
            return this.network.data.ptr->GetTargetTick();
        }

        /// <summary>
        /// Returns unsafe module.
        /// </summary>
        public UnsafeNetworkModule GetUnsafeModule() {
            return this.network;
        }

        /// <summary>
        /// Sets replay mode.
        /// </summary>
        public void SetReplayMode(bool replayMode) {
            if (replayMode == true) {
                this.networkState = NetworkState.Replay;
            } else {
                this.networkState = NetworkState.Normal;
            }
        }
        
        /// <summary>
        /// Tests whether the context is in replay mode.
        /// </summary>
        public bool IsInReplayMode() => this.networkState == NetworkState.Replay;

        /// <summary>
        /// Serializes all events.
        /// </summary>
        public byte[] SerializeAllEvents(ulong tickFrom = ulong.MinValue, ulong tickTo = ulong.MaxValue) {
            return this.network.SerializeAllEvents(tickFrom, tickTo);
        }

        /// <summary>
        /// Deserializes all events.
        /// </summary>
        public bool DeserializeAllEvents(byte[] bytes) {
            return this.network.DeserializeAllEvents(bytes);
        }
        
        /// <summary>
        /// Create diff patch between current state and reset state
        /// </summary>
        /// <returns></returns>
        public Patch CreatePatch() {
            var resetState = this.GetResetState();
            var currentState = this.network.data.ptr->connectedWorld.state;
            var resetStateWriter = new StreamBufferWriter();
            resetState.ptr->Serialize(ref resetStateWriter);
            var currentStateWriter = new StreamBufferWriter();
            currentState.ptr->Serialize(ref currentStateWriter);
            var patch = new Patch();
            Patch.GetDiff(new StreamBufferReader(resetStateWriter), new StreamBufferReader(currentStateWriter), ref patch);
            return patch;
        }

        /// <summary>
        /// Apply patch to current state
        /// </summary>
        /// <param name="patch"></param>
        public void ApplyPatch(in Patch patch) {
            Patch.Apply(in patch, this.network.data.ptr->connectedWorld.state);
            this.network.data.ptr->connectedWorld.UpdateAfterDeserialization();
        }
        
    }

}