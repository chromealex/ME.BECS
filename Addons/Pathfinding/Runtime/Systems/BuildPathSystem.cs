namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using Unity.Collections;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a path.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct BuildPathSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdatePathJob : IJobParallelForComponents<TargetComponent> {

            public World world;
            public Filter filter;
            
            public void Execute(in Ent ent, ref TargetComponent targetData) {
                
                if (targetData.target.IsAlive() == false) return;

                var targetInfo = targetData.target.Read<TargetInfoComponent>();
                Path path;
                MemArrayAuto<bool> chunksToUpdate;
                if (ent.TryRead(out TargetPathComponent targetPathComponent) == true) {
                    // update path
                    path = targetPathComponent.path;
                    Graph.SetTarget(ref path, targetInfo.position, in this.filter);
                    // use target chunks which must be updated in path follow system
                    chunksToUpdate = targetPathComponent.chunksToUpdate;
                    var updateRequired = new NativeReference<bool>(true, Allocator.Temp);
                    Graph.PathUpdateSync(in this.world, ref path, in path.graph, chunksToUpdate, path.filter, updateRequired);
                    var arr = targetPathComponent.chunksToUpdate;
                    _memclear(arr.GetUnsafePtr(), arr.Length * TSize<bool>.size);
                } else {
                    Graph.MakePath(in this.world, out path, in targetData.graphEnt, in targetInfo.position, this.filter);
                    chunksToUpdate = new MemArrayAuto<bool>(targetData.target, path.chunks.Length);
                }
                ent.Set(new TargetPathComponent() {
                    path = path,
                    chunksToUpdate = chunksToUpdate,
                });
                
            }

        }

        public Filter filter;
        
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).With<TargetComponent>().ScheduleParallelFor<UpdatePathJob, TargetComponent>(new UpdatePathJob() {
                world = context.world,
                filter = this.filter,
            });
            
            context.SetDependency(dependsOn);
            
        }

    }

}