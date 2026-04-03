
namespace ME.BECS.Units {
    
    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;
    using ME.BECS.Transforms;
    using ME.BECS.Effects;

    [BURST]
    [UnityEngine.Tooltip("Unit spawn effect system")]
    public struct SpawnSystem : IUpdate {

        [BURST]
        public struct Job : IJobForAspects<TransformAspect, UnitAspect> {

            public void Execute(in JobInfo jobInfo, in Ent ent, ref TransformAspect tr, ref UnitAspect unit) {
                EffectUtils.CreateEffect(in jobInfo, tr.position, tr.rotation, unit.ent.ReadStatic<UnitEffectOnSpawnComponent>().effect, unit.readOwner.GetAspect<ME.BECS.Players.PlayerAspect>());
            }

        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().With<UnitJustSpawnedEvent>().Schedule<Job, TransformAspect, UnitAspect>().AddDependency(ref context);

        }

    }

}