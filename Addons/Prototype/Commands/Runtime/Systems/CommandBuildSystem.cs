namespace ME.BECS.Commands {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using Jobs;
    using Pathfinding;
    using Units;
    using Transforms;
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct CommandBuildSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<UnitCommandGroupAspect> {

            public BuildGraphSystem buildGraphSystem;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref UnitCommandGroupAspect commandGroup) {

                var parameters = commandGroup.ent.Read<CommandBuild>();
                if (parameters.building.Has<BuildingInProgress>() == false && parameters.building.IsActive() == false) {

                    var graph = this.buildGraphSystem.GetGraphByTypeId(parameters.buildingTypeId);
                    if (GraphUtils.IsGraphMaskValid(in graph, parameters.snappedPosition, parameters.rotation, parameters.size, 0, 254) == true) {

                        //UnityEngine.Debug.Log("Start Building: " + parameters.building);
                        // just turn on building's mask
                        // After building has been complete - turn on building
                        var nodes = parameters.building.Read<ChildrenComponent>().list;
                        for (uint i = 0u; i < nodes.Count; ++i) {
                            var node = nodes[i];
                            if (node.Has<GraphMaskComponent>() == true) {
                                node.SetActive(true);
                            }
                        }
                        // start building
                        // pick first unit in a group
                        var builder = commandGroup.readUnits[0];
                        builder.Set(new BuildInProgress() {
                            building = parameters.building,
                        });
                        var list = new ListAuto<Ent>(in parameters.building, 1u);
                        list.Add(builder);
                        parameters.building.Set(new BuildingInProgress() {
                            value = 0f,
                            timeToBuild = parameters.timeToBuild,
                            builders = list,
                        });

                    } else {

                        //UnityEngine.Debug.Log("Building Cancelled: " + parameters.building);
                        // Place is occupied already
                        // Destroy building because we have fail to build
                        parameters.building.DestroyHierarchy();
                        
                    }

                } else {
                    
                    // Building is deployment already - join to the process or skip this command
                    //UnityEngine.Debug.Log("Building Skipped: " + parameters.building + ", commandGroup: " + commandGroup.ent);
                    
                }

                UnitUtils.SetNextTargetIfAvailableExcept<BuildInProgress>(in commandGroup);

                commandGroup.ent.SetTag<IsCommandGroupDirty>(false);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var buildGraphSystem = context.world.GetSystem<BuildGraphSystem>();
            var handle = context.Query().With<CommandBuild>().With<IsCommandGroupDirty>().AsParallel().Schedule<Job, UnitCommandGroupAspect>(new Job() {
                buildGraphSystem = buildGraphSystem,
            });
            context.SetDependency(handle);
            
        }

    }

}