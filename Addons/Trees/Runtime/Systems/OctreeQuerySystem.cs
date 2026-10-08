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
    /// Groups octree components for change tracking and queries.
    /// </summary>
    public struct OctreeComponentGroup {

        /// <summary>
        /// Color used to render or identify this value.
        /// </summary>
        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }
    
    /// <summary>
    /// Defines configuration-backed entity data for octree query.
    /// </summary>
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
    
    /// <summary>
    /// Stores per-entity state for octree query has custom filter tag.
    /// </summary>
    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeQueryHasCustomFilterTag : IComponent {}

    /// <summary>
    /// Stores per-entity state for octree result.
    /// </summary>
    [ComponentGroup(typeof(OctreeComponentGroup))]
    public struct OctreeResult : IComponent {

        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public QueryResults results;

    }

    /// <summary>
    /// Provides typed access to the entity components used for octree query.
    /// </summary>
    [EditorComment("Filter all entities which suitable for this query")]
    public partial struct OctreeQueryAspect : IAspect {

        /// <summary>
        /// Entity whose components or lifetime are associated with this value.
        /// </summary>
        public Ent ent { get; set; }

        /// <summary>
        /// Native pointer or typed storage accessor for query.
        /// </summary>
        [QueryWith]
        public AspectDataPtr<OctreeQuery> queryPtr;
        /// <summary>
        /// Native pointer or typed storage accessor for result.
        /// </summary>
        public AspectDataPtr<OctreeResult> resultPtr;

        /// <summary>
        /// Query describing the candidates to process.
        /// </summary>
        public readonly ref OctreeQuery query => ref this.queryPtr.Get(this.ent.id, this.ent.gen);
        /// <summary>
        /// Destination or stored results of the associated operation.
        /// </summary>
        public readonly ref OctreeResult results => ref this.resultPtr.Get(this.ent.id, this.ent.gen);

        /// <summary>
        /// Read-only access to query.
        /// </summary>
        public readonly ref readonly OctreeQuery readQuery => ref this.queryPtr.Read(this.ent.id, this.ent.gen);
        /// <summary>
        /// Read-only access to results.
        /// </summary>
        public readonly ref readonly OctreeResult readResults => ref this.resultPtr.Read(this.ent.id, this.ent.gen);

    }
    
    /// <summary>
    /// Coordinates octree query during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [RequiredDependencies(typeof(OctreeInsertSystem))]
    public partial struct OctreeQuerySystem : IUpdate {

        /// <summary>
        /// Executes octree query system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobForAspects<OctreeQueryAspect, TransformAspect> {

            /// <summary>
            /// System instance used by the associated operation.
            /// </summary>
            public OctreeInsertSystem system;

            /// <summary>
            /// Processes the job inputs for <c>OctreeQuerySystem</c>.
            /// </summary>
            public void Execute(in JobInfo jobInfo, in Ent ent, ref OctreeQueryAspect query, ref TransformAspect tr) {

                this.system.FillNearest(ref query, in tr, new AlwaysTrueOctreeSubFilter());
                
            }

        }
        
        /// <summary>
        /// Updates octree query system using the current inputs and execution context.
        /// </summary>
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<OctreeInsertSystem>();
            var handle = context.Query().Without<OctreeQueryHasCustomFilterTag>().AsParallel().Schedule<Job, OctreeQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}
