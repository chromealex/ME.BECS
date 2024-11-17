namespace ME.BECS {

    using static CutsPool;
    using Unity.Jobs;
    using Unity.Collections.LowLevel.Unsafe;
    using INLINE = System.Runtime.CompilerServices.MethodImplAttribute;
    using BURST = Unity.Burst.BurstCompileAttribute;

    public partial struct Query {
        
        [INLINE(256)]
        public static QueryBuilderStatic WithAll<T0, T1>(in SystemContext systemContext) where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            return Query.WithAll<T0, T1>(in systemContext.world, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic WithAll<T0, T1>(in World world, JobHandle dependsOn) where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            return new QueryBuilderStatic(in world, dependsOn).WithAll<T0, T1>();
        }

        [INLINE(256)]
        public static QueryBuilderStatic WithAny<T0, T1>(in SystemContext systemContext) where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            return Query.WithAny<T0, T1>(in systemContext.world, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic WithAny<T0, T1>(in World world, JobHandle dependsOn) where T0 : unmanaged, IComponentBase where T1 : unmanaged, IComponentBase {
            return new QueryBuilderStatic(in world, dependsOn).WithAny<T0, T1>();
        }

        [INLINE(256)]
        public static QueryBuilderStatic With<T>(in SystemContext systemContext) where T : unmanaged, IComponentBase {
            return Query.With<T>(in systemContext.world, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic With<T>(in World world, JobHandle dependsOn) where T : unmanaged, IComponentBase {
            return new QueryBuilderStatic(in world, dependsOn).With<T>();
        }

        [INLINE(256)]
        public static QueryBuilderStatic Without<T>(in SystemContext systemContext) where T : unmanaged, IComponentBase {
            return Query.Without<T>(in systemContext.world, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic Without<T>(in World world, JobHandle dependsOn) where T : unmanaged, IComponentBase {
            return new QueryBuilderStatic(in world, dependsOn).Without<T>();
        }

        /*
        [INLINE(256)]
        public static QueryBuilderStatic WithAspect<T>(in SystemContext systemContext) where T : unmanaged, IAspect {
            return Query.WithAspect<T>(in systemContext.world, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic WithAspect<T>(in World world, JobHandle dependsOn) where T : unmanaged, IAspect {
            return new QueryBuilderStatic(in world, dependsOn).WithAspect<T>();
        }
        */

        [INLINE(256)]
        public static QueryBuilderStatic Step(in SystemContext systemContext, uint steps, uint minElementsPerStep) {
            return Query.Step(systemContext.world, steps, minElementsPerStep, systemContext.dependsOn);
        }

        [INLINE(256)]
        public static QueryBuilderStatic Step(in World world, uint steps, uint minElementsPerStep, JobHandle dependsOn) {
            return new QueryBuilderStatic(in world, dependsOn).Step(steps, minElementsPerStep);
        }

    }

}