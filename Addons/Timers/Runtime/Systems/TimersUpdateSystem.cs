#if FIXED_POINT
using tfloat = sfloat;
using ME.BECS.FixedPoint;
#else
using tfloat = System.Single;
using Unity.Mathematics;
#endif

namespace ME.BECS.Timers {

    using BURST = Unity.Burst.BurstCompileAttribute;
    using ME.BECS.Jobs;

    [BURST]
    [SystemGenericParallelMode]
    public struct TimersUpdateSystem<T> : IUpdate where T : unmanaged, ITimer {

        [BURST]
        public struct Job : IJobForComponents<T> {
            [InjectDeltaTime]
            public tfloat dt;
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                component.timer -= this.dt;
                if (component.timer <= 0f) {
                    component.timer = 0f;
                }
            }
        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    [BURST]
    [SystemGenericParallelMode]
    public struct TimersMsUpdateSystem<T> : IUpdate where T : unmanaged, ITimerMs {

        [BURST]
        public struct Job : IJobForComponents<T> {
            [InjectDeltaTime]
            public uint dt;
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                if (component.timer >= this.dt) {
                    component.timer -= this.dt;
                } else {
                    component.timer = 0u;
                }
            }
        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    [BURST]
    [SystemGenericParallelMode]
    public struct TimersAutoDestroyUpdateSystem<T> : IUpdate where T : unmanaged, ITimerAutoDestroy {

        [BURST]
        public struct Job : IJobForComponents<T> {
            [InjectDeltaTime]
            public tfloat dt;
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                component.timer -= this.dt;
                if (component.timer <= 0f) {
                    ent.Remove<T>();
                }
            }
        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

    [BURST]
    [SystemGenericParallelMode]
    public struct TimersMsAutoDestroyUpdateSystem<T> : IUpdate where T : unmanaged, ITimerMsAutoDestroy {

        [BURST]
        public struct Job : IJobForComponents<T> {
            [InjectDeltaTime]
            public uint dt;
            public void Execute(in JobInfo jobInfo, in Ent ent, ref T component) {
                if (component.timer >= this.dt) {
                    component.timer -= this.dt;
                } else {
                    ent.Remove<T>();
                }
            }
        }

        public void OnUpdate(ref SystemContext context) {

            context.Query().AsParallel().Schedule<Job, T>().AddDependency(ref context);

        }

    }

}