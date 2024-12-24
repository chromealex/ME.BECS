
namespace ME.BECS.Network.Markers {
    
    using BECS.Internal;
    using static Cuts;

    public struct InternalNetworkHeader {

        public ClassPtr<INetworkTransport> transport;
        public safe_ptr<UnsafeNetworkModule.Data> moduleData;

        public void Dispose() {
            this.transport.Dispose();
        }

    }

    public struct WorldsNetworkDataStorage {

        private static readonly Unity.Burst.SharedStatic<Array<InternalNetworkHeader>> worldsArrBurst = Unity.Burst.SharedStatic<Array<InternalNetworkHeader>>.GetOrCreatePartiallyUnsafeWithHashCode<WorldsStorage>(TAlign<Array<InternalNetworkHeader>>.align, 10033);
        internal static ref ME.BECS.Internal.Array<InternalNetworkHeader> worlds => ref worldsArrBurst.Data;

        public static void CleanUp() {

            for (int i = 0; i < worlds.Length; ++i) {
                worlds.Get(i).Dispose();
            }

            worlds.Dispose();

        }

    }

    public static unsafe class WorldNetworkMarkers {

        [UnityEngine.RuntimeInitializeOnLoadMethodAttribute(UnityEngine.RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void Initialize() {
            
            CustomModules.RegisterResetPass(Reset);
            
        }
        
        public static void Reset() {
            WorldsNetworkDataStorage.CleanUp();
        }

        public static void Set(in World world, in UnsafeNetworkModule data) {
            
            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            ref var ptr = ref WorldsNetworkDataStorage.worlds.Get(world.id);
            ptr = new InternalNetworkHeader() {
                transport = _classPtr(data.networkTransport),
                moduleData = data.data,
            };

        }

        public static void SendNetworkEvent<T>(this in World world, T marker, NetworkMethodDelegate method) where T : unmanaged, IPackageData {

            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            var header = WorldsNetworkDataStorage.worlds.Get(world.id);
            var playerId = header.moduleData.ptr->localPlayerId;
            UnsafeNetworkModule.AddEvent(header.transport.Value, header.moduleData, playerId, method, marker, 0UL);
            
        }

        public static void SendNetworkEvent<T>(this in World world, T marker, NetworkMethodDelegate method, ulong negativeDelta) where T : unmanaged, IPackageData {

            WorldsNetworkDataStorage.worlds.Resize(world.id + 1u);
            var header = WorldsNetworkDataStorage.worlds.Get(world.id);
            var playerId = header.moduleData.ptr->localPlayerId;
            UnsafeNetworkModule.AddEvent(header.transport.Value, header.moduleData, playerId, method, marker, negativeDelta);
            
        }

    }

}