namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    public struct QuadTreeQueryFogOfWarFilter : IComponent {

        public FogOfWarSubFilter data;

    }
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateSystem), typeof(QuadTreeInsertSystem))]
    public struct QuadTreeQueryFogOfWarSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<QuadTreeQueryAspect, TransformAspect> {

            public QuadTreeInsertSystem system;
            public CreateSystem fow;

            public void Execute(in JobInfo jobInfo, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                var subFilter = query.ent.Read<QuadTreeQueryFogOfWarFilter>().data;
                subFilter.fow = this.fow;
                this.system.FillNearest(ref query, in tr, in subFilter);
                
            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var fow = context.world.GetSystem<CreateSystem>();
            var handle = context.Query().With<QuadTreeQueryFogOfWarFilter>().Schedule<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
                fow = fow,
            });
            context.SetDependency(handle);

        }

    }

}