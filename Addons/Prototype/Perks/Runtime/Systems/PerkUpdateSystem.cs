using ME.BECS.Transforms;

namespace ME.BECS.Perks {
    
    using ME.BECS;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using BURST = Unity.Burst.BurstCompileAttribute;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    [BURST]
    public struct PerkInitializeSystem<T> : IUpdate where T : unmanaged, IPerkInitializeComponent {

        [BURST]
        public struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                ent.Remove<IsPerkInitializeRequired>();
                perk.OnInitialize(in jobInfo, perkAspect.readOwner.GetAspect<PlayerAspect>(), in perkAspect);
            }
        }
        
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().With<IsPerkInitializeRequired>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

    [BURST]
    [SystemGenericParallelMode]
    public struct PerkUpdateParallelSystem<T> : IUpdate where T : unmanaged, IPerkParallelComponent {

        [BURST]
        public struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            [InjectDeltaTime]
            public uint dt;
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                var unit = ent.ReadParent();
                perk.Run(in perkAspect, in unit, this.dt);
            }
        }
        
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().AsParallel().Without<IsPerkInitializeRequired>().Without<IsPerkUsedComponent>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

    [BURST]
    public struct PerkUpdateSystem<T> : IUpdate, IGenericWithout<IPerkParallelComponent> where T : unmanaged, IPerkComponent {

        [BURST]
        public struct Job : IJobFor1Aspects1Components<PerkAspect, T> {
            [InjectDeltaTime]
            public uint dt;
            [INLINE(256)]
            public void Execute(in JobInfo jobInfo, in Ent ent, ref PerkAspect perkAspect, ref T perk) {
                var unit = ent.ReadParent();
                perk.Run(in perkAspect, in unit, this.dt);
            }
        }
        
        [INLINE(256)]
        public void OnUpdate(ref SystemContext context) {
            context.Query().AsParallel().Without<IsPerkInitializeRequired>().Without<IsPerkUsedComponent>().Schedule<Job, PerkAspect, T>().AddDependency(ref context);
        }

    }

}