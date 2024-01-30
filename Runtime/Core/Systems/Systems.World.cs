namespace ME.BECS {
    
    using Unity.Jobs;
    using static Cuts;
    using Unity.Collections;
    
    public class WorldSystemRegistry {

        private static readonly Unity.Burst.SharedStatic<NativeHashMap<ushort, SystemGroup>> systemGroupsBurst = Unity.Burst.SharedStatic<NativeHashMap<ushort, SystemGroup>>.GetOrCreate<WorldSystemRegistry>();
        public static ref NativeHashMap<ushort, SystemGroup> systemGroups => ref systemGroupsBurst.Data;

        public static void Validate() {

            if (systemGroups.IsCreated == false) {
                systemGroups = new NativeHashMap<ushort, SystemGroup>(10, Constants.ALLOCATOR_DOMAIN);
            }
            
        }
        
    }
    
    public static unsafe class SystemsWorldExt {

        public static void Awake(this ref World world) {
            Batches.Apply(world.state);
            world.Awake(default, 0).Complete();
        }

        public static JobHandle Awake(this ref World world, JobHandle dependsOn, ushort subId = 0) {
            
            E.IS_CREATED(world);
            dependsOn = Batches.Apply(dependsOn, world.state);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                
                // if we have static data
                if (SystemsStatic.RaiseOnAwake(in rootGroup, subId, 0f, ref world, ref dependsOn) == false) {

                    dependsOn = rootGroup.Awake(ref world, subId, dependsOn);

                }
                
            }

            return dependsOn;

        }
        
        public static void DrawGizmos(this ref World world) {
            Batches.Apply(world.state);
            world.DrawGizmos(default).Complete();
        }

        public static JobHandle DrawGizmos(this ref World world, JobHandle dependsOn) {
            
            E.IS_CREATED(world);
            dependsOn = Batches.Apply(dependsOn, world.state);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                
                // if we have static data
                if (SystemsStatic.RaiseOnDrawGizmos(in rootGroup, ref world, ref dependsOn) == false) {

                    dependsOn = rootGroup.DrawGizmos(ref world, dependsOn);

                }

            }

            return dependsOn;

        }

        internal static JobHandle TickRootSystemGroup(this ref World world, float dt, ushort updateType, JobHandle dependsOn) {

            E.IS_CREATED(world);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {

                // if we have static data
                if (SystemsStatic.RaiseOnUpdate(in rootGroup, updateType, dt, ref world, ref dependsOn) == false) {

                    dependsOn = rootGroup.Update(ref world, dt, updateType, dependsOn);

                }

            }

            return dependsOn;

        }      

        public static void AssignRootSystemGroup(this ref World world, SystemGroup systemGroup) {

            E.IS_CREATED(world);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.ContainsKey(address) == true) {

                WorldSystemRegistry.systemGroups[address] = systemGroup;

            } else {

                WorldSystemRegistry.systemGroups.Add(address, systemGroup);

            }

        }
        
        internal static void UnassignRootSystemGroup(this ref World world, JobHandle dependsOn) {

            E.IS_CREATED(world);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {

                // if we have static data
                if (SystemsStatic.RaiseOnDestroy(in rootGroup, 0, 0f, ref world, ref dependsOn) == false) {
                    dependsOn = rootGroup.Destroy(ref world, 0, dependsOn);
                }

                dependsOn.Complete();
                rootGroup.Dispose();
                WorldSystemRegistry.systemGroups.Remove(address);

            }
            
        }

        public static ref T GetSystem<T>(this in World world) where T : unmanaged, ISystem {
            
            E.IS_CREATED(world);
            var address = world.id;
            WorldSystemRegistry.Validate();
            if (WorldSystemRegistry.systemGroups.TryGetValue(address, out var rootGroup) == true) {
                if (SystemsStatic.TryGetSystem<T>(out var system) == false) {
                    var systemAddr = _addressT(ref rootGroup.GetSystem<T>(out var found));
                    if (found == false) throw E.NOT_FOUND(typeof(T).Name);
                    return ref _ref(systemAddr);
                } else {
                    return ref _ref(system);
                }
            }

            throw E.NOT_FOUND(typeof(T).Name);

        }

    }

}