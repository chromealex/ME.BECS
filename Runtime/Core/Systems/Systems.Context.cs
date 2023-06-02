namespace ME.BECS {

    using Unity.Jobs;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    
    public struct SystemContext {

        public readonly float deltaTime;
        public readonly World world;
        public JobHandle dependsOn { get; private set; }

        [INLINE(256)]
        internal SystemContext(float deltaTime, in World world, JobHandle dependsOn) {
            this.deltaTime = deltaTime;
            this.world = world;
            this.dependsOn = dependsOn;
        }
        
        [INLINE(256)]
        public void SetDependency(JobHandle dependsOn) {
            this.dependsOn = dependsOn;
        }

        [INLINE(256)]
        public static SystemContext Create(float dt, in World world, JobHandle dependsOn) {
            return new SystemContext(dt, in world, dependsOn);
        }

        [INLINE(256)]
        public static SystemContext Create(in World world, JobHandle dependsOn) {
            return new SystemContext(0f, in world, dependsOn);
        }

    }
    
}