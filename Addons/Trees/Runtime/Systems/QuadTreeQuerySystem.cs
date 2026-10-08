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

    /// <summary>
    /// Groups quad tree components for change tracking and queries.
    /// </summary>
    public struct QuadTreeComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for quad tree query.
    /// </summary>
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
    
    /// <summary>
    /// Stores per-entity state for quad tree query has custom filter tag.
    /// </summary>
    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeQueryHasCustomFilterTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for quad tree result.
    /// </summary>
    [ComponentGroup(typeof(QuadTreeComponentGroup))]
    public struct QuadTreeResult : IComponent {

        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public QueryResults results;

    }

    /// <summary>
    /// Provides typed access to the entity components used for quad tree query.
    /// </summary>
    [EditorComment("Filter all entities which suitable for this query")]
    public partial struct QuadTreeQueryAspect : IAspect {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for query.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<QuadTreeQuery> queryPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for result.
        /// </summary>
        public AspectDataPtr<QuadTreeResult> resultPtr;

        /// <summary>
        /// Query describing the candidates to process.
        /// </summary>
        public readonly ref QuadTreeQuery query => ref this.queryPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public readonly ref QuadTreeResult results => ref this.resultPtr.Get(this.ent.id, this.ent.gen);

        /// <summary>
        /// Read-only access to query.
        /// </summary>
        public readonly ref readonly QuadTreeQuery readQuery => ref this.queryPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to results.
        /// </summary>
        public readonly ref readonly QuadTreeResult readResults => ref this.resultPtr.Read(this.ent.id, this.ent.gen);

    }
    
    /// <summary>
    /// Coordinates quad tree query during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(QuadTreeInsertSystem))]
    public partial struct QuadTreeQuerySystem : IUpdate {

        /// <summary>
        /// Executes quad tree query system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<QuadTreeQueryAspect, TransformAspect> {

            /// <summary>
            /// System instance used by the associated operation.
            /// </summary>
            public QuadTreeInsertSystem system;

            /// <summary>
            /// Processes the job inputs for <c>QuadTreeQuerySystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref QuadTreeQueryAspect query, ref TransformAspect tr) {

                this.system.FillNearest(ref query, in tr, new AlwaysTrueSubFilter());
                
            }

        }
        
        /// <summary>
        /// Updates quad tree query system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<QuadTreeInsertSystem>();
            var handle = context.Query().Without<QuadTreeQueryHasCustomFilterTag>().AsParallel().Schedule<Job, QuadTreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}
