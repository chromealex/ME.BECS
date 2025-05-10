namespace ME.BECS.Network {

    using Unity.Jobs;

    [System.Serializable]
    public struct NetworkModuleProperties {

        [System.Serializable]
        public struct MethodsStorageProperties {

            public static MethodsStorageProperties Default => new MethodsStorageProperties() {
                capacity = 10u,
            };
            
            [UnityEngine.Tooltip("Methods storage resize by this value.")]
            public uint capacity;

        }
        
        [System.Serializable]
        public struct EventsStorageProperties {

            public static EventsStorageProperties Default => new EventsStorageProperties() {
                capacity = 1000u,
                capacityPerTick = 30u,
                localPlayersCapacity = 1u,
                bufferCapacity = 1000u,
            };
            
            [UnityEngine.Tooltip("Events storage resize by this value.")]
            public uint capacity;
            [UnityEngine.Tooltip("Events storage per tick resize by this value.")]
            public uint capacityPerTick;
            [UnityEngine.Tooltip("How much local players will be in your game.")]
            public uint localPlayersCapacity;
            [UnityEngine.Tooltip("Write/Read buffer capacity.")]
            public uint bufferCapacity;

        }

        [System.Serializable]
        public struct StatesStorageProperties {

            public static StatesStorageProperties Default => new StatesStorageProperties() {
                capacity = 10u,
                copyPerTick = 30u,
            };

            [UnityEngine.Tooltip("How many states we need to store.")]
            public uint capacity;
            [UnityEngine.Tooltip("Copy state every N ticks. This value is used on rollback.")]
            public uint copyPerTick;

        }

        public static NetworkModuleProperties Default => new NetworkModuleProperties() {
            eventsStorageProperties = EventsStorageProperties.Default,
            statesStorageProperties = StatesStorageProperties.Default,
            methodsStorageProperties = MethodsStorageProperties.Default,
            tickTime = 33u,
            maxFrameTime = 100u,
            inputLag = 1u,
            transport = new LocalTransport(),
        };
        
        [UnityEngine.Tooltip("How often should run Update methods on systems (ms).")]
        public uint tickTime;
        [UnityEngine.Tooltip("How long can take one frame (ms). Logic will smoothly run up to the next frames.")]
        public uint maxFrameTime;
        [UnityEngine.Tooltip("Input lag in ticks. How much ticks should be added to current tick when send network event.")]
        public uint inputLag;
        [UnityEngine.SerializeReference]
        [ME.BECS.Extensions.SubclassSelector.SubclassSelectorAttribute(runtimeAssembliesOnly: true, showLabel = false)]
        [UnityEngine.Tooltip("Custom transport implementation for INetworkTransport interface.")]
        public INetworkTransport transport;
        public EventsStorageProperties eventsStorageProperties;
        public StatesStorageProperties statesStorageProperties;
        public MethodsStorageProperties methodsStorageProperties;

    }
    
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Network Module")]
    public unsafe class NetworkModule : Module {
        
        public NetworkModuleProperties properties = NetworkModuleProperties.Default;
        private UnsafeNetworkModule network;

        public TransportStatus Status => this.network.networkTransport.Status;
        
        public uint LocalPlayerId  => this.network.data.ptr->localPlayerId;

        public override void OnAwake(ref World world) {
            this.network = new UnsafeNetworkModule(in world, this.properties);
        }

        public override JobHandle OnStart(ref World world, JobHandle dependsOn) {
            return dependsOn;
        }

        public override JobHandle OnUpdate(JobHandle dependsOn) {
            return dependsOn;
        }

        public override void DoDestroy() {
            this.network.Dispose();
        }

        public bool IsInRollback() => this.network.IsInRollback();

        public JobHandle UpdateInitializer(uint dtMs, NetworkWorldInitializer initializer, JobHandle dependsOn, ref World world) {
            
            {
                var serverTime = this.network.networkTransport.ServerTime;
                if (serverTime > this.GetCurrentTime()) {
                    this.SetServerTime(serverTime);
                } else {
                    this.SetServerTime(this.GetCurrentTime() + dtMs);
                }
            }

            if (this.network.networkTransport.Status == TransportStatus.Disconnected ||
                this.network.networkTransport.Status == TransportStatus.Unknown) {
                
                dependsOn = this.Connect(dependsOn);
                
            }

            return this.network.Update(initializer, dependsOn, ref world);
            
        }

        public double GetCurrentTime() => this.network.GetCurrentTime();

        public void SetLocalPlayerId(uint playerId) {
            this.network.SetLocalPlayerId(playerId);
        }

        public void SetServerStartTime(double startTime, in World world) {
            this.network.SetServerStartTime(startTime, in world);
        }

        public void SetServerTime(double timeFromStart) {
            this.network.SetServerTime(timeFromStart);
        }

        public void SaveResetState() {
            this.network.SaveResetState();
        }

        public void RegisterMethod(NetworkMethodDelegate method) {
            this.network.RegisterMethod(method);
        }

        public void AddEvent<T>(uint playerId, ushort methodId, in T data) where T : unmanaged, IPackageData {
            this.network.AddEvent(playerId, methodId, in data);
        }

        public JobHandle Connect(JobHandle dependsOn) {
            return this.network.networkTransport.Connect(in this.network.data.ptr->connectedWorld, this, dependsOn);
        }

        public safe_ptr<State> GetStartFrameState() {
            return this.network.data.ptr->startFrameState;
        }

        public INetworkTransport GetTransport() {
            return this.network.GetTransport();
        }

        public ULongDictionaryAuto<SortedNetworkPackageList> GetEvents() {
            return this.network.GetEvents();
        }

        public bool RewindTo(ulong targetTick) {
            return this.network.RewindTo(targetTick);
        }

        public ulong GetCurrentTick() {
            return this.network.data.ptr->connectedWorld.CurrentTick;
        }

    }

}