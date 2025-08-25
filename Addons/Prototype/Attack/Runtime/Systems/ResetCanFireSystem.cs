namespace ME.BECS.Attack {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Units;
    using ME.BECS.Transforms;

    [BURST]
    [UnityEngine.Tooltip("Reset Can Fire system")]
    public struct ResetCanFireSystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<AttackAspect, TransformAspect> {
            
            public void Execute(in JobInfo jobInfo, in Ent ent, ref AttackAspect aspect, ref TransformAspect tr) {

                if (tr.parent.Has<IsUnitStaticComponent>() == false) return;
                if (aspect.HasAnyTarget == false) aspect.CanFire = false;

            }

        }

        public void OnUpdate(ref SystemContext context) {

            var dependsOn = context.Query().AsParallel().Without<CanFireWhileMovesTag>().Schedule<Job, AttackAspect, TransformAspect>();
            context.SetDependency(dependsOn);

        }

    }

}