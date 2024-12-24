namespace ME.BECS {
    
    using static Cuts;
    using System.Diagnostics;
    
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Journal Module")]
    public unsafe class JournalModule : Module {

        public JournalProperties properties = JournalProperties.Default;
        private uint worldId;

        public override void OnAwake(ref World world) {

            this.worldId = world.id;
            CreateJournal(in world, in this.properties);

        }

        [Conditional(JournalConditionals.JOURNAL)]
        private static void CreateJournal(in World world, in JournalProperties properties) {
            
            var journal = _make(Journal.Create(in world, in properties));
            JournalsStorage.Set(world.id, journal);
            
        }

        public override Unity.Jobs.JobHandle OnStart(ref World world, Unity.Jobs.JobHandle dependsOn) {
            return dependsOn;
        }

        public override Unity.Jobs.JobHandle OnUpdate(Unity.Jobs.JobHandle dependsOn) {
            return dependsOn;
        }

        public override void DoDestroy() {

            var journal = JournalsStorage.Get(this.worldId);
            if (journal.ptr == null) return;
            journal.ptr->Dispose();
            
        }

    }

}
