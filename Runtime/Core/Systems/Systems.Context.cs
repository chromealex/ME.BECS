#if FIXED_POINT
using tfloat = sfloat;
#else
using tfloat = System.Single;
#endif

namespace ME.BECS {

    using Unity.Jobs;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;

    public static class SystemContextExt {

        public static JobHandle AddDependency(this in JobHandle jobHandle, ref SystemContext context) {
            context.AddDependency(in jobHandle);
            return context.dependsOn;
        }

    }
    
    public struct SystemContext {

        public readonly tfloat deltaTime => (tfloat)this.deltaTimeMs / (tfloat)1000f;
        public readonly uint deltaTimeMs;
        public readonly World world;
        public JobHandle dependsOn { get; private set; }

        public JobInfo jobInfo => JobInfo.Create(this.world.id);

        [INLINE(256)]
        private SystemContext(uint deltaTimeMs, in World world, JobHandle dependsOn) {
            this.deltaTimeMs = deltaTimeMs;
            this.world = world;
            this.dependsOn = dependsOn;
        }
        
        [INLINE(256)]
        public static SystemContext Create(uint deltaTimeMs, in World world, JobHandle dependsOn) {
            return new SystemContext(deltaTimeMs, in world, dependsOn);
        }

        [INLINE(256)]
        public static SystemContext Create(in World world, JobHandle dependsOn) {
            return new SystemContext(0u, in world, dependsOn);
        }

        [INLINE(256)]
        public void SetDependency(JobHandle dependsOn) {
            this.dependsOn = dependsOn;
        }

        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2) {
            this.dependsOn = JobHandle.CombineDependencies(handle1, handle2);
        }

        [INLINE(256)]
        public void SetDependency(JobHandle handle1, JobHandle handle2, JobHandle handle3) {
            this.dependsOn = JobHandle.CombineDependencies(handle1, handle2, handle3);
        }

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

        [INLINE(256)]
        public void AddDependency(in JobHandle handle) {
            this.dependsOn = JobHandle.CombineDependencies(this.dependsOn, handle);
        }

    }
    
}