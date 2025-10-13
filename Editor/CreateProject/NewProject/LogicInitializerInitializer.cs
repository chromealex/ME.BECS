using UnityEngine;
using ME.BECS;
using ME.BECS.Network;
using Unity.Burst;

namespace NewProject {
    
    public class LogicWorld {
        public static readonly SharedStatic<World> world = SharedStatic<World>.GetOrCreate<LogicWorld>();
        public static World World => world.Data;
    }
    
    public class LogicInitializer : NetworkWorldInitializer {

        private static NetworkModule netModule;

        protected override void Awake() {
            
            base.Awake();
            LogicWorld.world.Data = this.world;
            netModule = this.GetModule<NetworkModule>();
            
        }

        public static NetworkModule GetNetworkModule() {
            return netModule;
        }

    }
    
}