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

    public struct QuadTreeComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }
    
    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeQuery : IConfigComponent {

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
        /// <summary>
        /// Sometimes you need to use parent rotation instead of sensor rotation
        /// Be sure you have parent of this object
        /// </summary>
        public bbool useParentRotation;

    }
    
    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeQueryHasCustomFilterTag : IComponent {}

    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeResult : IComponent {

        public ListAuto<Ent> results;

    }

    [EditorComment("Filter all entities which suitable for this query")]
    public struct QuadTreeQueryAspect : IAspect {

        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<QuadTreeQuery> queryPtr;
        public AspectDataPtr<QuadTreeResult> resultPtr;

        public readonly ref QuadTreeQuery query => ref this.queryPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref QuadTreeResult results => ref this.resultPtr.Get(this.ent.id, this.ent.gen);

        public readonly ref readonly QuadTreeQuery readQuery => ref this.queryPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly QuadTreeResult readResults => ref this.resultPtr.Read(this.ent.id, this.ent.gen);

    }
    
    [BURST]
    [RequiredDependencies(typeof(QuadTreeInsertSystem))]
    public struct QuadTreeQuerySystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<QuadTreeQueryAspect, TransformAspect> {

            public QuadTreeInsertSystem system;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                this.system.FillNearest(ref query, in tr, new AlwaysTrueSubFilter());
                
            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var handle = context.Query().Without<QuadTreeQueryHasCustomFilterTag>().AsParallel().Schedule<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}
