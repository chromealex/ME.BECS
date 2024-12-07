namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;

    [BURST(CompileSynchronously = true)]
    [UnityEngine.Tooltip("Reset Can Fire system")]
    [RequiredDependencies(typeof(StopWhileAttackSystem))]
    public struct ResetCanFireSystem : IUpdate {

        [BURST(CompileSynchronously = true)]
        public struct Job : IJobForAspects<AttackAspect> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect) {

                aspect.CanFire = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().With<IsUnitStaticComponent>().Without<CanFireWhileMovesTag>().Schedule<Job, AttackAspect>();
            context.SetDependency(dependsOn);

        }

    }

}