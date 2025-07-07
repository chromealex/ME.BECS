namespace ME.BECS.FogOfWar {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Players;
    using Transforms;
    using Unity.Jobs;

    [BURST]
    [RequiredDependencies(typeof(CreateSystem))]
    public struct UpdateSystem : IUpdate {

        [BURST]
        public struct RevealRectJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;

                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRect(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY);

            }

        }

        [BURST]
        public struct RevealRangeJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY);
                
            }

        }

        [BURST]
        public struct RevealRectPartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;

                var parentValue = parent.value;
                ref readonly var revealer = ref parentValue.Read<FogOfWarRevealerComponent>();
                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRect(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part);
                
            }

        }

        [BURST]
        public struct RevealRangePartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var parentValue = parent.value;
                ref readonly var revealer = ref parentValue.Read<FogOfWarRevealerComponent>();
                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part);

            }

        }

        [BURST]
        public struct RevealRangeSectorJob : IJobForComponents<FogOfWarRevealerComponent, OwnerComponent, FogOfWarSectorRevealerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref FogOfWarRevealerComponent revealer, ref OwnerComponent owner, ref FogOfWarSectorRevealerComponent sectorComponent) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsCalculated == false) return;
                
                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                var sector = new FowMathSector(tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), sectorComponent.value);
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, in sector);
                
            }

        }

        [BURST]
        public struct RevealRangeSectorPartialJob : IJobForComponents<ParentComponent, FogOfWarRevealerPartialComponent, OwnerComponent> {

            public FogOfWarStaticComponent props;
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref ParentComponent parent, ref FogOfWarRevealerPartialComponent part, ref OwnerComponent owner) {

                var tr = ent.GetAspect<TransformAspect>();
                if (tr.IsWorldMatrixTickCalculated == false) return;
                var parentValue = parent.value;
                ref readonly var revealer = ref parentValue.Read<FogOfWarRevealerComponent>();
                var ownerEnt = owner.ent;
                var fow = ownerEnt.GetAspect<PlayerAspect>().readTeam.Read<FogOfWarComponent>();
                var sector = new FowMathSector(tr.GetWorldMatrixPosition(), tr.GetWorldMatrixRotation(), parentValue.Read<FogOfWarSectorRevealerComponent>().value);
                var marker = new Unity.Profiling.ProfilerMarker("WriteRange");
                marker.Begin();
                FogOfWarUtils.WriteRange(in this.props, in fow, in tr, revealer.height, revealer.range, revealer.rangeY, part.part, in sector);
                marker.End();

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