namespace ME.BECS {
    
    using static Cuts;
    using System.Diagnostics;
    
    /// <summary>
    /// Provides lifecycle integration for the journal feature.
    /// </summary>
    [UnityEngine.CreateAssetMenu(menuName = "ME.BECS/Journal Module")]
    public unsafe class JournalModule : Module {

        /// <summary>
        /// Configuration values used by this operation.
        /// </summary>
        public JournalProperties properties = JournalProperties.Default;
        private uint worldId;

        /// <summary>
        /// Initializes journal module state from the supplied context.
        /// </summary>
        public override void OnAwake(ref World world) {

            this.worldId = world.id;
            CreateJournal(in world, in this.properties);

        }

        [Conditional(JournalConditionals.JOURNAL)]
        private static void CreateJournal(in World world, in JournalProperties properties) {
            
            var journal = _make(Journal.Create(in world, in properties));
            JournalsStorage.Set(world.id, journal);
            
        }

        /// <summary>
        /// Starts journal module processing for the supplied context.
        /// </summary>
        public override Unity.Jobs.JobHandle OnStart(ref World world, Unity.Jobs.JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Updates journal module using the current inputs and execution context.
        /// </summary>
        public override Unity.Jobs.JobHandle OnUpdate(Unity.Jobs.JobHandle dependsOn) {
            return dependsOn;
        }

        /// <summary>
        /// Releases journal module state at the end of its owning lifecycle.
        /// </summary>
        public override void DoDestroy() {

            JournalsStorage.Dispose(this.worldId);
            
        }

    }

}
