using ME.BECS.Transforms;

namespace ME.BECS.Perks {
    
    using ME.BECS;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using BURST = Unity.Burst.BurstCompileAttribute;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Coordinates perk initialize during the ECS system lifecycle.
    /// </summary>
    [BURST]
    public partial struct PerkInitializeSystem<T> : IUpdate where T : unmanaged, IPerkInitializeComponent {

        /// <summary>
        /// Executes perk initialize system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            /// <summary>
            /// Processes the job inputs for <c>PerkInitializeSystem</c>.
            /// </summary>
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                ent.Remove<IsPerkInitializeRequired>();
                perk.OnInitialize(in jobInfo, perkAspect.readOwner.GetAspect<PlayerAspect>(), in perkAspect);
            }
        }
        
        /// <summary>
        /// Updates perk initialize system using the current inputs and execution context.
        /// </summary>
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().With<IsPerkInitializeRequired>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

    /// <summary>
    /// Coordinates perk update parallel during the ECS system lifecycle.
    /// </summary>
    [BURST]
    [SystemGenericParallelMode]
    public partial struct PerkUpdateParallelSystem<T> : IUpdate where T : unmanaged, IPerkParallelComponent {

        /// <summary>
        /// Executes perk update parallel system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public uint dt;
            /// <summary>
            /// Processes the job inputs for <c>PerkUpdateParallelSystem</c>.
            /// </summary>
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                var unit = ent.ReadParent();
                perk.Run(in jobInfo, in perkAspect, in unit, this.dt);
            }
        }
        
        /// <summary>
        /// Updates perk update parallel system using the current inputs and execution context.
        /// </summary>
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().AsParallel().Without<IsPerkInitializeRequired>().Without<IsPerkUsedComponent>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

    /// <summary>
    /// Coordinates perk update during the ECS system lifecycle.
    /// </summary>
    [BURST]
    public partial struct PerkUpdateSystem<T> : IUpdate, IGenericWithout<IPerkParallelComponent> where T : unmanaged, IPerkComponent {

        /// <summary>
        /// Executes perk update system work through the job scheduler.
        /// </summary>
        [BURST]
        public partial struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            /// <summary>
            /// Time step supplied to this update.
            /// </summary>
            [InjectDeltaTime]
            public uint dt;
            /// <summary>
            /// Processes the job inputs for <c>PerkUpdateSystem</c>.
            /// </summary>
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                var unit = ent.ReadParent();
                perk.Run(in jobInfo, in perkAspect, in unit, this.dt);
            }
        }
        
        /// <summary>
        /// Updates perk update system using the current inputs and execution context.
        /// </summary>
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().Without<IsPerkInitializeRequired>().Without<IsPerkUsedComponent>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

}