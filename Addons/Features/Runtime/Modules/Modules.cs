namespace ME.BECS {
    
    using Unity.Jobs;

    /// <summary>
    /// Provides lifecycle integration for the  feature.
    /// </summary>
    public abstract class Module : UnityEngine.ScriptableObject {

        /// <summary>
        /// World properties used by <c>Module</c>.
        /// </summary>
        protected internal WorldProperties worldProperties;

        /// <summary>
        /// Stores the world properties used by the module lifecycle.
        /// </summary>
        public void Setup(in WorldProperties properties) {
            this.worldProperties = properties;
        }
        
        /// <summary>
        /// Initializes module state from the supplied context.
        /// </summary>
        public abstract void OnAwake(ref World world);
        /// <summary>
        /// Starts module processing for the supplied context.
        /// </summary>
        public abstract JobHandle OnStart(ref World world, JobHandle dependsOn);
        /// <summary>
        /// Updates module using the current inputs and execution context.
        /// </summary>
        public abstract JobHandle OnUpdate(JobHandle dependsOn);
        /// <summary>
        /// Releases module state at the end of its owning lifecycle.
        /// </summary>
        public abstract void DoDestroy();

    }

}