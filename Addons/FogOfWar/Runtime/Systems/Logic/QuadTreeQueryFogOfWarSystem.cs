namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    /// <summary>
    /// Coordinates quad tree query fog of war during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(CreateSystem), typeof(QuadTreeInsertSystem))]
    public partial struct QuadTreeQueryFogOfWarSystem : IUpdate {

        /// <summary>
        /// Executes quad tree query fog of war system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobFor2Aspects1Components<QuadTreeQueryAspect, TransformAspect, QuadTreeQueryFogOfWarFilter> {

            /// <summary>
            /// System instance used by the associated operation.
            /// </summary>
            public QuadTreeInsertSystem system;
            /// <summary>
            /// Fog-of-war state used by this operation.
            /// </summary>
            public CreateSystem fow;

            /// <summary>
            /// Processes the job inputs for <c>QuadTreeQueryFogOfWarSystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref QuadTreeQueryAspect query, ref TransformAspect tr, ref QuadTreeQueryFogOfWarFilter filter) {

                var subFilter = filter.data;
                subFilter.fow = this.fow;
                this.system.FillNearest(ref query, in tr, in subFilter);
                
            }

        }
        
        /// <summary>
        /// Updates quad tree query fog of war system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var fow = context.world.GetSystem<CreateSystem>();
            var handle = context.Query().AsParallel().Schedule<Job, QuadTreeQueryAspect, TransformAspect, QuadTreeQueryFogOfWarFilter>(new Job() {
                system = querySystem,
                fow = fow,
            });
            context.SetDependency(handle);

        }

    }

}