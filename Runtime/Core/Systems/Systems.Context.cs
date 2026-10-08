#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS {

    using Unity.Jobs;
    #if INLINE_DISABLED
    using INLINE = ME.BECS.NoInline;
    #else
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    #endif

    /// <summary>
    /// Provides helper operations for system context.
    /// </summary>
    public static class SystemContextExt {

        /// <summary>
        /// Adds the supplied job handle to the existing dependency chain.
        /// </summary>
        public static JobHandle AddDependency(this in JobHandle jobHandle, ref SystemContext context) {
            context.AddDependency(in jobHandle);
            return context.dependsOn;
        }

    }
    
    /// <summary>
    /// Carries the world, update timing and job dependency chain for a system callback.
    /// </summary>
    public struct SystemContext {

        /// <summary>
        /// Elapsed simulation time supplied to this update.
        /// </summary>
        public readonly tfloat deltaTime => (tfloat)this.deltaTimeMs / (tfloat)1000f;
        /// <summary>
        /// Elapsed simulation time in milliseconds.
        /// </summary>
        public readonly uint deltaTimeMs;
        /// <summary>
        /// World used by the containing operation.
        /// </summary>
        public readonly World world;
        /// <summary>
        /// Job dependency that must complete before the associated work can access its inputs.
        /// </summary>
        public JobHandle dependsOn { get; private set; }

        /// <summary>
        /// World and execution context supplied to the job.
        /// </summary>
        public JobInfo jobInfo => JobInfo.Create(this.world.id);

        [INLINE(256)]
        private SystemContext(uint deltaTimeMs, in World world, JobHandle dependsOn) {
            this.deltaTimeMs = deltaTimeMs;
            this.world = world;
            this.dependsOn = dependsOn;
        }
        
        /// <summary>
        /// Creates <c>SystemContext</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static SystemContext Create(uint deltaTimeMs, in World world, JobHandle dependsOn) {
            return new SystemContext(deltaTimeMs, in world, dependsOn);
        }

        /// <summary>
        /// Creates <c>SystemContext</c> using the supplied creation arguments.
        /// </summary>
        [INLINE(256)]
        public static SystemContext Create(in World world, JobHandle dependsOn) {
            return new SystemContext(0u, in world, dependsOn);
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle dependsOn) {
            this.dependsOn = dependsOn;
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2) {
            this.dependsOn = JobHandle.CombineDependencies(handle1, handle2);
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2, JobHandle handle3) {
            this.dependsOn = JobHandle.CombineDependencies(handle1, handle2, handle3);
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4) {
            var list = new Unity.Collections.NativeArray<JobHandle>(4, Constants.ALLOCATOR_TEMP);
            list[0] = handle1;
            list[1] = handle2;
            list[2] = handle3;
            list[3] = handle4;
            this.dependsOn = JobHandle.CombineDependencies(list);
            list.Dispose();
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5) {
            var list = new Unity.Collections.NativeArray<JobHandle>(5, Constants.ALLOCATOR_TEMP);
            list[0] = handle1;
            list[1] = handle2;
            list[2] = handle3;
            list[3] = handle4;
            list[4] = handle5;
            this.dependsOn = JobHandle.CombineDependencies(list);
            list.Dispose();
        }

        /// <summary>
        /// Sets dependency.
        /// </summary>
        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6) {
            var list = new Unity.Collections.NativeArray<JobHandle>(6, Constants.ALLOCATOR_TEMP);
            list[0] = handle1;
            list[1] = handle2;
            list[2] = handle3;
            list[3] = handle4;
            list[4] = handle5;
            list[5] = handle6;
            this.dependsOn = JobHandle.CombineDependencies(list);
            list.Dispose();
        }

        /// <summary>
        /// Adds the supplied job handle to the existing dependency chain.
        /// </summary>
        [INLINE(256)]
        public void AddDependency(in JobHandle handle) {
            this.dependsOn = JobHandle.CombineDependencies(this.dependsOn, handle);
        }

    }
    
}