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

    /// <summary>
    /// Coordinates build path during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [UnityEngine.Tooltip("Schedule building a path.")]
    [RequiredDependencies(typeof(BuildGraphSystem))]
    public partial struct BuildPathSystem : IUpdate {

        /// <summary>
        /// Executes update path work through the job scheduler.
        /// </summary>
        [BURST]
        public unsafe partial struct UpdatePathJob : IJobForComponents<TargetComponent> {

            /// <summary>
            /// World used by the containing operation.
            /// </summary>
            public World world;
            /// <summary>
            /// Filter restricting the entries considered by this operation.
            /// </summary>
            public Filter filter;
            
            /// <summary>
            /// Processes update path using the supplied job inputs.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref TargetComponent targetData) {
                
                if (targetData.target.IsAlive() == false) return;

                var targetInfo = targetData.target.Read<TargetInfoComponent>();
                Path path;
                MemArrayAuto<byte> chunksToUpdate;
                if (ent.TryRead(out TargetPathComponent targetPathComponent) == true) {
                    // update path
                    path = targetPathComponent.path;
                    Graph.SetTarget(ref path, in targetInfo.target, in this.filter);
                    // use target chunks which must be updated in path follow system
                    chunksToUpdate = targetPathComponent.chunksToUpdate;
                    var updateRequired = new NativeReference<byte>(1, Allocator.Temp);
                    Graph.PathUpdateSync(in this.world, ref path, in path.graph, chunksToUpdate, path.filter, updateRequired);
                    var arr = targetPathComponent.chunksToUpdate;
                    _memclear(arr.GetUnsafePtr(), arr.Length * TSize<byte>.size);
                } else {
                    Graph.MakePath(in this.world, out path, in targetData.graphEnt, in targetInfo.target, this.filter);
                    chunksToUpdate = new MemArrayAuto<byte>(targetData.target, path.chunks.Length);
                }
                ent.Set(new TargetPathComponent() {
                    path = path,
                    chunksToUpdate = chunksToUpdate,
                });
                
            }

        }

        /// <summary>
        /// Filter restricting the entries considered by this operation.
        /// </summary>
        public Filter filter;
        
        /// <summary>
        /// Updates build path system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().With<TargetComponent>().AsParallel().Schedule<UpdatePathJob, TargetComponent>(new UpdatePathJob() {
                world = context.world,
                filter = this.filter,
            });
            
            context.SetDependency(dependsOn);
            
        }

    }

}