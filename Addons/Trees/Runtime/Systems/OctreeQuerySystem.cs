#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
using Bounds = ME.BECS.FixedPoint.AABB;
using Rect = ME.BECS.FixedPoint.Rect;
#else
using tfloat = System.Single;
using Unity.Mathematics;
using Bounds = UnityEngine.Bounds;
using Rect = UnityEngine.Rect;
#endif

namespace ME.BECS {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;

    public struct OctreeComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }
    
    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeQuery : IConfigComponent {

        /// <summary>
        /// Trees mask
        /// </summary>
        /// <example>1 &lt;&lt; 0 - select first tree</example>
        public int treeMask;
        /// <summary>
        /// Range to select
        /// </summary>
        public tfloat rangeSqr;
        /// <summary>
        /// Min range to select
        /// </summary>
        public tfloat minRangeSqr;
        /// <summary>
        /// Sector angle in degrees (align to look rotation)
        /// </summary>
        public tfloat sector;
        /// <summary>
        /// Select X units for each tree
        /// </summary>
        public ushort nearestCount;
        /// <summary>
        /// Reset pos.y to zero
        /// </summary>
        public bbool ignoreY;
        /// <summary>
        /// Ignore self ent
        /// </summary>
        public bbool ignoreSelf;
        /// <summary>
        /// If set select will be a bit faster, but results will be unsorted
        /// </summary>
        public bbool ignoreSorting;
        /// <summary>
        /// Every n ticks query will be updated. 0 - update every tick.
        /// </summary>
        public byte updatePerTick;

    }
    
    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeQueryHasCustomFilterTag : IComponent {}

    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeResult : IComponent {

        public ListAuto<Ent> results;

    }

    [EditorComment("Filter all entities which suitable for this query")]
    public struct OctreeQueryAspect : IAspect {

        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<OctreeQuery> queryPtr;
        public AspectDataPtr<OctreeResult> resultPtr;

        public readonly ref OctreeQuery query => ref this.queryPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref OctreeResult results => ref this.resultPtr.Get(this.ent.id, this.ent.gen);

        public readonly ref readonly OctreeQuery readQuery => ref this.queryPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly OctreeResult readResults => ref this.resultPtr.Read(this.ent.id, this.ent.gen);

    }
    
    [BURST]
    [RequiredDependencies(typeof(OctreeInsertSystem))]
    public struct OctreeQuerySystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<OctreeQueryAspect, TransformAspect> {

            public OctreeInsertSystem system;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref OctreeQueryAspect query, ref TransformAspect tr) {

                this.system.FillNearest(ref query, in tr, new AlwaysTrueOctreeSubFilter());
                
            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<OctreeInsertSystem>();
            var handle = context.Query().Without<OctreeQueryHasCustomFilterTag>().AsParallel().Schedule<Job, OctreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}
