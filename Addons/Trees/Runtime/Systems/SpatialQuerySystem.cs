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

    public struct SpatialComponentGroup {

        public static UnityEngine.Color color = UnityEngine.Color.yellow;

    }
    
    [ComponentGroup(typeof(SpatialComponentGroup))]
    public struct SpatialQuery : IConfigComponent {

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
    
    [ComponentGroup(typeof(SpatialComponentGroup))]
    public struct SpatialQueryHasCustomFilterTag : IComponent {}

    [ComponentGroup(typeof(SpatialComponentGroup))]
    public struct SpatialResult : IComponent {

        public ListAuto<Ent> results;

    }

    [EditorComment("Filter all entities which suitable for this query")]
    public struct SpatialQueryAspect : IAspect {

        public Ent ent { get; set; }

        [QueryWith]
        public AspectDataPtr<SpatialQuery> queryPtr;
        public AspectDataPtr<SpatialResult> resultPtr;

        public readonly ref SpatialQuery query => ref this.queryPtr.Get(this.ent.id, this.ent.gen);
        public readonly ref SpatialResult results => ref this.resultPtr.Get(this.ent.id, this.ent.gen);

        public readonly ref readonly SpatialQuery readQuery => ref this.queryPtr.Read(this.ent.id, this.ent.gen);
        public readonly ref readonly SpatialResult readResults => ref this.resultPtr.Read(this.ent.id, this.ent.gen);

    }
    
    [BURST]
    [RequiredDependencies(typeof(SpatialInsertSystem))]
    public struct SpatialQuerySystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<SpatialQueryAspect, TransformAspect> {

            public SpatialInsertSystem system;

            public void Execute(in JobInfo jobInfo, in Ent ent, ref SpatialQueryAspect query, ref TransformAspect tr) {

                this.system.FillNearest(ref query, in tr, new AlwaysTrueSpatialSubFilter());
                
            }

        }
        
        public void OnUpdate(ref SystemContext context) {

            var querySystem = context.world.GetSystem<SpatialInsertSystem>();
            var handle = context.Query().Without<SpatialQueryHasCustomFilterTag>().AsParallel().Schedule<Job, SpatialQueryAspect, TransformAspect>(new Job() {
                system = querySystem,
            });
            context.SetDependency(handle);

        }

    }

}
