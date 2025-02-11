#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Pathfinding {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Collections;
    using static Cuts;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Schedule building a path.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public struct BuildPathSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public unsafe struct UpdatePathJob : IJobForComponents<TargetComponent> {

            public World world;
            public Filter filter;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TargetComponent targetData) {
                
                if (targetData.target.IsAlive() == false) return;

                var targetInfo = targetData.target.Read<TargetInfoComponent>();
                Path path;
                MemArrayAuto<byte> chunksToUpdate;
                if (ent.TryRead(out TargetPathComponent targetPathComponent) == true) {
                    // update path
                    path = targetPathComponent.path;
                    Graph.SetTarget(ref path, targetInfo.position, in this.filter);
                    // use target chunks which must be updated in path follow system
                    chunksToUpdate = targetPathComponent.chunksToUpdate;
                    var updateRequired = new NativeReference<byte>(1, Allocator.Temp);
                    Graph.PathUpdateSync(in this.world, ref path, in path.graph, chunksToUpdate, path.filter, updateRequired);
                    var arr = targetPathComponent.chunksToUpdate;
                    _memclear(arr.GetUnsafePtr(), arr.Length * TSize<byte>.size);
                } else {
                    Graph.MakePath(in this.world, out path, in targetData.graphEnt, in targetInfo.position, this.filter);
                    chunksToUpdate = new MemArrayAuto<byte>(targetData.target, path.chunks.Length);
                }
                ent.Set(new TargetPathComponent() {
                    path = path,
                    chunksToUpdate = chunksToUpdate,
                });
                
            }

        }

        public Filter filter;
        
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = API.Query(in context).With<TargetComponent>().AsParallel().Schedule<UpdatePathJob, TargetComponent>(new UpdatePathJob() {
                world = context.world,
                filter = this.filter,
            });
            
            context.SetDependency(dependsOn);
            
        }

    }

}