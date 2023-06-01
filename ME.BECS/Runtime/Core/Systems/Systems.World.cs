namespace ME.BECS {
    
    using Unity.Jobs;
    using static Cuts;
    
    public static class WorldSystemRegistry {

        public static readonly System.Collections.Generic.Dictionary<ushort, SystemGroup> systemGroups = new System.Collections.Generic.Dictionary<ushort, SystemGroup>();
        
    }
    
    public static unsafe class SystemsWorldExt {

        public static void Awake(this ref World world) {
            Batches.Apply(world.state, world.id);
            world.Awake(default).Complete();
        }

        public static JobHandle Awake(this ref World world, JobHandle dependsOn) {
            
            E.IS_CREATED(world);
            dependsOn = Batches.Apply(dependsOn, world.state, world.id);
            var address = world.id;
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                
                dependsOn = rootGroup.Awake(ref world, dependsOn);
                
            }

            return dependsOn;

        }
        
        internal static JobHandle TickRootSystemGroup(this ref World world, float dt, JobHandle dependsOn) {

            var address = world.id;
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                
                dependsOn = rootGroup.Update(ref world, dt, dependsOn);

            }

            return dependsOn;

        }      

        public static void AssignRootSystemGroup(this ref World world, SystemGroup systemGroup) {

            var address = world.id;
            if (WorldSystemRegistry.systemGroups.ContainsKey(address) == true) {

                WorldSystemRegistry.systemGroups[address] = systemGroup;

            } else {

                WorldSystemRegistry.systemGroups.Add(address, systemGroup);

            }

        }
        
        internal static void UnassignRootSystemGroup(this ref World world, JobHandle dependsOn) {

            var address = world.id;
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {

                dependsOn = rootGroup.Destroy(ref world, dependsOn);
                dependsOn.Complete();
                rootGroup.Dispose();
                WorldSystemRegistry.systemGroups.Remove(address);

            }
            
        }

        public static ref T GetSystem<T>(this ref World world) where T : unmanaged, ISystem {
            
            var address = world.id;
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                var systemAddr = _address(ref rootGroup.GetSystem<T>(out var found));
                if (found == false) throw E.NOT_FOUND(typeof(T).Name);
                return ref _ref(systemAddr);
            }

            throw E.NOT_FOUND(typeof(T).Name);

        }

    }

}