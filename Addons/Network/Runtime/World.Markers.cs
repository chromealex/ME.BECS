
namespace ME.BECS.Network.Markers {
    
    using BECS.Internal;
    using static Cuts;

    /// <summary>
    /// Stores the protocol header attached to internal network messages.
    /// </summary>
    public struct InternalNetworkHeader {

        /// <summary>
        /// Transport used by <c>InternalNetworkHeader</c>.
        /// </summary>
        public ClassPtr<INetworkTransport> transport;
        /// <summary>
        /// Module data used by <c>InternalNetworkHeader</c>.
        /// </summary>
        public safe_ptr<UnsafeNetworkModule.Data> moduleData;

        /// <summary>
        /// Releases the resources owned by this internal network header instance.
        /// </summary>
        public void Dispose() {
            this.transport.Dispose();
        }

    }

    /// <summary>
    /// Stores and indexes worlds network data entries.
    /// </summary>
    public struct WorldsNetworkDataStorage {

        private static readonly Unity.Burst.SharedStatic<Array<InternalNetworkHeader>> worldsArrBurst = Unity.Burst.SharedStatic<Array<InternalNetworkHeader>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsStorage>(TAlign<Array<InternalNetworkHeader>>.align, 10033);
        internal static ref ME.BECS.Internal.Array<InternalNetworkHeader> worlds => ref worldsArrBurst.Data;

        /// <summary>
        /// Clears state and releases resources managed by this operation.
        /// </summary>
        public static void CleanUp() {

            for (int i = 0; i < worlds.Length; ++i) {
                worlds.Get(i).Dispose();
            }

            worlds.Dispose();

        }

    }

    /// <summary>
    /// Defines world network markers state and operations.
    /// </summary>
    public static unsafe class WorldNetworkMarkers {

        /// <summary>
        /// Initializes world network markers state from the supplied context.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Reset);
            
        }
        
        /// <summary>
        /// Restores the tracked state to its initial values.
        /// </summary>
        public static void Reset() {
            WorldsNetworkDataStorage.CleanUp();
        }

        /// <summary>
        /// Stores the supplied value in world network markers.
        /// </summary>
        public static void Set(in World world, in UnsafeNetworkModule data) {
            
            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            ref var ptr = ref WorldsNetworkDataStorage.worlds.Get(world.id);
            ptr = new InternalNetworkHeader() {
                transport = _classPtr(data.networkTransport),
                moduleData = data.data,
            };

        }

        /// <summary>
        /// Sends network event.
        /// </summary>
        public static void SendNetworkEvent<T>(this in World world, T marker, NetworkMethodDelegate method) where T : unmanaged, IPackageData {

            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            var header = WorldsNetworkDataStorage.worlds.Get(world.id);
            var playerId = header.moduleData.ptr->localPlayerId;
            UnsafeNetworkModule.AddEvent(header.transport.Value, header.moduleData, playerId, method, marker, 0UL);
            
        }

        /// <summary>
        /// Sends network event.
        /// </summary>
        public static void SendNetworkEvent<T>(this in World world, T marker, NetworkMethodDelegate method, ulong negativeDelta) where T : unmanaged, IPackageData {

            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            var header = WorldsNetworkDataStorage.worlds.Get(world.id);
            var playerId = header.moduleData.ptr->localPlayerId;
            UnsafeNetworkModule.AddEvent(header.transport.Value, header.moduleData, playerId, method, marker, negativeDelta);
            
        }

    }

}