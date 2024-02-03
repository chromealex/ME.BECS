
namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using Unity.Mathematics;
    using Unity.Collections.LowLevel.Unsafe;
    using static Cuts;
    using ME.BECS.Transforms;

    public struct QuadTreeQuery : IComponent {

        /// <summary>
        /// Trees mask
        /// </summary>
        /* Ex: 1 << 0 - select first tree */
        public int treeMask;
        /// <summary>
        /// Range to select
        /// </summary>
        public float range;
        /// <summary>
        /// Reset pos.y to zero
        /// </summary>
        public bool ignoreY;
        /// <summary>
        /// Select X units for each tree
        /// </summary>
        public uint nearestCount;
        
    }

    public struct QuadTreeResult : IComponent {

        public ListAuto<Ent> results;

    }

    public struct QuadTreeQueryAspect : IAspect {

        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeQuery> queryPtr;
        public AspectDataPtr<QuadTreeResult> resultPtr;

        public ref QuadTreeQuery query => ref this.queryPtr.value.Get(this.ent.id, this.ent.gen);

        public ref QuadTreeResult results => ref this.resultPtr.value.Get(this.ent.id, this.ent.gen);
        
    }
    
    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(QuadTreeInsertSystem))]
    public unsafe struct QuadTreeQuerySystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobParallelForAspect<QuadTreeQueryAspect, TransformAspect> {

            public QuadTreeInsertSystem system;

            public void Execute(ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                var data = query.query;
                var worldPos = tr.GetWorldMatrixPosition();
                if (data.ignoreY == true) worldPos.y = 0f;
                
                // clean up results
                if (query.results.results.isCreated == true) query.results.results.Clear();
                
                // for each tree
                for (int i = 0; i < this.system.treesCount; ++i) {

                    if ((query.query.treeMask & (1 << i)) == 0) {
                        continue;
                    }
                    
                    ref var tree = ref *this.system.GetTree(i);
                    
                    if (data.nearestCount > 0) {

                        var results = new Unity.Collections.NativeArray<Ent>((int)data.nearestCount, Unity.Collections.Allocator.Temp);
                        var cnt = tree.QueryKNearest(worldPos, query.query.range, new Unity.Collections.NativeSlice<Ent>(results));
                        if (query.results.results.isCreated == false) query.results.results = new ListAuto<Ent>(query.ent, (uint)results.Length);
                        query.results.results.AddRange(in results, 0, cnt);
                        
                    } else {

                        var results = new UnsafeList<Ent>(tree.Points.Length, Unity.Collections.Allocator.Temp);
                        tree.QueryRange(worldPos, data.range, ref results);
                        //UnityEngine.Debug.Log(i + " :: " + query.ent);
                        if (query.results.results.isCreated == false) query.results.results = new ListAuto<Ent>(query.ent, (uint)results.Length);
                        query.results.results.AddRange(in results);
                        
                    }

                }
                
            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var handle = API.Query(in context).ScheduleParallelFor<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}