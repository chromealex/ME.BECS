namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using Transforms;
    using Unity.Jobs;

    [BURST(CompileSynchronously = true)]
    [RequiredDependencies(typeof(CreateSystem))]
    public struct UpdateSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct RevealRectJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRect(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RevealRangeJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY);
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RevealRectPartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                ref readonly var revealer = ref parent.value.Read<FogOfWarRevealerComponent>();
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRect(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part);
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RevealRangePartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                ref readonly var revealer = ref parent.value.Read<FogOfWarRevealerComponent>();
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part);

            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RevealRangeSectorJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent, FogOfWarSectorRevealerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner, ref FogOfWarSectorRevealerComponent sectorComponent) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                var sector = new FowMathSector(in this.props, tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), sectorComponent.value);
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, in sector);
                
            }

        }

        [BURST(CompileSynchronously = true)]
        public struct RevealRangeSectorPartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                ref readonly var revealer = ref parent.value.Read<FogOfWarRevealerComponent>();
                var fow = owner.ent.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                var sector = new FowMathSector(in this.props, tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), parent.value.Read<FogOfWarSectorRevealerComponent>().value);
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part, in sector);

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var fowStaticData = context.world.GetSystem<CreateSystem>().heights.Read<FogOfWarStaticComponent>();
            var dependsOnRectFull = context.Query().AsParallel().With<FogOfWarRevealerIsRectTag>().Without<FogOfWarRevealerIsPartialTag>().WithAspect<TransformAspect>().Schedule<RevealRectJob, FogOfWarRevealerComponent, OwnerComponent>(new RevealRectJob() {
                props = fowStaticData,
            });
            var dependsOnRangeFull = context.Query().AsParallel().Without<FogOfWarRevealerIsSectorTag>().With<FogOfWarRevealerIsRangeTag>().Without<FogOfWarRevealerIsPartialTag>().WithAspect<TransformAspect>().Schedule<RevealRangeJob, FogOfWarRevealerComponent, OwnerComponent>(new RevealRangeJob() {
                props = fowStaticData,
            });
            var dependsOnRangeFullSector = context.Query().AsParallel().With<FogOfWarRevealerIsSectorTag>().With<FogOfWarRevealerIsRangeTag>().Without<FogOfWarRevealerIsPartialTag>().WithAspect<TransformAspect>().Schedule<RevealRangeSectorJob, FogOfWarRevealerComponent, OwnerComponent, FogOfWarSectorRevealerComponent>(new RevealRangeSectorJob() {
                props = fowStaticData,
            });
            var dependsOnRectPartial = context.Query().AsParallel().With<FogOfWarRevealerIsRectTag>().WithAspect<TransformAspect>().Schedule<RevealRectPartialJob, ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent>(new RevealRectPartialJob() {
                props = fowStaticData,
            });
            var dependsOnRangePartial = context.Query().AsParallel().Without<FogOfWarRevealerIsSectorTag>().With<FogOfWarRevealerIsRangeTag>().WithAspect<TransformAspect>().Schedule<RevealRangePartialJob, ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent>(new RevealRangePartialJob() {
                props = fowStaticData,
            });
            var dependsOnRangePartialSector = context.Query().AsParallel().With<FogOfWarRevealerIsSectorTag>().With<FogOfWarRevealerIsRangeTag>().WithAspect<TransformAspect>().Schedule<RevealRangeSectorPartialJob, ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent>(new RevealRangeSectorPartialJob() {
                props = fowStaticData,
            });
            context.SetDependency(JobHandle.CombineDependencies(JobHandle.CombineDependencies(dependsOnRectFull, dependsOnRangeFull), JobHandle.CombineDependencies(dependsOnRectPartial, dependsOnRangePartial), JobHandle.CombineDependencies(dependsOnRangeFullSector, dependsOnRangePartialSector)));

        }

    }

}